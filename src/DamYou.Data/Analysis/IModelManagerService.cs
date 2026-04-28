namespace DamYou.Data.Analysis;

public interface IModelManagerService
{
    bool IsModelReady(string modelId);
    string GetModelPath(string modelId);
    string GetModelDirectory(string modelId);
    Task EnsureModelReadyAsync(string modelId, IProgress<ModelDownloadProgress>? progress = null, CancellationToken ct = default);
    IReadOnlyList<string> GetAllModelIds();
}
