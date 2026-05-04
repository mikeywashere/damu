using DamYou.Data;
using DamYou.Data.Analysis;
using DamYou.Data.Entities;
using DamYou.Data.Pipeline;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DamYou.Services;

/// <summary>
/// IHostedService that drives the two-queue processing loop (folders + files).
///
/// Lifecycle:
/// - StartAsync: waits 30 seconds (startup delay), then launches the processing loop.
/// - StopAsync: cancels the loop and waits for it to finish.
///
/// Processing logic:
/// - Both queues have items → one folder tick then one file tick per cycle
/// - Only folders → process folder only
/// - Only files → process file only
/// - Both empty → idle, wait for next cycle
///
/// Folder processing:
///   Dequeue folder → scan sub-directories (enqueue) → scan image files (enqueue to file queue)
///   → mark folder complete
///
/// File processing:
///   Dequeue file path → import as Photo (if new) → create PipelineTask → run analysis pipeline
///
/// Crash recovery: items stuck in 'Processing' status are skipped on restart.
/// They can be manually retried or will time out in a future maintenance pass.
/// </summary>
public sealed class QueueProcessorService : IHostedService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif",
        ".heic", ".heif", ".webp", ".raw", ".arw", ".cr2", ".nef",
        ".orf", ".dng", ".rw2", ".pef", ".srw", ".x3f"
    };

    private readonly IFolderQueueService _folderQueue;
    private readonly IFileQueueService _fileQueue;
    private readonly IQueueSettings _queueSettings;
    private readonly IProcessingStateService _processingState;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QueueProcessorService> _logger;
    private readonly IVerboseLoggingService _loggingService;
    private int current = 0;

    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public QueueProcessorService(
        IFolderQueueService folderQueue,
        IFileQueueService fileQueue,
        IQueueSettings queueSettings,
        IProcessingStateService processingState,
        IServiceScopeFactory scopeFactory,
        ILogger<QueueProcessorService> logger,
        IVerboseLoggingService loggingService)
    {
        _folderQueue = folderQueue;
        _fileQueue = fileQueue;
        _queueSettings = queueSettings;
        _processingState = processingState;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _loggingService = loggingService;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("QueueProcessorService starting — {DelayMs}ms startup delay", _queueSettings.GetStartupDelayMs());
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loopTask = RunWithStartupDelayAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("QueueProcessorService stopping");
        _cts?.Cancel();

        if (_loopTask is not null)
        {
            try { await _loopTask.WaitAsync(cancellationToken); }
            catch (OperationCanceledException) { }
        }

        _cts?.Dispose();
        _logger.LogInformation("QueueProcessorService stopped");
    }

    private async Task RunWithStartupDelayAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(_queueSettings.GetStartupDelayMs(), ct);
            _logger.LogInformation("QueueProcessorService startup delay complete — entering processing loop");
            await ProcessingLoopAsync(ct);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("QueueProcessorService startup delay or loop cancelled");
        }
    }

    private async Task ProcessingLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var folderCount = await _folderQueue.GetCountAsync(ct);
                var fileCount = await _fileQueue.GetCountAsync(ct);

                int waitMs = _queueSettings.GetQueueWaitTimeMs();
                await Task.Delay(waitMs, ct);

                string activeQueue = DetermineActiveQueue(folderCount, fileCount);
                _processingState.NotifyQueueCountsChanged(folderCount, fileCount, null, activeQueue);

                if (current == 0)
                {
                    var fp = await _folderQueue.DequeueAsync(ct);
                    if (fp is not null)
                    {
                        await ProcessFolderAsync(fp, ct);
                        continue;
                    }
                }
                if (current == 1)
                {
                    await ProcessFileAsync(ct);
                    continue;
                }
                // else both empty — idle

            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in QueueProcessorService loop");
            }
            finally
            {
                current = (current + 1) % 2; // alternate between 0 and 1 for folder/file priority;
            }

        }
    }

    public async Task ProcessFolderAsync(string folderPath, CancellationToken ct = default)
    {
        _logger.LogDebug("Processing folder: {FolderPath}", folderPath);
        _processingState.NotifyQueueCountsChanged(
            await _folderQueue.GetCountAsync(ct),
            await _fileQueue.GetCountAsync(ct),
            folderPath,
            "Folders");

        try
        {
            if (!Directory.Exists(folderPath))
            {
                _logger.LogWarning("Folder no longer exists, skipping: {FolderPath}", folderPath);
                await _folderQueue.MarkCompleteAsync(folderPath, ct);
                return;
            }

            // Guard against symlink cycles — use a visited set (per-scan in-memory)
            foreach (var subDir in Directory.EnumerateDirectories(folderPath))
            {
                ct.ThrowIfCancellationRequested();
                await _folderQueue.EnqueueAsync(subDir, ct);
            }

            foreach (var file in Directory.EnumerateFiles(folderPath))
            {
                ct.ThrowIfCancellationRequested();
                if (SupportedExtensions.Contains(Path.GetExtension(file)))
                    await _fileQueue.EnqueueAsync(file, ct);
            }

            await _folderQueue.MarkCompleteAsync(folderPath, ct);
            _logger.LogDebug("Folder scan complete: {FolderPath}", folderPath);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning folder: {FolderPath}", folderPath);
            await _folderQueue.MarkFailedAsync(folderPath, CancellationToken.None);
        }
    }

    private async Task ProcessFileAsync(CancellationToken ct)
    {
        var filePath = await _fileQueue.DequeueAsync(ct);
        if (filePath is null) return;

        _logger.LogDebug("Processing file: {FilePath}", filePath);
        _processingState.NotifyQueueCountsChanged(
            await _folderQueue.GetCountAsync(ct),
            await _fileQueue.GetCountAsync(ct),
            filePath,
            "Files");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DamYouDbContext>();
            var taskRepo = scope.ServiceProvider.GetRequiredService<IPipelineTaskRepository>();
            var pipelineProcessor = scope.ServiceProvider.GetRequiredService<IPipelineProcessorService>();

            // Import the file as a Photo if not already indexed
            var photo = await db.Photos.FirstOrDefaultAsync(p => p.FilePath == filePath, ct);

            if (photo is null)
            {
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("File no longer exists, skipping: {FilePath}", filePath);
                    await _fileQueue.MarkCompleteAsync(filePath, CancellationToken.None);
                    return;
                }

                var watchedFolder = await db.WatchedFolders
                    .Where(w => w.IsActive && filePath.StartsWith(w.Path))
                    .FirstOrDefaultAsync(ct);

                if (watchedFolder is null)
                {
                    _logger.LogWarning("No matching WatchedFolder for file, skipping: {FilePath}", filePath);
                    await _fileQueue.MarkCompleteAsync(filePath, CancellationToken.None);
                    return;
                }

                var info = new FileInfo(filePath);
                photo = new Photo
                {
                    WatchedFolderId = watchedFolder.Id,
                    FileName = Path.GetFileName(filePath),
                    FilePath = filePath,
                    FileSizeBytes = info.Length,
                    FileHash = string.Empty, // populated during analysis
                    DateIndexed = DateTime.UtcNow,
                    Status = ProcessingStatus.Unprocessed
                };
                db.Photos.Add(photo);
                await db.SaveChangesAsync(ct);
            }

            // Create a pipeline task if not already queued for this photo
            var alreadyQueued = await db.PipelineTasks.AnyAsync(
                t => t.PhotoId == photo.Id
                  && (t.Status == PipelineTaskStatus.Queued || t.Status == PipelineTaskStatus.Running), ct);

            if (!alreadyQueued && photo.Status == ProcessingStatus.Unprocessed)
                await taskRepo.EnqueueAsync("Process Photo", photo.Id, ct);

            // Run the pipeline (processes all queued PipelineTasks, including the one just added)
            await pipelineProcessor.ProcessQueueAsync(ct: ct);

            // Log completion of analysis pipeline with final step
            var fileName = Path.GetFileName(filePath);
            var completionTime = DateTime.UtcNow;
            var finalStep = "Processing Color Palette";
            await _loggingService.LogStepAsync(finalStep, fileName, completionTime, ct);

            await _fileQueue.MarkCompleteAsync(filePath, CancellationToken.None);
            _logger.LogDebug("File processing complete: {FilePath}", filePath);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file: {FilePath}", filePath);
            await _fileQueue.MarkFailedAsync(filePath, CancellationToken.None);
        }
    }

    private static string DetermineActiveQueue(int folderCount, int fileCount)
    {
        if (folderCount > 0 && fileCount > 0) return "Both";
        if (folderCount > 0) return "Folders";
        if (fileCount > 0) return "Files";
        return "Idle";
    }
}
