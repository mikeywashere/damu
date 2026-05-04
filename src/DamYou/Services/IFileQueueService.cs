using DamYou.Data.Entities;

namespace DamYou.Services;

/// <summary>
/// Queue service for image file paths awaiting pipeline processing.
/// DequeueAsync atomically marks items as processing.
/// </summary>
public interface IFileQueueService : IQueueService<string>
{
    Task MarkCompleteAsync(string filePath, CancellationToken ct = default);
    Task MarkFailedAsync(string filePath, CancellationToken ct = default);
    Task<IReadOnlyList<QueuedFile>> GetActiveItemsAsync(CancellationToken ct = default);
}
