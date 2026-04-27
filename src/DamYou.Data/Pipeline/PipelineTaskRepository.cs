using DamYou.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DamYou.Data.Pipeline;

public sealed class PipelineTaskRepository : IPipelineTaskRepository
{
    private readonly DamYouDbContext _db;

    public PipelineTaskRepository(DamYouDbContext db)
    {
        _db = db;
    }

    public async Task<PipelineTask> EnqueueAsync(string taskName, int? photoId = null, CancellationToken ct = default)
    {
        var task = new PipelineTask
        {
            TaskName = taskName,
            Status = PipelineTaskStatus.Queued,
            PhotoId = photoId
        };
        _db.PipelineTasks.Add(task);
        await _db.SaveChangesAsync(ct);
        return task;
    }

    public async Task UpdateStatusAsync(int taskId, PipelineTaskStatus status, string? errorMessage = null, CancellationToken ct = default)
    {
        var task = await _db.PipelineTasks.FindAsync([taskId], ct)
            ?? throw new InvalidOperationException($"PipelineTask {taskId} not found.");

        task.Status = status;
        task.ErrorMessage = errorMessage;

        if (status == PipelineTaskStatus.Running)
            task.StartedAt = DateTime.UtcNow;
        else if (status is PipelineTaskStatus.Completed or PipelineTaskStatus.Failed)
            task.CompletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PipelineTask>> GetActiveTasksAsync(CancellationToken ct = default)
    {
        return await _db.PipelineTasks
            .Where(t => t.Status == PipelineTaskStatus.Queued || t.Status == PipelineTaskStatus.Running)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PipelineTask>> GetQueuedTasksAsync(CancellationToken ct = default)
    {
        return await _db.PipelineTasks
            .Where(t => t.Status == PipelineTaskStatus.Queued)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<int> GetQueueDepthAsync(CancellationToken ct = default)
    {
        return await _db.PipelineTasks
            .CountAsync(t => t.Status == PipelineTaskStatus.Queued, ct);
    }
}
