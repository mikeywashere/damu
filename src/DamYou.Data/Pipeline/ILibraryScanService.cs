using DamYou.Data.Entities;

namespace DamYou.Data.Pipeline;

public interface ILibraryScanService
{
    Task ScanAsync(IProgress<ScanProgress>? progress = null, CancellationToken ct = default);
    Task EnqueuePhotosForAnalysisAsync(CancellationToken ct = default);
}
