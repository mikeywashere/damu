namespace DamYou.Data.Analysis;

public interface IDistilBertService
{
    int EmbeddingDimensions { get; }  // 768
    bool IsReady { get; }
    Task EnsureReadyAsync(IProgress<ModelDownloadProgress>? progress = null, CancellationToken ct = default);
    Task<float[]> GetTextEmbeddingAsync(string text, CancellationToken ct = default);
}
