namespace DamYou.Data.Analysis;

public interface IClipService
{
    string ModelVariant { get; }    // "clip-vit-b32" or "clip-vit-l14"
    int EmbeddingDimensions { get; } // 512 for B/32, 768 for L/14
    Task<float[]> GetImageEmbeddingAsync(string imagePath, CancellationToken ct = default);
    Task<float[]> GetTextEmbeddingAsync(string text, CancellationToken ct = default);
    bool IsReady { get; }
    Task EnsureReadyAsync(IProgress<ModelDownloadProgress>? progress = null, CancellationToken ct = default);
}
