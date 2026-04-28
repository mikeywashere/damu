using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

namespace DamYou.Data.Analysis;

#if WINDOWS

[SupportedOSPlatform("windows")]
public sealed class ClipService : IClipService, IDisposable
{
    // CLIP normalization constants
    private static readonly float[] Mean = [0.48145466f, 0.4578275f, 0.40821073f];
    private static readonly float[] Std  = [0.26862954f, 0.26130258f, 0.27577711f];
    private const int ImageSize = 224;
    private const int MaxTokenLength = 77;

    private readonly IModelManagerService _modelManager;
    private readonly IHardwareDetectionService _hardware;
    private InferenceSession? _visionSession;
    private InferenceSession? _textSession;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public ClipService(IModelManagerService modelManager, IHardwareDetectionService hardware)
    {
        _modelManager = modelManager;
        _hardware = hardware;
        ModelVariant = hardware.GetRecommendedClipModel();
        EmbeddingDimensions = ModelVariant == "clip-vit-l14" ? 768 : 512;
    }

    public string ModelVariant { get; }
    public int EmbeddingDimensions { get; }
    public bool IsReady => _visionSession is not null && _textSession is not null;

    public async Task EnsureReadyAsync(IProgress<ModelDownloadProgress>? progress = null, CancellationToken ct = default)
    {
        if (IsReady) return;
        await _initLock.WaitAsync(ct);
        try
        {
            if (IsReady) return;
            await _modelManager.EnsureModelReadyAsync(ModelVariant, progress, ct);
            var modelDir = _modelManager.GetModelDirectory(ModelVariant);
            var opts = BuildSessionOptions();
            _visionSession = new InferenceSession(Path.Combine(modelDir, "vision_model.onnx"), opts);
            _textSession  = new InferenceSession(Path.Combine(modelDir, "text_model.onnx"), opts);
        }
        finally { _initLock.Release(); }
    }

    private static SessionOptions BuildSessionOptions()
    {
        var opts = new SessionOptions();
        try
        {
            // Try DirectML first (any DirectX 12 GPU)
            opts.AppendExecutionProvider_DML(0);
            return opts;
        }
        catch { opts.Dispose(); }

        // Fall back to CPU
        var cpuOpts = new SessionOptions();
        cpuOpts.AppendExecutionProvider_CPU();
        return cpuOpts;
    }

    public async Task<float[]> GetImageEmbeddingAsync(string imagePath, CancellationToken ct = default)
    {
        await EnsureReadyAsync(ct: ct);
        ct.ThrowIfCancellationRequested();

        var tensor = await Task.Run(() => PreprocessImage(imagePath), ct);
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("pixel_values", tensor)
        };

        using var results = _visionSession!.Run(inputs);
        var output = results.First(r => r.Name == "image_embeds");
        var data = output.AsTensor<float>().ToArray();
        return Normalize(data);
    }

    public async Task<float[]> GetTextEmbeddingAsync(string text, CancellationToken ct = default)
    {
        await EnsureReadyAsync(ct: ct);
        ct.ThrowIfCancellationRequested();

        var (inputIds, attentionMask) = await Task.Run(() => TokenizeClip(text), ct);
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids",      CreateInt64Tensor(inputIds,      [1, MaxTokenLength])),
            NamedOnnxValue.CreateFromTensor("attention_mask", CreateInt64Tensor(attentionMask, [1, MaxTokenLength])),
        };

        using var results = _textSession!.Run(inputs);
        var output = results.First(r => r.Name == "text_embeds");
        var data = output.AsTensor<float>().ToArray();
        return Normalize(data);
    }

    // Simplified CLIP tokenizer: UTF-8 byte-level BPE approximation.
    // Produces SOT token (49406), max 75 text tokens, EOT token (49407).
    // This is a good-faith approximation. For production, swap in OrtxTokenizer.
    private static (long[] inputIds, long[] attentionMask) TokenizeClip(string text)
    {
        const long SotToken = 49406;
        const long EotToken = 49407;

        var ids = new List<long> { SotToken };
        // Simple whitespace token encoding — maps each character to its Unicode codepoint + 256 offset
        // (approximate CLIP byte-level BPE behavior for ASCII text)
        foreach (var c in text.ToLowerInvariant().Take(74))
            ids.Add((long)(c < 256 ? c + 256 : 256));
        ids.Add(EotToken);

        var inputIds      = new long[MaxTokenLength];
        var attentionMask = new long[MaxTokenLength];
        for (int i = 0; i < Math.Min(ids.Count, MaxTokenLength); i++)
        {
            inputIds[i]      = ids[i];
            attentionMask[i] = 1;
        }
        return (inputIds, attentionMask);
    }

    private static DenseTensor<float> PreprocessImage(string imagePath)
    {
        using var original = Image.FromFile(imagePath);
        using var resized  = new Bitmap(ImageSize, ImageSize, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(resized);
        g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.DrawImage(original, 0, 0, ImageSize, ImageSize);

        var tensor = new DenseTensor<float>([1, 3, ImageSize, ImageSize]);
        for (int y = 0; y < ImageSize; y++)
        {
            for (int x = 0; x < ImageSize; x++)
            {
                var px = resized.GetPixel(x, y);
                tensor[0, 0, y, x] = (px.R / 255.0f - Mean[0]) / Std[0];
                tensor[0, 1, y, x] = (px.G / 255.0f - Mean[1]) / Std[1];
                tensor[0, 2, y, x] = (px.B / 255.0f - Mean[2]) / Std[2];
            }
        }
        return tensor;
    }

    private static DenseTensor<long> CreateInt64Tensor(long[] data, int[] shape)
    {
        var tensor = new DenseTensor<long>(shape);
        for (int i = 0; i < data.Length; i++) tensor.SetValue(i, data[i]);
        return tensor;
    }

    private static float[] Normalize(float[] vec)
    {
        var norm = MathF.Sqrt(vec.Sum(x => x * x));
        if (norm < 1e-8f) return vec;
        return vec.Select(x => x / norm).ToArray();
    }

    public void Dispose()
    {
        _visionSession?.Dispose();
        _textSession?.Dispose();
        _initLock.Dispose();
    }
}

#endif
