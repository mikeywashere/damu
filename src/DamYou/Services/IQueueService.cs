namespace DamYou.Services;

/// <summary>
/// Generic persistent queue abstraction backed by SQLite.
/// T is the item being queued (e.g., file path string, folder path string).
/// </summary>
public interface IQueueService<T>
{
    Task EnqueueAsync(T item, CancellationToken ct = default);
    Task<T?> DequeueAsync(CancellationToken ct = default);
    Task<T?> PeekAsync(CancellationToken ct = default);
    Task<int> GetCountAsync(CancellationToken ct = default);
    Task ClearAsync(CancellationToken ct = default);
}
