using DamYou.Data.Entities;
using DamYou.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace DamYou.Data.Import;

public sealed class PhotoImportService : IPhotoImportService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif",
        ".heic", ".heif", ".webp", ".raw", ".arw", ".cr2", ".nef",
        ".orf", ".dng", ".rw2", ".pef", ".srw", ".x3f"
    };

    private const int BatchSize = 100;

    private readonly DamYouDbContext _db;
    private readonly IFolderRepository _folderRepository;

    public PhotoImportService(DamYouDbContext db, IFolderRepository folderRepository)
    {
        _db = db;
        _folderRepository = folderRepository;
    }

    public async Task ImportAsync(IProgress<ImportProgress>? progress = null, CancellationToken ct = default)
    {
        var folders = await _folderRepository.GetActiveFoldersAsync(ct);

        var candidates = new List<(string Path, int FolderId)>();
        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder.Path)) continue;
            foreach (var file in Directory.EnumerateFiles(folder.Path, "*", SearchOption.AllDirectories))
            {
                if (SupportedExtensions.Contains(Path.GetExtension(file)))
                    candidates.Add((file, folder.Id));
            }
        }

        var indexedPaths = await _db.Photos
            .Select(p => p.FilePath)
            .ToListAsync(ct);
        var indexedSet = new HashSet<string>(indexedPaths, StringComparer.OrdinalIgnoreCase);

        var total = candidates.Count;
        var processed = 0;
        var batch = new List<Photo>(BatchSize);

        foreach (var (filePath, folderId) in candidates)
        {
            ct.ThrowIfCancellationRequested();

            if (!indexedSet.Contains(filePath))
            {
                try
                {
                    var hash = await ComputeSha256Async(filePath, ct);
                    var info = new FileInfo(filePath);
                    batch.Add(new Photo
                    {
                        WatchedFolderId = folderId,
                        FileName = Path.GetFileName(filePath),
                        FilePath = filePath,
                        FileSizeBytes = info.Length,
                        FileHash = hash,
                        DateIndexed = DateTime.UtcNow,
                    });

                    if (batch.Count >= BatchSize)
                    {
                        await FlushBatchAsync(batch, ct);
                        batch.Clear();
                    }
                }
                catch (IOException) { /* skip unreadable files */ }
                catch (UnauthorizedAccessException) { /* skip inaccessible files */ }
            }

            processed++;
            progress?.Report(new ImportProgress(total, processed, filePath));
        }

        if (batch.Count > 0)
            await FlushBatchAsync(batch, ct);
    }

    private async Task FlushBatchAsync(List<Photo> batch, CancellationToken ct)
    {
        await _db.Photos.AddRangeAsync(batch, ct);
        await _db.SaveChangesAsync(ct);
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
}
