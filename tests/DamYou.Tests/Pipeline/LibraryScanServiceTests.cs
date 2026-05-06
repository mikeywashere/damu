using DamYou.Data;
using DamYou.Data.Analysis;
using DamYou.Data.Entities;
using DamYou.Data.Pipeline;
using DamYou.Data.Repositories;
using DamYou.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace DamYou.Tests.Pipeline;

public sealed class LibraryScanServiceTests : IDisposable
{
    private readonly DamYouDbContext _db;
    private readonly FolderRepository _folderRepo;
    private readonly PipelineTaskRepository _taskRepo;
    private readonly Mock<IPipelineProcessorService> _pipelineProcessorMock;
    private readonly LibraryScanService _sut;
    private readonly SyntheticPhotoFixture _fixture;

    public LibraryScanServiceTests()
    {
        var options = new DbContextOptionsBuilder<DamYouDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new DamYouDbContext(options);
        _folderRepo = new FolderRepository(_db);
        _taskRepo = new PipelineTaskRepository(_db);
        _pipelineProcessorMock = new Mock<IPipelineProcessorService>();
        _pipelineProcessorMock
            .Setup(p => p.ProcessQueueAsync(It.IsAny<IProgress<AnalysisProgress>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _sut = new LibraryScanService(_db, _folderRepo, _taskRepo, _pipelineProcessorMock.Object);
        _fixture = new SyntheticPhotoFixture();
    }

    public void Dispose()
    {
        _db.Dispose();
        _fixture.Dispose();
    }

    private async Task AddWatchedFolderAsync(string path)
        => await _folderRepo.AddFoldersAsync([path]);

    [Fact]
    public async Task ScanAsync_CreatesScanLibraryTask()
    {
        await AddWatchedFolderAsync(_fixture.RootDirectory);

        await _sut.ScanAsync(null, CancellationToken.None);

        var scanTask = await _db.PipelineTasks
            .FirstOrDefaultAsync(t => t.TaskName == "Scan Library", CancellationToken.None);
        Assert.NotNull(scanTask);
    }

    [Fact]
    public async Task ScanAsync_ScanLibraryTask_IsCompletedAfterSuccess()
    {
        _fixture.CreatePhotos(2);
        await AddWatchedFolderAsync(_fixture.RootDirectory);

        await _sut.ScanAsync(null, CancellationToken.None);

        var scanTask = await _db.PipelineTasks
            .SingleAsync(t => t.TaskName == "Scan Library", CancellationToken.None);
        Assert.Equal(PipelineTaskStatus.Completed, scanTask.Status);
    }

    [Fact]
    public async Task ScanAsync_EnqueuesProcessPhotoTask_ForEachNewFile()
    {
        _fixture.CreatePhotos(3);
        await AddWatchedFolderAsync(_fixture.RootDirectory);

        await _sut.ScanAsync(null, CancellationToken.None);

        var processPhotoTasks = await _db.PipelineTasks
            .Where(t => t.TaskName == "Process Photo")
            .ToListAsync(CancellationToken.None);
        Assert.Equal(3, processPhotoTasks.Count);
    }

    [Fact]
    public async Task ScanAsync_NewPhotos_HaveIsProcessedFalse()
    {
        _fixture.CreatePhotos(3);
        await AddWatchedFolderAsync(_fixture.RootDirectory);

        await _sut.ScanAsync(null, CancellationToken.None);

        var photos = await _db.Photos.ToListAsync(CancellationToken.None);
        Assert.All(photos, p => Assert.Equal(ProcessingStatus.Unprocessed, p.Status));
    }

    [Fact]
    public async Task ScanAsync_SkipsAlreadyQueuedPhoto()
    {
        var filePath = _fixture.CreatePhoto("existing.jpg");
        await AddWatchedFolderAsync(_fixture.RootDirectory);

        // Pre-populate a Photo and an existing Queued task for it
        var folder = (await _folderRepo.GetActiveFoldersAsync(CancellationToken.None)).First();
        var photo = new Photo
        {
            WatchedFolderId = folder.Id,
            FileName = Path.GetFileName(filePath),
            FilePath = filePath,
            FileSizeBytes = new FileInfo(filePath).Length,
            Status = ProcessingStatus.Unprocessed,
            DateIndexed = DateTime.UtcNow
        };
        _db.Photos.Add(photo);
        await _db.SaveChangesAsync(CancellationToken.None);

        _db.PipelineTasks.Add(new PipelineTask
        {
            TaskName = "Process Photo",
            PhotoId = photo.Id,
            Status = PipelineTaskStatus.Queued
        });
        await _db.SaveChangesAsync(CancellationToken.None);

        await _sut.ScanAsync(null, CancellationToken.None);

        var processPhotoTasks = await _db.PipelineTasks
            .Where(t => t.TaskName == "Process Photo")
            .ToListAsync(CancellationToken.None);
        Assert.Single(processPhotoTasks);
    }

    [Fact]
    public async Task ScanAsync_ReEnqueues_UnprocessedPhotoWithNoQueuedTask()
    {
        var filePath = _fixture.CreatePhoto("unprocessed.jpg");
        await AddWatchedFolderAsync(_fixture.RootDirectory);

        // Pre-populate a Photo with no queue task
        var folder = (await _folderRepo.GetActiveFoldersAsync(CancellationToken.None)).First();
        var photo = new Photo
        {
            WatchedFolderId = folder.Id,
            FileName = Path.GetFileName(filePath),
            FilePath = filePath,
            FileSizeBytes = new FileInfo(filePath).Length,
            Status = ProcessingStatus.Unprocessed,
            DateIndexed = DateTime.UtcNow
        };
        _db.Photos.Add(photo);
        await _db.SaveChangesAsync(CancellationToken.None);

        await _sut.ScanAsync(null, CancellationToken.None);

#pragma warning disable HAA0301 // Closure Allocation Source
        var processPhotoTasks = await _db.PipelineTasks
            .Where(t => t.TaskName == "Process Photo" && t.PhotoId == photo.Id)
            .ToListAsync(CancellationToken.None);
#pragma warning restore HAA0301 // Closure Allocation Source
        Assert.Single(processPhotoTasks);
    }

    [Fact]
    public async Task ScanAsync_HandlesEmptyFolder()
    {
        await AddWatchedFolderAsync(_fixture.RootDirectory);

#pragma warning disable HAA0301 // Closure Allocation Source
        var ex = await Record.ExceptionAsync(() => _sut.ScanAsync(null, CancellationToken.None));
#pragma warning restore HAA0301 // Closure Allocation Source

        Assert.Null(ex);
#pragma warning disable HAA0301 // Closure Allocation Source
        var processPhotoTasks = await _db.PipelineTasks
            .CountAsync(CancellationToken.None);
#pragma warning restore HAA0301 // Closure Allocation Source
        Assert.Equal(0, processPhotoTasks);
    }

    [Fact]
    public async Task ScanAsync_ReportsProgress()
    {
        _fixture.CreatePhotos(3);
        await AddWatchedFolderAsync(_fixture.RootDirectory);

        var reports = new List<ScanProgress>();
#pragma warning disable HAA0301 // Closure Allocation Source
        var progress = new Progress<ScanProgress>(p => reports.Add(p));
#pragma warning restore HAA0301 // Closure Allocation Source

        await _sut.ScanAsync(progress, CancellationToken.None);

        // Progress<T> posts asynchronously; give it a moment to flush
        await Task.Delay(50, CancellationToken.None);

        Assert.NotEmpty(reports);
#pragma warning disable HAA0301 // Closure Allocation Source
        Assert.Contains(reports, r => r.TotalDiscovered > 0);
#pragma warning restore HAA0301 // Closure Allocation Source
    }

    [Fact]
    public async Task ScanAsync_RespectsCancellationToken()
    {
        _fixture.CreatePhotos(20);
        await AddWatchedFolderAsync(_fixture.RootDirectory);

        using var cts = new CancellationTokenSource();
#pragma warning disable HAA0301 // Closure Allocation Source
        var progress = new Progress<ScanProgress>(_ => cts.Cancel());
#pragma warning restore HAA0301 // Closure Allocation Source

#pragma warning disable HAA0301 // Closure Allocation Source
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _sut.ScanAsync(progress, cts.Token));
#pragma warning restore HAA0301 // Closure Allocation Source
    }
}
