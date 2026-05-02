using DamYou.Data.Entities;
using DamYou.Data.Repositories;
using DamYou.Data.Analysis;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace DamYou.Data.Pipeline;

public sealed class LibraryScanService : ILibraryScanService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif",
        ".heic", ".heif", ".webp", ".raw", ".arw", ".cr2", ".nef",
        ".orf", ".dng", ".rw2", ".pef", ".srw", ".x3f"
    };

    private readonly DamYouDbContext _db;
    private readonly IFolderRepository _folderRepository;
    private readonly IPipelineTaskRepository _taskRepository;
    private readonly IPipelineProcessorService _pipelineProcessor;

    public LibraryScanService(
        DamYouDbContext db,
        IFolderRepository folderRepository,
        IPipelineTaskRepository taskRepository,
        IPipelineProcessorService pipelineProcessor)
    {
        _db = db;
        _folderRepository = folderRepository;
        _taskRepository = taskRepository;
        _pipelineProcessor = pipelineProcessor;
    }

    public async Task ScanAsync(IProgress<ScanProgress>? progress = null, CancellationToken ct = default)
    {
        var scanTask = new PipelineTask
        {
            TaskName = "Scan Library",
            Status = PipelineTaskStatus.Running,
            StartedAt = DateTime.UtcNow,
            TotalItems = 0,
            CurrentItemIndex = 0,
            CurrentItemName = null
        };
        _db.PipelineTasks.Add(scanTask);
        await _db.SaveChangesAsync(ct);

        try
        {
            var folders = await _folderRepository.GetActiveFoldersAsync(ct);

            var totalDiscovered = 0;
            var enqueued = 0;

            foreach (var folder in folders)
            {
                ct.ThrowIfCancellationRequested();

                if (!Directory.Exists(folder.Path)) continue;

                foreach (var filePath in Directory.EnumerateFiles(folder.Path, "*", SearchOption.AllDirectories))
                {
                    if (!SupportedExtensions.Contains(Path.GetExtension(filePath)))
                        continue;

                    ct.ThrowIfCancellationRequested();
                    totalDiscovered++;
                    
                    // Update progress tracking
                    scanTask.TotalItems = totalDiscovered;
                    scanTask.CurrentItemIndex = totalDiscovered;
                    scanTask.CurrentItemName = Path.GetFileName(filePath);
                    _db.PipelineTasks.Update(scanTask);
                    await _db.SaveChangesAsync(ct);
                    
                    progress?.Report(new ScanProgress(totalDiscovered, enqueued, folder.Path));

                    var existing = await _db.Photos
                        .FirstOrDefaultAsync(p => p.FilePath == filePath, ct);

                    if (existing is null)
                    {
                        try
                        {
                            var hash = await ComputeSha256Async(filePath, ct);
                            var existingByHash = await _db.Photos
                                .FirstOrDefaultAsync(p => p.FileHash == hash, ct);

                            if (existingByHash is not null)
                            {
                                // Hash matches - verify byte-by-byte
                                if (await BytesMatchAsync(filePath, existingByHash.FilePath, ct))
                                {
                                    // True duplicate - add to PhotoDuplicate table
                                    var duplicate = new PhotoDuplicate
                                    {
                                        PhotoId = existingByHash.Id,
                                        FilePath = filePath,
                                        FileName = Path.GetFileName(filePath),
                                        DateDiscovered = DateTime.UtcNow
                                    };
                                    _db.PhotoDuplicates.Add(duplicate);
                                    await _db.SaveChangesAsync(ct);
                                    continue;
                                }
                            }

                            // Not a duplicate - create new Photo
                            var info = new FileInfo(filePath);
                            var photo = new Photo
                            {
                                WatchedFolderId = folder.Id,
                                FileName = Path.GetFileName(filePath),
                                FilePath = filePath,
                                FileSizeBytes = info.Length,
                                FileHash = hash,
                                DateIndexed = DateTime.UtcNow,
                                Status = ProcessingStatus.Unprocessed
                            };
                            _db.Photos.Add(photo);
                            await _db.SaveChangesAsync(ct);

                            await _taskRepository.EnqueueAsync("Process Photo", photo.Id, ct);
                            enqueued++;
                        }
                        catch (IOException) { /* skip unreadable files */ }
                        catch (UnauthorizedAccessException) { /* skip inaccessible files */ }
                    }
                    else if (existing.Status == ProcessingStatus.Unprocessed)
                    {
                        var alreadyQueued = await _db.PipelineTasks.AnyAsync(
                            t => t.PhotoId == existing.Id && t.Status == PipelineTaskStatus.Queued, ct);

                        if (!alreadyQueued)
                        {
                            await _taskRepository.EnqueueAsync("Process Photo", existing.Id, ct);
                            enqueued++;
                        }
                    }

                    progress?.Report(new ScanProgress(totalDiscovered, enqueued, folder.Path));
                }
            }

            // After scan completes, trigger pipeline processing for unprocessed photos
            await EnqueuePhotosForAnalysisAsync(ct);

            scanTask.Status = PipelineTaskStatus.Completed;
            scanTask.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            scanTask.Status = PipelineTaskStatus.Failed;
            scanTask.ErrorMessage = ex.Message;
            scanTask.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task EnqueuePhotosForAnalysisAsync(CancellationToken ct = default)
    {
        var unprocessedPhotos = await _db.Photos
            .Where(p => p.Status == ProcessingStatus.Unprocessed)
            .CountAsync(ct);

        if (unprocessedPhotos == 0)
            return;

        await _pipelineProcessor.ProcessQueueAsync(ct: ct);
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
    {
        using var sha = SHA256.Create();
        using var stream = new FileStream(
            filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 81920, useAsync: true);
        var hashBytes = await sha.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static async Task<bool> BytesMatchAsync(string filePath1, string filePath2, CancellationToken ct)
    {
        try
        {
            var info1 = new FileInfo(filePath1);
            var info2 = new FileInfo(filePath2);

            if (info1.Length != info2.Length)
                return false;

            const int bufferSize = 81920;
            using var stream1 = new FileStream(
                filePath1, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize, useAsync: true);
            using var stream2 = new FileStream(
                filePath2, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize, useAsync: true);

            byte[] buffer1 = new byte[bufferSize];
            byte[] buffer2 = new byte[bufferSize];

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                int read1 = await stream1.ReadAsync(buffer1, 0, bufferSize, ct);
                int read2 = await stream2.ReadAsync(buffer2, 0, bufferSize, ct);

                if (read1 != read2)
                    return false;

                if (read1 == 0)
                    return true;

                if (!buffer1.AsSpan(0, read1).SequenceEqual(buffer2.AsSpan(0, read2)))
                    return false;
            }
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
