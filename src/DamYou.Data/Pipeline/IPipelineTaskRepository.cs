using DamYou.Data.Entities;

namespace DamYou.Data.Pipeline;

public interface IPipelineTaskRepository
{
    Task<PipelineTask> EnqueueAsync(string taskName, int? photoId = null, CancellationToken ct = default);
    Task UpdateStatusAsync(int taskId, PipelineTaskStatus status, string? errorMessage = null, CancellationToken ct = default);
    Task<IReadOnlyList<PipelineTask>> GetActiveTasksAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PipelineTask>> GetQueuedTasksAsync(CancellationToken ct = default);
    Task<int> GetQueueDepthAsync(CancellationToken ct = default);
}
