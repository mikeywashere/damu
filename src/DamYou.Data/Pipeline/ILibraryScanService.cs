namespace DamYou.Data.Pipeline;

public interface ILibraryScanService
{
    Task ScanAsync(string folderName, IProgress<ScanProgress>? progress = null, CancellationToken ct = default);
}
