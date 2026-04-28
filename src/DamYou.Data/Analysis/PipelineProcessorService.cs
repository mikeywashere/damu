using DamYou.Data.Pipeline;

namespace DamYou.Data.Analysis;

public sealed class PipelineProcessorService : IPipelineProcessorService
{
    private readonly DamYouDbContext _db;
    private readonly IPipelineTaskRepository _taskRepo;
    private readonly IPhotoAnalysisService _analysis;

    public PipelineProcessorService(
        DamYouDbContext db,
        IPipelineTaskRepository taskRepo,
        IPhotoAnalysisService analysis)
    {
        _db = db; _taskRepo = taskRepo; _analysis = analysis;
    }

    public async Task<int> GetPendingCountAsync(CancellationToken ct = default)
        => await _taskRepo.GetQueueDepthAsync(ct);

    public async Task ProcessQueueAsync(IProgress<AnalysisProgress>? progress = null, CancellationToken ct = default)
    {
        var queued = await _taskRepo.GetQueuedTasksAsync(ct);
        var photoTasks = queued
            .Where(t => t.TaskName == "Process Photo" && t.PhotoId.HasValue)
            .ToList();

        int total = photoTasks.Count;
        int completed = 0;

        foreach (var task in photoTasks)
        {
            ct.ThrowIfCancellationRequested();
            await _taskRepo.UpdateStatusAsync(task.Id, PipelineTaskStatus.Running, ct: ct);
            try
            {
                var photoProgress = new Progress<AnalysisProgress>(p =>
                    progress?.Report(p with { Total = total, Completed = completed }));

                await _analysis.AnalyzePhotoAsync(task.PhotoId!.Value, photoProgress, ct);
                await _taskRepo.UpdateStatusAsync(task.Id, PipelineTaskStatus.Completed, ct: ct);
                completed++;
            }
            catch (OperationCanceledException)
            {
                await _taskRepo.UpdateStatusAsync(task.Id, PipelineTaskStatus.Queued, ct: CancellationToken.None);
                throw;
            }
            catch (Exception ex)
            {
                await _taskRepo.UpdateStatusAsync(task.Id, PipelineTaskStatus.Failed, ex.Message, CancellationToken.None);
                completed++;
            }
        }
    }
}
