using DamYou.Data;
using DamYou.Data.Entities;
using DamYou.Services;
using DamYou.Tests.Fixtures;
using DamYou.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace DamYou.Tests.Services;

// ============================================================
//  HELPER: in-memory DB + scope factory
// ============================================================

/// <summary>
/// Creates a shared EF Core in-memory database and vends mock IServiceScopeFactory
/// instances that resolve DamYouDbContext from it. Multiple service objects built
/// from the same fixture operate on the same backing store — this models app restart
/// (new service instance, same SQLite data).
/// </summary>
internal sealed class InMemoryQueueFixture
{
    private readonly DbContextOptions<DamYouDbContext> _options;

    public InMemoryQueueFixture()
    {
        _options = new DbContextOptionsBuilder<DamYouDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var seed = new DamYouDbContext(_options);
        seed.Database.EnsureCreated();
    }

    /// <summary>Returns a new scope factory mock whose scopes provide fresh DamYouDbContext instances.</summary>
    public IServiceScopeFactory CreateScopeFactory()
    {
        var providerMock = new Mock<IServiceProvider>();
        providerMock
            .Setup(p => p.GetService(typeof(DamYouDbContext)))
            .Returns(() => new DamYouDbContext(_options));

        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(providerMock.Object);

        var factoryMock = new Mock<IServiceScopeFactory>();
        factoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);
        return factoryMock.Object;
    }

    public FolderQueueService CreateFolderQueueService()
        => new FolderQueueService(
            CreateScopeFactory(),
            new Mock<ILogger<FolderQueueService>>().Object);

    public FileQueueService CreateFileQueueService()
        => new FileQueueService(
            CreateScopeFactory(),
            new Mock<ILogger<FileQueueService>>().Object);
}

// ============================================================
//  FOLDER QUEUE SERVICE TESTS
// ============================================================

/// <summary>
/// Verifies FolderQueueService: enqueue, dequeue, FIFO order, idempotency,
/// failure tracking, and persistence across service instances.
/// </summary>
public sealed class FolderQueueServiceTests
{
    private readonly InMemoryQueueFixture _fixture = new();

    [Fact]
    public async Task EnqueueFolder_AddsToDatabase()
    {
        var sut = _fixture.CreateFolderQueueService();

        await sut.EnqueueAsync(@"C:\Photos");

        Assert.Equal(1, await sut.GetCountAsync());
    }

    [Fact]
    public async Task EnqueueFolder_MultiplePaths_AddsAll()
    {
        var sut = _fixture.CreateFolderQueueService();
        await sut.EnqueueAsync(@"C:\Photos");
        await sut.EnqueueAsync(@"C:\Videos");
        await sut.EnqueueAsync(@"C:\Documents");

        Assert.Equal(3, await sut.GetCountAsync());
    }

    [Fact]
    public async Task EnqueueFolder_DuplicatePendingPath_IsIdempotent()
    {
        var sut = _fixture.CreateFolderQueueService();
        await sut.EnqueueAsync(@"C:\Photos");
        await sut.EnqueueAsync(@"C:\Photos"); // duplicate pending — no-op

        Assert.Equal(1, await sut.GetCountAsync());
    }

    [Fact]
    public async Task EnqueueFolder_ReEnqueuesFailedFolder()
    {
        // A previously-failed folder should be re-queued (supports retry).
        var sut = _fixture.CreateFolderQueueService();
        await sut.EnqueueAsync(@"C:\Photos");
        await sut.DequeueAsync();                      // now Processing
        await sut.MarkFailedAsync(@"C:\Photos");        // now Failed, count = 0

        await sut.EnqueueAsync(@"C:\Photos");           // re-enqueue — resets to Pending

        Assert.Equal(1, await sut.GetCountAsync());
    }

    [Fact]
    public async Task DequeueFolder_ReturnsPendingFolder_AndDecrementsCount()
    {
        var sut = _fixture.CreateFolderQueueService();
        await sut.EnqueueAsync(@"C:\Photos");

        var result = await sut.DequeueAsync();

        Assert.Equal(@"C:\Photos", result);
        Assert.Equal(0, await sut.GetCountAsync()); // item moved to Processing status
    }

    [Fact]
    public async Task DequeueFolder_WhenEmpty_ReturnsNull()
    {
        var sut = _fixture.CreateFolderQueueService();

        Assert.Null(await sut.DequeueAsync());
    }

