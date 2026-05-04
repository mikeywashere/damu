using DamYou.Data;
using DamYou.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DamYou.Services;

/// <summary>
/// Persistent folder queue backed by the QueuedFolders SQLite table.
/// DequeueAsync atomically marks the item as Processing to prevent double-processing on restart.
/// Thread safety: each operation creates its own scope+DbContext. The QueueProcessorService
/// runs single-threaded, but the guard of marking 'Processing' first means concurrent
/// callers would simply skip already-claimed items.
/// </summary>
public sealed class FolderQueueService : IFolderQueueService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FolderQueueService> _logger;

    public FolderQueueService(IServiceScopeFactory scopeFactory, ILogger<FolderQueueService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task EnqueueAsync(string folderPath, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DamYouDbContext>();

        var existing = await db.QueuedFolders
            .FirstOrDefaultAsync(f => f.FolderPath == folderPath, ct);

        if (existing is not null)
        {
            // Re-queue a previously completed/failed folder so it can be retried
            if (existing.Status is QueueStatus.Completed or QueueStatus.Failed)
            {
                existing.Status = QueueStatus.Pending;
                existing.AddedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
            // Already pending or processing — no-op (idempotent)
            return;
        }

        db.QueuedFolders.Add(new QueuedFolder
        {
            FolderPath = folderPath,
            AddedAt = DateTime.UtcNow,
            Status = QueueStatus.Pending
        });
        await db.SaveChangesAsync(ct);
        _logger.LogDebug("Enqueued folder: {FolderPath}", folderPath);
    }

    /// <summary>
    /// Dequeues the highest-priority pending folder and marks it Processing atomically.
    /// Items already stuck in Processing (crash survivors) are skipped.
    /// Returns null if the queue is empty.
    /// </summary>
    public async Task<string?> DequeueAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DamYouDbContext>();

        var item = await db.QueuedFolders
            .Where(f => f.Status == QueueStatus.Pending)
            .OrderByDescending(f => f.Priority)
            .ThenBy(f => f.AddedAt)
            .FirstOrDefaultAsync(ct);

        if (item is null)
            return null;

        item.Status = QueueStatus.Processing;
        await db.SaveChangesAsync(ct);
        return item.FolderPath;
    }

    public async Task<string?> PeekAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DamYouDbContext>();

        return await db.QueuedFolders
            .Where(f => f.Status == QueueStatus.Pending)
            .OrderByDescending(f => f.Priority)
            .ThenBy(f => f.AddedAt)
            .Select(f => f.FolderPath)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<int> GetCountAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DamYouDbContext>();

        return await db.QueuedFolders
            .CountAsync(f => f.Status == QueueStatus.Pending, ct);
    }

    public async Task MarkCompleteAsync(string folderPath, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DamYouDbContext>();

        var item = await db.QueuedFolders
            .FirstOrDefaultAsync(f => f.FolderPath == folderPath, ct);

        if (item is null) return;
        item.Status = QueueStatus.Completed;
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkFailedAsync(string folderPath, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DamYouDbContext>();

        var item = await db.QueuedFolders
            .FirstOrDefaultAsync(f => f.FolderPath == folderPath, ct);

        if (item is null) return;
        item.Status = QueueStatus.Failed;
        await db.SaveChangesAsync(ct);
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DamYouDbContext>();

        await db.QueuedFolders
            .Where(f => f.Status == QueueStatus.Pending)
            .ExecuteDeleteAsync(ct);
    }
}
