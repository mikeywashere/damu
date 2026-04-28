using DamYou.Data;
using DamYou.Data.Entities;
using DamYou.Data.Pipeline;
using DamYou.Data.Repositories;
using DamYou.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace DamYou.Tests.Pipeline;

public sealed class LibraryScanServiceTests : IDisposable
{
    private readonly DamYouDbContext _db;
    private readonly FolderRepository _folderRepo;
    private readonly PipelineTaskRepository _taskRepo;
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
        _sut = new LibraryScanService(_db, _folderRepo, _taskRepo);
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

        await _sut.ScanAsync();

        var scanTask = await _db.PipelineTasks
            .FirstOrDefaultAsync(t => t.TaskName == "Scan Library");
        Assert.NotNull(scanTask);
    }

    [Fact]
    public async Task ScanAsync_ScanLibraryTask_IsCompletedAfterSuccess()
    {
        _fixture.CreatePhotos(2);
        await AddWatchedFolderAsync(_fixture.RootDirectory);

        await _sut.ScanAsync();

        var scanTask = await _db.PipelineTasks
            .SingleAsync(t => t.TaskName == "Scan Library");
        Assert.Equal(PipelineTaskStatus.Completed, scanTask.Status);
    }

    [Fact]
    public async Task ScanAsync_EnqueuesProcessPhotoTask_ForEachNewFile()
    {
        _fixture.CreatePhotos(3);
        await AddWatchedFolderAsync(_fixture.RootDirectory);

        await _sut.ScanAsync();

        var processPhotoTasks = await _db.PipelineTasks
            .Where(t => t.TaskName == "Process Photo")
            .ToListAsync();
        Assert.Equal(3, processPhotoTasks.Count);
    }

    [Fact]
    public async Task ScanAsync_NewPhotos_HaveIsProcessedFalse()
    {
        _fixture.CreatePhotos(3);
        await AddWatchedFolderAsync(_fixture.RootDirectory);

        await _sut.ScanAsync();

        var photos = await _db.Photos.ToListAsync();
        Assert.All(photos, p => Assert.Equal(ProcessingStatus.Unprocessed, p.Status));
    }

    [Fact]
    public async Task ScanAsync_SkipsAlreadyQueuedPhoto()
    {
        var filePath = _fixture.CreatePhoto("existing.jpg");
        await AddWatchedFolderAsync(_fixture.RootDirectory);

        // Pre-populate a Photo and an existing Queued task for it
        var folder = (await _folderRepo.GetActiveFoldersAsync()).First();
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
        await _db.SaveChangesAsync();

        _db.PipelineTasks.Add(new PipelineTask
        {
            TaskName = "Process Photo",
            PhotoId = photo.Id,
            Status = PipelineTaskStatus.Queued
        });
        await _db.SaveChangesAsync();

        await _sut.ScanAsync();

        var processPhotoTasks = await _db.PipelineTasks
            .Where(t => t.TaskName == "Process Photo")
            .ToListAsync();
        Assert.Single(processPhotoTasks);
    }

    [Fact]
    public async Task ScanAsync_ReEnqueues_UnprocessedPhotoWithNoQueuedTask()
    {
        var filePath = _fixture.CreatePhoto("unprocessed.jpg");
        await AddWatchedFolderAsync(_fixture.RootDirectory);

        // Pre-populate a Photo with no queue task
        var folder = (await _folderRepo.GetActiveFoldersAsync()).First();
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
        await _db.SaveChangesAsync();

        await _sut.ScanAsync();

        var processPhotoTasks = await _db.PipelineTasks
            .Where(t => t.TaskName == "Process Photo" && t.PhotoId == photo.Id)
            .ToListAsync();
        Assert.Single(processPhotoTasks);
    }

    [Fact]
    public async Task ScanAsync_HandlesEmptyFolder()
    {
        await AddWatchedFolderAsync(_fixture.RootDirectory);

        var ex = await Record.ExceptionAsync(() => _sut.ScanAsync());

        Assert.Null(ex);
        var processPhotoTasks = await _db.PipelineTasks
            .Where(t => t.TaskName == "Process Photo")
            .CountAsync();
        Assert.Equal(0, processPhotoTasks);
    }

    [Fact]
    public async Task ScanAsync_ReportsProgress()
    {
        _fixture.CreatePhotos(3);
        await AddWatchedFolderAsync(_fixture.RootDirectory);

        var reports = new List<ScanProgress>();
        var progress = new Progress<ScanProgress>(p => reports.Add(p));

        await _sut.ScanAsync(progress);

        // Progress<T> posts asynchronously; give it a moment to flush
        await Task.Delay(50);

        Assert.NotEmpty(reports);
        Assert.Contains(reports, r => r.TotalDiscovered > 0);
    }

    [Fact]
    public async Task ScanAsync_RespectsCancellationToken()
    {
        _fixture.CreatePhotos(20);
        await AddWatchedFolderAsync(_fixture.RootDirectory);

        using var cts = new CancellationTokenSource();
        var progress = new Progress<ScanProgress>(_ => cts.Cancel());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _sut.ScanAsync(progress, cts.Token));
    }
}
