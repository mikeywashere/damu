namespace DamYou.Data.Analysis;

public interface IPipelineProcessorService
{
    Task ProcessQueueAsync(IProgress<AnalysisProgress>? progress = null, CancellationToken ct = default);
    Task<int> GetPendingCountAsync(CancellationToken ct = default);
}
