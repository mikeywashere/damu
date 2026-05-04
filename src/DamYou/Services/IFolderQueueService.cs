using DamYou.Data.Entities;

namespace DamYou.Services;

/// <summary>
/// Queue service for folder paths awaiting recursive scan.
/// DequeueAsync atomically marks items as processing.
/// </summary>
public interface IFolderQueueService : IQueueService<string>
{
    Task MarkCompleteAsync(string folderPath, CancellationToken ct = default);
    Task MarkFailedAsync(string folderPath, CancellationToken ct = default);
    Task<IReadOnlyList<QueuedFolder>> GetActiveItemsAsync(CancellationToken ct = default);
}
