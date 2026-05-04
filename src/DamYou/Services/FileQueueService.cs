using DamYou.Data;
using DamYou.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DamYou.Services;

/// <summary>
/// Persistent file queue backed by the QueuedFiles SQLite table.
/// DequeueAsync atomically marks the item as Processing to prevent double-processing.
/// Idempotent enqueue: re-enqueueing a completed/failed file resets it; duplicate pending enqueues are no-ops.
/// </summary>
public sealed class FileQueueService : IFileQueueService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FileQueueService> _logger;

    public FileQueueService(IServiceScopeFactory scopeFactory, ILogger<FileQueueService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task EnqueueAsync(string filePath, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DamYouDbContext>();

        var existing = await db.QueuedFiles
            .FirstOrDefaultAsync(f => f.FilePath == filePath, ct);

        if (existing is not null)
        {
            if (existing.Status is QueueStatus.Completed or QueueStatus.Failed)
            {
                existing.Status = QueueStatus.Pending;
                existing.AddedAt = DateTime.UtcNow;
                existing.ProcessingStartedAt = null;
                existing.CompletedAt = null;
                await db.SaveChangesAsync(ct);
            }
            return;
        }

        db.QueuedFiles.Add(new QueuedFile
        {
            FilePath = filePath,
            AddedAt = DateTime.UtcNow,
            Status = QueueStatus.Pending
        });
        await db.SaveChangesAsync(ct);
        _logger.LogDebug("Enqueued file: {FilePath}", filePath);
    }

    /// <summary>
    /// Dequeues the oldest pending file and marks it Processing atomically.
    /// Returns null if the queue is empty.
    /// </summary>
    public async Task<string?> DequeueAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DamYouDbContext>();

        var item = await db.QueuedFiles
            .Where(f => f.Status == QueueStatus.Pending)
            .OrderBy(f => f.AddedAt)
            .FirstOrDefaultAsync(ct);

        if (item is null)
            return null;

        item.Status = QueueStatus.Processing;
        item.ProcessingStartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return item.FilePath;
    }

    public async Task<string?> PeekAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DamYouDbContext>();

        return await db.QueuedFiles
            .Where(f => f.Status == QueueStatus.Pending)
            .OrderBy(f => f.AddedAt)
            .Select(f => f.FilePath)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<int> GetCountAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DamYouDbContext>();

        return await db.QueuedFiles
            .CountAsync(f => f.Status == QueueStatus.Pending, ct);
    }

    public async Task MarkCompleteAsync(string filePath, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DamYouDbContext>();

        var item = await db.QueuedFiles
            .FirstOrDefaultAsync(f => f.FilePath == filePath, ct);

        if (item is null) return;
        item.Status = QueueStatus.Completed;
        item.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkFailedAsync(string filePath, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DamYouDbContext>();

        var item = await db.QueuedFiles
            .FirstOrDefaultAsync(f => f.FilePath == filePath, ct);

        if (item is null) return;
        item.Status = QueueStatus.Failed;
        item.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DamYouDbContext>();

        await db.QueuedFiles
            .Where(f => f.Status == QueueStatus.Pending)
            .ExecuteDeleteAsync(ct);
    }
}
