using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace DamYou.Data.Analysis;

public sealed class DistilBertService : IDistilBertService, IDisposable
{
    private const int MaxSeqLength = 128;
    public int EmbeddingDimensions => 768;

    private readonly IModelManagerService _modelManager;
    private InferenceSession? _session;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public DistilBertService(IModelManagerService modelManager)
    {
        _modelManager = modelManager;
    }

    public bool IsReady => _session is not null;

    public async Task EnsureReadyAsync(IProgress<ModelDownloadProgress>? progress = null, CancellationToken ct = default)
    {
        if (IsReady) return;
        await _initLock.WaitAsync(ct);
        try
        {
            if (IsReady) return;
            await _modelManager.EnsureModelReadyAsync("distilbert", progress, ct);
            var modelPath = Path.Combine(_modelManager.GetModelDirectory("distilbert"), "model.onnx");
            var opts = new SessionOptions();
            try { opts.AppendExecutionProvider_DML(0); }
            catch { opts.Dispose(); opts = new SessionOptions(); opts.AppendExecutionProvider_CPU(); }
            _session = new InferenceSession(modelPath, opts);
        }
        finally { _initLock.Release(); }
    }

    public async Task<float[]> GetTextEmbeddingAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return new float[EmbeddingDimensions];
        await EnsureReadyAsync(ct: ct);
        ct.ThrowIfCancellationRequested();

        var (inputIds, attentionMask) = await Task.Run(() => TokenizeWordPiece(text), ct);

        int seqLen = inputIds.Length;
        var inputIdsTensor      = new DenseTensor<long>([1, seqLen]);
        var attentionMaskTensor = new DenseTensor<long>([1, seqLen]);
        for (int i = 0; i < seqLen; i++)
        {
            inputIdsTensor[0, i]      = inputIds[i];
            attentionMaskTensor[0, i] = attentionMask[i];
        }

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids",      inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
        };

        using var results = _session!.Run(inputs);
        // last_hidden_state: [1, seqLen, 768]
        var hidden = results.First(r => r.Name == "last_hidden_state").AsTensor<float>();

        // Mean pool over non-padding tokens
        var pooled = new float[EmbeddingDimensions];
        int nonPadCount = 0;
        for (int t = 0; t < seqLen; t++)
        {
            if (attentionMask[t] == 0) continue;
            nonPadCount++;
            for (int d = 0; d < EmbeddingDimensions; d++)
                pooled[d] += hidden[0, t, d];
        }
        if (nonPadCount > 0)
            for (int d = 0; d < EmbeddingDimensions; d++)
                pooled[d] /= nonPadCount;

        return Normalize(pooled);
    }

    // Simplified WordPiece tokenizer: CLS=101, SEP=102, UNK=100, PAD=0
    // Maps words to codepoint-based token IDs (approximation; replace with OrtxTokenizer for production)
    private static (long[] inputIds, long[] attentionMask) TokenizeWordPiece(string text)
    {
        const long ClsToken = 101;
        const long SepToken = 102;
        const long UNK = 100;

        var ids = new List<long> { ClsToken };
        foreach (var word in text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var c in word.Take(MaxSeqLength - 3))
                ids.Add(c < 30522 ? (long)c : UNK);
            if (ids.Count >= MaxSeqLength - 1) break;
        }
        ids.Add(SepToken);

        int seqLen = Math.Min(ids.Count, MaxSeqLength);
        var inputIds      = new long[seqLen];
        var attentionMask = new long[seqLen];
        for (int i = 0; i < seqLen; i++)
        {
            inputIds[i]      = ids[i];
            attentionMask[i] = 1;
        }
        return (inputIds, attentionMask);
    }

    private static float[] Normalize(float[] vec)
    {
        var norm = MathF.Sqrt(vec.Sum(x => x * x));
        if (norm < 1e-8f) return vec;
        return vec.Select(x => x / norm).ToArray();
    }

    public void Dispose() { _session?.Dispose(); _initLock.Dispose(); }
}
