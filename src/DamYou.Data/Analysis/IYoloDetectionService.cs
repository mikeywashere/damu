namespace DamYou.Data.Analysis;

public interface IYoloDetectionService
{
    bool IsReady { get; }
    Task EnsureReadyAsync(IProgress<ModelDownloadProgress>? progress = null, CancellationToken ct = default);
    Task<IReadOnlyList<DetectedObject>> DetectObjectsAsync(string imagePath, CancellationToken ct = default);
}
