using System.Net.Http;
using System.Net.Http.Headers;

namespace DamYou.Data.Analysis;

public sealed class ModelManagerService : IModelManagerService
{
    private sealed record ModelDescriptor(string ModelId, string Url, string FileName);

    private static readonly IReadOnlyDictionary<string, ModelDescriptor[]> ModelGroups = new Dictionary<string, ModelDescriptor[]>
    {
        ["clip-vit-b32"] = new[]
        {
            new ModelDescriptor("clip-vit-b32", "https://huggingface.co/Xenova/clip-vit-base-patch32/resolve/main/onnx/vision_model.onnx", "vision_model.onnx"),
            new ModelDescriptor("clip-vit-b32", "https://huggingface.co/Xenova/clip-vit-base-patch32/resolve/main/onnx/text_model.onnx", "text_model.onnx"),
            new ModelDescriptor("clip-vit-b32", "https://huggingface.co/Xenova/clip-vit-base-patch32/resolve/main/tokenizer.json", "tokenizer.json"),
            new ModelDescriptor("clip-vit-b32", "https://huggingface.co/Xenova/clip-vit-base-patch32/resolve/main/preprocessor_config.json", "preprocessor_config.json"),
        },
        ["clip-vit-l14"] = new[]
        {
            new ModelDescriptor("clip-vit-l14", "https://huggingface.co/Xenova/clip-vit-large-patch14/resolve/main/onnx/vision_model.onnx", "vision_model.onnx"),
            new ModelDescriptor("clip-vit-l14", "https://huggingface.co/Xenova/clip-vit-large-patch14/resolve/main/onnx/text_model.onnx", "text_model.onnx"),
            new ModelDescriptor("clip-vit-l14", "https://huggingface.co/Xenova/clip-vit-large-patch14/resolve/main/tokenizer.json", "tokenizer.json"),
            new ModelDescriptor("clip-vit-l14", "https://huggingface.co/Xenova/clip-vit-large-patch14/resolve/main/preprocessor_config.json", "preprocessor_config.json"),
        },
        ["yolov8n"] = new[]
        {
            new ModelDescriptor("yolov8n", "https://huggingface.co/msaroufim/yolov8/resolve/main/yolov8n_float32.onnx", "yolov8n.onnx"),
        },
        ["distilbert"] = new[]
        {
            new ModelDescriptor("distilbert", "https://huggingface.co/Xenova/distilbert-base-uncased/resolve/main/onnx/model.onnx", "model.onnx"),
            new ModelDescriptor("distilbert", "https://huggingface.co/Xenova/distilbert-base-uncased/resolve/main/tokenizer.json", "tokenizer.json"),
        },
    };

    private readonly string _modelsRoot;

    public ModelManagerService()
    {
        _modelsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DamYou", "Models");
        Directory.CreateDirectory(_modelsRoot);
    }

    public IReadOnlyList<string> GetAllModelIds() => ModelGroups.Keys.ToList();

    public string GetModelDirectory(string modelId) => Path.Combine(_modelsRoot, modelId);

    public string GetModelPath(string modelId)
    {
        if (ModelGroups.TryGetValue(modelId, out var descriptors))
            return Path.Combine(GetModelDirectory(modelId), descriptors[0].FileName);
        throw new ArgumentException($"Unknown model: {modelId}");
    }

    public bool IsModelReady(string modelId)
    {
        if (!ModelGroups.TryGetValue(modelId, out var descriptors)) return false;
        var dir = GetModelDirectory(modelId);
        return descriptors.All(d => File.Exists(Path.Combine(dir, d.FileName)));
    }

    public async Task EnsureModelReadyAsync(string modelId, IProgress<ModelDownloadProgress>? progress = null, CancellationToken ct = default)
    {
        if (IsModelReady(modelId)) return;
        if (!ModelGroups.TryGetValue(modelId, out var descriptors))
            throw new ArgumentException($"Unknown model: {modelId}");

        var dir = GetModelDirectory(modelId);
        Directory.CreateDirectory(dir);

        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DamYou", "1.0"));

        foreach (var descriptor in descriptors)
        {
            var destPath = Path.Combine(dir, descriptor.FileName);
            if (File.Exists(destPath)) continue;

            var response = await client.GetAsync(descriptor.Url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            var totalBytes = response.Content.Headers.ContentLength ?? 0L;

            var tempPath = destPath + ".tmp";
            try
            {
                using var contentStream = await response.Content.ReadAsStreamAsync(ct);
                using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
                var buffer = new byte[81920];
                long bytesReceived = 0;
                int read;
                while ((read = await contentStream.ReadAsync(buffer, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                    bytesReceived += read;
                    progress?.Report(new ModelDownloadProgress(descriptor.FileName, bytesReceived, totalBytes));
                }
                await fileStream.FlushAsync(ct);
            }
            catch
            {
                try { File.Delete(tempPath); } catch { }
                throw;
            }
            File.Move(tempPath, destPath, overwrite: true);
        }
    }
}