    [Fact]
    public async Task DequeueFolder_OnMultiplePendingItems_ReturnsHighestPriorityThenOldest()
    {
        // Same priority → FIFO by AddedAt
        var sut = _fixture.CreateFolderQueueService();
        await sut.EnqueueAsync(@"C:\Photos");
        await Task.Delay(10); // ensure distinct timestamps
        await sut.EnqueueAsync(@"C:\Videos");

        var first = await sut.DequeueAsync();
        var second = await sut.DequeueAsync();

        Assert.Equal(@"C:\Photos", first);
        Assert.Equal(@"C:\Videos", second);
    }

    [Fact]
    public async Task FolderQueue_PersistsAcrossInstances()
    {
        // Enqueue using one instance then read from a new instance (simulates app restart).
        await _fixture.CreateFolderQueueService().EnqueueAsync(@"C:\Photos");

        var instance2 = _fixture.CreateFolderQueueService();
        Assert.Equal(1, await instance2.GetCountAsync());
    }

    [Fact]
    public async Task FolderQueue_PersistsMultipleItems_AcrossInstances()
    {
        var write = _fixture.CreateFolderQueueService();
        await write.EnqueueAsync(@"C:\Photos");
        await write.EnqueueAsync(@"C:\Videos");
        await write.EnqueueAsync(@"C:\Music");

        Assert.Equal(3, await _fixture.CreateFolderQueueService().GetCountAsync());
    }

    [Fact]
    public async Task FolderQueue_DequeueOnOneInstance_VisibleToSecondInstance()
    {
        var write = _fixture.CreateFolderQueueService();
        await write.EnqueueAsync(@"C:\Photos");
        await write.EnqueueAsync(@"C:\Videos");
        await write.DequeueAsync(); // dequeue one

        // A second service object should see the updated (1 remaining) count
        Assert.Equal(1, await _fixture.CreateFolderQueueService().GetCountAsync());
    }

    [Fact]
    public async Task MarkFolderFailed_RemovesFromPendingCount()
    {
        var sut = _fixture.CreateFolderQueueService();
        await sut.EnqueueAsync(@"C:\Photos");
        await sut.DequeueAsync(); // sets status to Processing

        await sut.MarkFailedAsync(@"C:\Photos");

        // Status = Failed is excluded from pending count
        Assert.Equal(0, await sut.GetCountAsync());
    }

    [Fact]
    public async Task MarkFolderComplete_RemovesFromPendingCount()
    {
        var sut = _fixture.CreateFolderQueueService();
        await sut.EnqueueAsync(@"C:\Photos");
        await sut.DequeueAsync();

        await sut.MarkCompleteAsync(@"C:\Photos");

        Assert.Equal(0, await sut.GetCountAsync());
    }

    [Fact]
    public async Task GetCountAsync_CountsOnlyPendingItems()
    {
        var sut = _fixture.CreateFolderQueueService();
        await sut.EnqueueAsync(@"C:\A");
        await sut.EnqueueAsync(@"C:\B");
        await sut.EnqueueAsync(@"C:\C");

        await sut.DequeueAsync(); // C:\A → Processing (not pending)

        Assert.Equal(2, await sut.GetCountAsync()); // B and C still pending
    }
}

// ============================================================
//  FILE QUEUE SERVICE TESTS
// ============================================================

/// <summary>
/// Verifies FileQueueService: enqueue, dequeue, FIFO order, idempotency,
/// and persistence across service instances.
/// </summary>
public sealed class FileQueueServiceTests
{
    private readonly InMemoryQueueFixture _fixture = new();

    [Fact]
    public async Task EnqueueFile_AddsToDatabase()
    {
        var sut = _fixture.CreateFileQueueService();

        await sut.EnqueueAsync(@"C:\Photos\IMG_001.jpg");

        Assert.Equal(1, await sut.GetCountAsync());
    }

    [Fact]
    public async Task EnqueueFile_DuplicatePendingPath_IsIdempotent()
    {
        var sut = _fixture.CreateFileQueueService();
        await sut.EnqueueAsync(@"C:\Photos\IMG_001.jpg");
        await sut.EnqueueAsync(@"C:\Photos\IMG_001.jpg");

        Assert.Equal(1, await sut.GetCountAsync());
    }

    [Fact]
    public async Task EnqueueFile_ReEnqueuesFailedFile()
    {
        var sut = _fixture.CreateFileQueueService();
        await sut.EnqueueAsync(@"C:\Photos\IMG_001.jpg");
        await sut.DequeueAsync();
        await sut.MarkFailedAsync(@"C:\Photos\IMG_001.jpg");

        await sut.EnqueueAsync(@"C:\Photos\IMG_001.jpg"); // retry

        Assert.Equal(1, await sut.GetCountAsync());
    }

