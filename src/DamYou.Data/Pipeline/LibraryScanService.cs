using DamYou.Data.Analysis;
using DamYou.Data.Entities;
using DamYou.Data.Repositories;
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

    public async Task ScanAsync(string folderPath, IProgress<ScanProgress>? progress = null, CancellationToken ct = default)
    {
        QueuedFolder folder = new()
        {
            AddedAt = DateTime.UtcNow,
            FolderPath = folderPath,
            Id = Guid.NewGuid().ToString("n"),
            Priority = 0,
            Status = QueueStatus.Pending
        };

        _db.QueuedFolders.Add(folder);
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
