namespace DamYou.Data.Pipeline;

public interface ILibraryScanService
{
    Task ScanAsync(IProgress<ScanProgress>? progress = null, CancellationToken ct = default);
}