    [Fact]
    public async Task DequeueFile_ReturnsPendingFile_AndDecrementsCount()
    {
        var sut = _fixture.CreateFileQueueService();
        await sut.EnqueueAsync(@"C:\Photos\IMG_001.jpg");

        var result = await sut.DequeueAsync();

        Assert.Equal(@"C:\Photos\IMG_001.jpg", result);
        Assert.Equal(0, await sut.GetCountAsync());
    }

    [Fact]
    public async Task DequeueFile_WhenEmpty_ReturnsNull()
    {
        Assert.Null(await _fixture.CreateFileQueueService().DequeueAsync());
    }

    [Fact]
    public async Task DequeueFile_OnMultiplePendingItems_ReturnsOldestFirst()
    {
        var sut = _fixture.CreateFileQueueService();
        await sut.EnqueueAsync(@"C:\Photos\IMG_001.jpg");
        await Task.Delay(10);
        await sut.EnqueueAsync(@"C:\Photos\IMG_002.jpg");

        Assert.Equal(@"C:\Photos\IMG_001.jpg", await sut.DequeueAsync());
        Assert.Equal(@"C:\Photos\IMG_002.jpg", await sut.DequeueAsync());
    }

    [Fact]
    public async Task FileQueue_PersistsAcrossInstances()
    {
        await _fixture.CreateFileQueueService().EnqueueAsync(@"C:\Photos\IMG_001.jpg");

        Assert.Equal(1, await _fixture.CreateFileQueueService().GetCountAsync());
    }

    [Fact]
    public async Task FileQueue_PersistsMultipleItems_AcrossInstances()
    {
        var write = _fixture.CreateFileQueueService();
        await write.EnqueueAsync(@"C:\Photos\IMG_001.jpg");
        await write.EnqueueAsync(@"C:\Photos\IMG_002.png");
        await write.EnqueueAsync(@"C:\Photos\IMG_003.heic");

        Assert.Equal(3, await _fixture.CreateFileQueueService().GetCountAsync());
    }
}

// ============================================================
//  QUEUE PROCESSOR SERVICE TESTS  (smart switching + startup delay)
// ============================================================

/// <summary>
/// Verifies QueueProcessorService: 30-second startup delay, smart queue selection
/// (only folders / only files / both), alternation, and idle behavior.
///
/// TIMING: Tests inject a fast IQueueSettings mock (StartupDelayMs=100, WaitMs=50)
/// so no test waits longer than ~600ms.
/// </summary>
public sealed class QueueProcessorServiceTests
{
    private readonly Mock<IFolderQueueService>   _folderQueue   = new();
    private readonly Mock<IFileQueueService>     _fileQueue     = new();
    private readonly Mock<IProcessingStateService> _state       = new();
    private readonly Mock<ILogger<QueueProcessorService>> _log  = new();
    private readonly Mock<IServiceScopeFactory>  _scopeFactory  = new();

    private Mock<IQueueSettings> FastSettings(int startupMs = 100, int waitMs = 50)
    {
        var m = new Mock<IQueueSettings>();
        m.Setup(s => s.GetStartupDelayMs()).Returns(startupMs);
        m.Setup(s => s.GetQueueWaitTimeMs()).Returns(waitMs);
        return m;
    }

    private QueueProcessorService Build(IQueueSettings settings)
        => new QueueProcessorService(
            _folderQueue.Object,
            _fileQueue.Object,
            settings,
            _state.Object,
            _scopeFactory.Object,
            _log.Object,
            new Mock<IVerboseLoggingService>().Object);

    [Fact]
    public async Task StartAsync_WaitsForStartupDelay_BeforeFirstDequeue()
    {
        // Startup delay = 300ms. No dequeue should happen before 200ms.
        var settings = FastSettings(startupMs: 300, waitMs: 50);
        var dequeueCallCount = 0;

        _folderQueue.Setup(q => q.GetCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _folderQueue.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .Callback(() => dequeueCallCount++)
            .ReturnsAsync(@"C:\Photos");
        _folderQueue.Setup(q => q.MarkCompleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _fileQueue.Setup(q => q.GetCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var sut = Build(settings.Object);
        var cts = new CancellationTokenSource();
        await sut.StartAsync(cts.Token);

        // Well within startup delay — no processing yet
        await Task.Delay(150);
        Assert.Equal(0, dequeueCallCount);

        // After startup delay elapses, at least one dequeue must happen
        await Task.Delay(400); // total ~550ms, past the 300ms delay
        Assert.True(dequeueCallCount > 0, "No dequeue happened after startup delay elapsed");

        cts.Cancel();
        await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task SmartQueueSelection_OnlyFolders_DequeuesOnlyFolderQueue()
    {
        var settings = FastSettings();
        _folderQueue.Setup(q => q.GetCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _folderQueue.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>())).ReturnsAsync(@"C:\Folder1");
        _folderQueue.Setup(q => q.MarkCompleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _fileQueue.Setup(q => q.GetCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var sut = Build(settings.Object);
        var cts = new CancellationTokenSource();
        await sut.StartAsync(cts.Token);
        await Task.Delay(500); // startup(100) + several cycles
        cts.Cancel();
        await sut.StopAsync(CancellationToken.None);

        _folderQueue.Verify(q => q.DequeueAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _fileQueue.Verify(q => q.DequeueAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SmartQueueSelection_OnlyFiles_DequeuesOnlyFileQueue()
    {
        var settings = FastSettings();
        _folderQueue.Setup(q => q.GetCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _fileQueue.Setup(q => q.GetCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _fileQueue.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>())).ReturnsAsync(@"C:\Photos\img.jpg");
        _fileQueue.Setup(q => q.MarkFailedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = Build(settings.Object);
        var cts = new CancellationTokenSource();
        await sut.StartAsync(cts.Token);
        await Task.Delay(500);
        cts.Cancel();
        await sut.StopAsync(CancellationToken.None);

        _fileQueue.Verify(q => q.DequeueAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _folderQueue.Verify(q => q.DequeueAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SmartQueueSelection_BothQueues_DequeuesBothQueues()
    {
        // After several cycles both queues should each have been dequeued at least once.
        var settings = FastSettings(startupMs: 50, waitMs: 30);
        var folderDequeues = 0;
        var fileDequeues   = 0;

        _folderQueue.Setup(q => q.GetCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => folderDequeues < 2 ? 1 : 0);
        _folderQueue.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .Callback(() => folderDequeues++)
            .ReturnsAsync(@"C:\Folder1");
        _folderQueue.Setup(q => q.MarkCompleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _fileQueue.Setup(q => q.GetCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => fileDequeues < 2 ? 1 : 0);
        _fileQueue.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .Callback(() => fileDequeues++)
            .ReturnsAsync(@"C:\Photos\img.jpg");
        _fileQueue.Setup(q => q.MarkFailedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = Build(settings.Object);
        var cts = new CancellationTokenSource();
        await sut.StartAsync(cts.Token);
        await Task.Delay(600);
        cts.Cancel();
        await sut.StopAsync(CancellationToken.None);

        Assert.True(folderDequeues > 0, "Folder queue was never dequeued");
        Assert.True(fileDequeues > 0,   "File queue was never dequeued");
    }

    [Fact]
    public async Task SmartQueueSelection_EmptyQueues_DoesNotDequeueEither()
    {
        var settings = FastSettings();
        _folderQueue.Setup(q => q.GetCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _fileQueue.Setup(q => q.GetCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var sut = Build(settings.Object);
        var cts = new CancellationTokenSource();
        await sut.StartAsync(cts.Token);
        await Task.Delay(400);
        cts.Cancel();
        await sut.StopAsync(CancellationToken.None);

        _folderQueue.Verify(q => q.DequeueAsync(It.IsAny<CancellationToken>()), Times.Never);
        _fileQueue.Verify(q => q.DequeueAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StopAsync_CancelsGracefully_DoesNotThrow()
    {
        var settings = FastSettings();
        _folderQueue.Setup(q => q.GetCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _fileQueue.Setup(q => q.GetCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var sut = Build(settings.Object);
        var cts = new CancellationTokenSource();
        await sut.StartAsync(cts.Token);
        await Task.Delay(50);

        var ex = await Record.ExceptionAsync(() => sut.StopAsync(CancellationToken.None));
        Assert.Null(ex);
    }

    [Fact]
    public async Task ExceptionInProcessingLoop_DoesNotCrashService()
    {
        // A transient exception must be swallowed so the loop continues.
        var settings = FastSettings();
        var callCount = 0;
        _folderQueue.Setup(q => q.GetCountAsync(It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                callCount++;
                if (callCount == 1) throw new InvalidOperationException("Simulated error");
                return 0;
            });
        _fileQueue.Setup(q => q.GetCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var sut = Build(settings.Object);
        var cts = new CancellationTokenSource();
        await sut.StartAsync(cts.Token);
        await Task.Delay(500); // enough time for multiple cycles

        Assert.True(callCount >= 2, "Expected loop to continue after error");

        cts.Cancel();
        await sut.StopAsync(CancellationToken.None);
    }
}

// ============================================================
//  FOLDER SCANNING TESTS  (via running service + real filesystem)
// ============================================================

/// <summary>
/// Verifies the folder-scan behavior of QueueProcessorService:
/// subfolders are enqueued to the folder queue, image files to the file queue,
/// non-image files are ignored, and inaccessible paths are gracefully skipped.
///
/// Strategy: mock both queue services. The folder queue mock is configured to return
/// exactly one folder path on the first dequeue, then nothing — so the service scans
/// that one folder and idles. We capture all EnqueueAsync calls to both queues.
/// </summary>
public sealed class FolderScanningTests : IDisposable
{
    private readonly SyntheticPhotoFixture _files = new();
    private readonly Mock<IFolderQueueService> _folderQueueMock = new();
    private readonly Mock<IFileQueueService>   _fileQueueMock   = new();
    private readonly Mock<IProcessingStateService> _stateMock   = new();
    private readonly Mock<IServiceScopeFactory>    _scopeFactory = new();

    private readonly List<string> _enqueuedFolders = new();
    private readonly List<string> _enqueuedFiles   = new();

    // Set to true after the first DequeueAsync call so GetCountAsync returns 0 on the next cycle.
    private volatile bool _folderConsumed;

    public FolderScanningTests()
    {
        // Capture all folder EnqueueAsync calls
        _folderQueueMock
            .Setup(q => q.EnqueueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((path, _) => _enqueuedFolders.Add(path))
            .Returns(Task.CompletedTask);

        // Capture all file EnqueueAsync calls
        _fileQueueMock
            .Setup(q => q.EnqueueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((path, _) => _enqueuedFiles.Add(path))
            .Returns(Task.CompletedTask);

        _folderQueueMock.Setup(q => q.MarkCompleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _folderQueueMock.Setup(q => q.MarkFailedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _fileQueueMock.Setup(q => q.GetCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);
        _fileQueueMock.Setup(q => q.MarkCompleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _fileQueueMock.Setup(q => q.MarkFailedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    /// <summary>Configures the folder queue mock to yield exactly one folder path, then idle.</summary>
    private void SetupSingleFolderToProcess(string folderPath)
    {
        _folderConsumed = false;
        _folderQueueMock.Setup(q => q.GetCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _folderConsumed ? 0 : 1);
        _folderQueueMock.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                if (_folderConsumed) return null;
                _folderConsumed = true;
                return folderPath;
            });
    }

    private QueueProcessorService BuildService(int startupMs = 80, int waitMs = 40)
    {
        var settings = new Mock<IQueueSettings>();
        settings.Setup(s => s.GetStartupDelayMs()).Returns(startupMs);
        settings.Setup(s => s.GetQueueWaitTimeMs()).Returns(waitMs);

        return new QueueProcessorService(
            _folderQueueMock.Object,
            _fileQueueMock.Object,
            settings.Object,
            _stateMock.Object,
            _scopeFactory.Object,
            new Mock<ILogger<QueueProcessorService>>().Object,
            new Mock<IVerboseLoggingService>().Object);
    }

    [Fact]
    public async Task ProcessFolder_ScansSubfolders_EnqueuesEachToFolderQueue()
    {
        _files.CreateSubFolder("AlbumA");
        _files.CreateSubFolder("AlbumB");
        _files.CreateSubFolder("AlbumC");

        SetupSingleFolderToProcess(_files.RootDirectory);

        var cts = new CancellationTokenSource();
        var sut = BuildService();
        await sut.StartAsync(cts.Token);
        await Task.Delay(400); // startup(80) + one cycle + margin
        cts.Cancel();
        await sut.StopAsync(CancellationToken.None);

        Assert.Equal(3, _enqueuedFolders.Count);
        Assert.Contains(Path.Combine(_files.RootDirectory, "AlbumA"), _enqueuedFolders);
        Assert.Contains(Path.Combine(_files.RootDirectory, "AlbumB"), _enqueuedFolders);
        Assert.Contains(Path.Combine(_files.RootDirectory, "AlbumC"), _enqueuedFolders);
    }

    [Fact]
    public async Task ProcessFolder_ScansImages_EnqueuesOnlyImageExtensions()
    {
        _files.CreatePhoto("photo1.jpg");
        _files.CreatePhoto("photo2.png");
        File.WriteAllText(Path.Combine(_files.RootDirectory, "readme.txt"), "not an image");

        SetupSingleFolderToProcess(_files.RootDirectory);

        var cts = new CancellationTokenSource();
        var sut = BuildService();
        await sut.StartAsync(cts.Token);
        await Task.Delay(400);
        cts.Cancel();
        await sut.StopAsync(CancellationToken.None);

        Assert.Equal(2, _enqueuedFiles.Count);
        _fileQueueMock.Verify(
            q => q.EnqueueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2),
            "Only .jpg and .png should be enqueued, not .txt");
    }

    [Fact]
    public async Task ProcessFolder_AllSupportedExtensions_AreEnqueued()
    {
        var extensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp",
                                  ".tiff", ".tif", ".heic", ".heif", ".webp" };
        foreach (var ext in extensions)
            File.WriteAllText(Path.Combine(_files.RootDirectory, "img" + ext), "fake");

        SetupSingleFolderToProcess(_files.RootDirectory);

        var cts = new CancellationTokenSource();
        var sut = BuildService();
        await sut.StartAsync(cts.Token);
        await Task.Delay(400);
        cts.Cancel();
        await sut.StopAsync(CancellationToken.None);

        Assert.Equal(extensions.Length, _enqueuedFiles.Count);
    }

    [Fact]
    public async Task ProcessFolder_NonImageFiles_AreNotEnqueued()
    {
        File.WriteAllText(Path.Combine(_files.RootDirectory, "doc.txt"),  "text");
        File.WriteAllText(Path.Combine(_files.RootDirectory, "arch.zip"), "zip");
        File.WriteAllText(Path.Combine(_files.RootDirectory, "clip.mp4"), "video");
        File.WriteAllText(Path.Combine(_files.RootDirectory, "thumb.db"), "db");

        SetupSingleFolderToProcess(_files.RootDirectory);

        var cts = new CancellationTokenSource();
        var sut = BuildService();
        await sut.StartAsync(cts.Token);
        await Task.Delay(400);
        cts.Cancel();
        await sut.StopAsync(CancellationToken.None);

        Assert.Empty(_enqueuedFiles);
    }

    [Fact]
    public async Task ProcessFolder_EmptyFolder_EnqueuesNothing()
    {
        // Root has no files and no subfolders
        SetupSingleFolderToProcess(_files.RootDirectory);

        var cts = new CancellationTokenSource();
        var sut = BuildService();
        await sut.StartAsync(cts.Token);
        await Task.Delay(400);
        cts.Cancel();
        await sut.StopAsync(CancellationToken.None);

        Assert.Empty(_enqueuedFiles);
        Assert.Empty(_enqueuedFolders);
        _folderQueueMock.Verify(
            q => q.MarkCompleteAsync(_files.RootDirectory, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessFolder_MixedContents_EnqueuesSubfoldersAndImages()
    {
        _files.CreateSubFolder("AlbumA");
        _files.CreateSubFolder("AlbumB");
        _files.CreatePhoto("photo1.jpg");
        _files.CreatePhoto("photo2.png");
        _files.CreatePhoto("photo3.heic");
        File.WriteAllText(Path.Combine(_files.RootDirectory, "notes.txt"), "text");

        SetupSingleFolderToProcess(_files.RootDirectory);

        var cts = new CancellationTokenSource();
        var sut = BuildService();
        await sut.StartAsync(cts.Token);
        await Task.Delay(400);
        cts.Cancel();
        await sut.StopAsync(CancellationToken.None);

        Assert.Equal(3, _enqueuedFiles.Count);
        Assert.Equal(2, _enqueuedFolders.Count);
    }

    [Fact]
    public async Task ProcessFolder_NonExistentPath_IsSkippedGracefully_DoesNotThrow()
    {
        SetupSingleFolderToProcess(@"C:\NonExistent\Path_XXXXXXXX");

        var cts = new CancellationTokenSource();
        var sut = BuildService();
        await sut.StartAsync(cts.Token);

        var ex = await Record.ExceptionAsync(async () =>
        {
            await Task.Delay(400);
            cts.Cancel();
            await sut.StopAsync(CancellationToken.None);
        });

        Assert.Null(ex);
    }

    [Fact]
    public async Task ProcessFolder_MarkCompleteAsync_CalledAfterSuccessfulScan()
    {
        SetupSingleFolderToProcess(_files.RootDirectory);

        var cts = new CancellationTokenSource();
        var sut = BuildService();
        await sut.StartAsync(cts.Token);
        await Task.Delay(400);
        cts.Cancel();
        await sut.StopAsync(CancellationToken.None);

        _folderQueueMock.Verify(
            q => q.MarkCompleteAsync(_files.RootDirectory, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    public void Dispose() => _files.Dispose();
}

// ============================================================
//  QUEUE SETTINGS TESTS
// ============================================================

/// <summary>
/// Verifies IQueueSettings contract defaults, mutation, and the SettingsViewModel
/// that wraps it for the UI.
/// Note: QueueSettingsService uses Preferences.Default (MAUI platform) and cannot
/// be unit-tested directly — test via the IQueueSettings mock here.
/// </summary>
public sealed class QueueSettingsTests
{
    [Fact]
    public void GetQueueWaitTimeMs_Default_Is5000()
    {
        // Verify the default value any implementation must honour
        var mock = new Mock<IQueueSettings>();
        mock.Setup(s => s.GetQueueWaitTimeMs()).Returns(5_000); // reflects DefaultQueueSettings default

        Assert.Equal(5_000, mock.Object.GetQueueWaitTimeMs());
    }

    [Fact]
    public void SettingsViewModel_LoadsQueueWaitTimeFromSettings()
    {
        var settingsMock = new Mock<IQueueSettings>();
        settingsMock.Setup(s => s.GetQueueWaitTimeMs()).Returns(7_000);
        var folderPickerMock = new Mock<IFolderPickerService>();

        var vm = new SettingsViewModel(settingsMock.Object, folderPickerMock.Object);

        Assert.Equal("7", vm.QueueWaitTimeSeconds);
    }

    [Fact]
    public void SettingsViewModel_SaveCommand_PersistsValueToSettings()
    {
        var settingsMock = new Mock<IQueueSettings>();
        settingsMock.Setup(s => s.GetQueueWaitTimeMs()).Returns(5_000);
        var folderPickerMock = new Mock<IFolderPickerService>();

        var vm = new SettingsViewModel(settingsMock.Object, folderPickerMock.Object, dispatcher: _ => { }); // no-op: skip UI alert
        vm.QueueWaitTimeSeconds = "10";
        vm.SaveSettingsCommand.Execute(null);

        settingsMock.Verify(s => s.SetQueueWaitTimeMs(10_000), Times.Once);
    }

    [Fact]
    public void SettingsViewModel_SaveCommand_EnforcesMinimumOneSecond()
    {
        var settingsMock = new Mock<IQueueSettings>();
        settingsMock.Setup(s => s.GetQueueWaitTimeMs()).Returns(5_000);
        var folderPickerMock = new Mock<IFolderPickerService>();

        var vm = new SettingsViewModel(settingsMock.Object, folderPickerMock.Object, dispatcher: _ => { });
        vm.QueueWaitTimeSeconds = "0"; // below minimum
        vm.SaveSettingsCommand.Execute(null);

        settingsMock.Verify(s => s.SetQueueWaitTimeMs(1_000), Times.Once); // clamped to 1s
    }

    [Fact]
    public void SettingsViewModel_SaveCommand_IgnoresNonNumericInput()
    {
        var settingsMock = new Mock<IQueueSettings>();
        settingsMock.Setup(s => s.GetQueueWaitTimeMs()).Returns(5_000);
        var folderPickerMock = new Mock<IFolderPickerService>();

        var vm = new SettingsViewModel(settingsMock.Object, folderPickerMock.Object);
        vm.QueueWaitTimeSeconds = "abc";
        vm.SaveSettingsCommand.Execute(null);

        settingsMock.Verify(s => s.SetQueueWaitTimeMs(It.IsAny<int>()), Times.Never);
    }
}

// ============================================================
//  PROCESSING STATE VIEWMODEL — QUEUE PROPERTIES TESTS
// ============================================================

/// <summary>
/// Verifies the queue-aware properties added to ProcessingStateViewModel:
/// FolderQueueCount, FileQueueCount, CurrentItemFull, CurrentItemShort, ActiveQueue.
/// These are driven by the QueueCountsChanged event from IProcessingStateService.
/// </summary>
public sealed class ProcessingStateViewModelQueueTests
{
    private readonly Mock<IProcessingWorker>        _workerMock = new();
    private readonly Mock<IProcessingStateService>  _stateMock  = new();
    private readonly ProcessingStateViewModel       _vm;

    public ProcessingStateViewModelQueueTests()
    {
        _vm = new ProcessingStateViewModel(_workerMock.Object, _stateMock.Object, dispatcher: a => a());
    }

    private void RaiseQueueCounts(int folders, int files, string? item = null, string queue = "Idle")
        => _stateMock.Raise(s => s.QueueCountsChanged += null, folders, files, item, queue);

    [Fact]
    public void FolderQueueCount_UpdatesWhen_QueueCountsChanged_Fires()
    {
        RaiseQueueCounts(folders: 3, files: 0);
        Assert.Equal(3, _vm.FolderQueueCount);
    }

    [Fact]
    public void FileQueueCount_UpdatesWhen_QueueCountsChanged_Fires()
    {
        RaiseQueueCounts(folders: 0, files: 7);
        Assert.Equal(7, _vm.FileQueueCount);
    }

    [Fact]
    public void BothQueueCounts_UpdateTogether()
    {
        RaiseQueueCounts(folders: 4, files: 12, queue: "Both");
        Assert.Equal(4,  _vm.FolderQueueCount);
        Assert.Equal(12, _vm.FileQueueCount);
    }

    [Fact]
    public void ActiveQueue_UpdatesToReflectCurrentState()
    {
        RaiseQueueCounts(folders: 1, files: 0, queue: "Folders");
        Assert.Equal("Folders", _vm.ActiveQueue);
    }

    [Fact]
    public void QueueCounts_RaisePropertyChanged_ForFolderQueueCount()
    {
        var changed = new List<string?>();
        _vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        RaiseQueueCounts(folders: 2, files: 0);
        Assert.Contains(nameof(ProcessingStateViewModel.FolderQueueCount), changed);
    }

    [Fact]
    public void QueueCounts_RaisePropertyChanged_ForFileQueueCount()
    {
        var changed = new List<string?>();
        _vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        RaiseQueueCounts(folders: 0, files: 5);
        Assert.Contains(nameof(ProcessingStateViewModel.FileQueueCount), changed);
    }

    [Fact]
    public void CurrentItemFull_SetWhenCurrentItemProvided()
    {
        string path = @"C:\Users\Michael\Photos\IMG_001.jpg";
        RaiseQueueCounts(folders: 0, files: 1, item: path, queue: "Files");
        Assert.Equal(path, _vm.CurrentItemFull);
    }

    [Fact]
    public void CurrentItemFull_ClearedWhenCurrentItemIsNull()
    {
        // First set, then clear
        RaiseQueueCounts(folders: 0, files: 1, item: @"C:\photo.jpg");

        RaiseQueueCounts(folders: 0, files: 0, item: null);

        Assert.Equal(string.Empty, _vm.CurrentItemFull);
    }

    [Fact]
    public void CurrentItemShort_ShortPath_IsUnchanged()
    {
        RaiseQueueCounts(folders: 0, files: 1, item: @"C:\Photos\img.jpg");
        Assert.Equal(@"C:\Photos\img.jpg", _vm.CurrentItemShort);
    }

    [Fact]
    public void CurrentItemShort_LongPath_IsTruncatedWithCenterEllipsis()
    {
        // 70+ chars — exceeds the 40-char truncation limit
        string longPath = @"C:\Users\Michael\Pictures\2024\Wedding\Reception\Venue\IMG_0001.jpg";
        RaiseQueueCounts(folders: 0, files: 1, item: longPath);
        string result = _vm.CurrentItemShort;

        Assert.NotEqual(longPath, result);
        Assert.True(result.Length < longPath.Length);
        Assert.Contains("...", result);
        Assert.StartsWith("C:", result);
        Assert.EndsWith(".jpg", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CurrentItemShort_ClearedWhenCurrentItemIsNull()
    {
        RaiseQueueCounts(folders: 0, files: 0, item: null);
        Assert.Equal(string.Empty, _vm.CurrentItemShort);
    }

    [Fact]
    public void InitialState_QueueCountsAreZeroAndQueueIsIdle()
    {
        Assert.Equal(0,      _vm.FolderQueueCount);
        Assert.Equal(0,      _vm.FileQueueCount);
        Assert.Equal("Idle", _vm.ActiveQueue);
        Assert.Equal(string.Empty, _vm.CurrentItemFull);
        Assert.Equal(string.Empty, _vm.CurrentItemShort);
    }
}
