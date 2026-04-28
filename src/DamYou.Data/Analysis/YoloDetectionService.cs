using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

namespace DamYou.Data.Analysis;

[SupportedOSPlatform("windows")]
public sealed class YoloDetectionService : IYoloDetectionService, IDisposable
{
    private const int InputSize = 640;
    private const float ConfidenceThreshold = 0.25f;
    private const float IouThreshold = 0.45f;

    // COCO 80 class labels
    private static readonly string[] CocoLabels =
    [
        "person","bicycle","car","motorcycle","airplane","bus","train","truck","boat","traffic light",
        "fire hydrant","stop sign","parking meter","bench","bird","cat","dog","horse","sheep","cow",
        "elephant","bear","zebra","giraffe","backpack","umbrella","handbag","tie","suitcase","frisbee",
        "skis","snowboard","sports ball","kite","baseball bat","baseball glove","skateboard","surfboard",
        "tennis racket","bottle","wine glass","cup","fork","knife","spoon","bowl","banana","apple",
        "sandwich","orange","broccoli","carrot","hot dog","pizza","donut","cake","chair","couch",
        "potted plant","bed","dining table","toilet","tv","laptop","mouse","remote","keyboard",
        "cell phone","microwave","oven","toaster","sink","refrigerator","book","clock","vase",
        "scissors","teddy bear","hair drier","toothbrush"
    ];

    private readonly IModelManagerService _modelManager;
    private InferenceSession? _session;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public YoloDetectionService(IModelManagerService modelManager)
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
            await _modelManager.EnsureModelReadyAsync("yolov8n", progress, ct);
            var modelPath = Path.Combine(_modelManager.GetModelDirectory("yolov8n"), "yolov8n.onnx");
            var opts = new SessionOptions();
            try { opts.AppendExecutionProvider_DML(0); }
            catch { opts.Dispose(); opts = new SessionOptions(); opts.AppendExecutionProvider_CPU(); }
            _session = new InferenceSession(modelPath, opts);
        }
        finally { _initLock.Release(); }
    }

    public async Task<IReadOnlyList<DetectedObject>> DetectObjectsAsync(string imagePath, CancellationToken ct = default)
    {
        await EnsureReadyAsync(ct: ct);
        ct.ThrowIfCancellationRequested();

        var (tensor, scaleX, scaleY) = await Task.Run(() => PreprocessImageWithScale(imagePath), ct);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("images", tensor)
        };

        using var results = _session!.Run(inputs);
        var output = results[0].AsTensor<float>();

        return PostProcess(output, scaleX, scaleY);
    }

    private static (DenseTensor<float> tensor, float scaleX, float scaleY) PreprocessImageWithScale(string imagePath)
    {
        float scaleX, scaleY;
        var tensor = PreprocessImage(imagePath, out scaleX, out scaleY);
        return (tensor, scaleX, scaleY);
    }

    private static DenseTensor<float> PreprocessImage(string imagePath, out float scaleX, out float scaleY)
    {
        using var original = Image.FromFile(imagePath);
        scaleX = (float)original.Width  / InputSize;
        scaleY = (float)original.Height / InputSize;

        using var resized = new Bitmap(InputSize, InputSize, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(resized);
        g.Clear(Color.Gray);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        // Letterbox: fit maintaining aspect ratio
        float scale = Math.Min((float)InputSize / original.Width, (float)InputSize / original.Height);
        int w = (int)(original.Width  * scale);
        int h = (int)(original.Height * scale);
        int offsetX = (InputSize - w) / 2;
        int offsetY = (InputSize - h) / 2;
        g.DrawImage(original, offsetX, offsetY, w, h);

        var tensor = new DenseTensor<float>([1, 3, InputSize, InputSize]);
        for (int y = 0; y < InputSize; y++)
        for (int x = 0; x < InputSize; x++)
        {
            var px = resized.GetPixel(x, y);
            tensor[0, 0, y, x] = px.R / 255.0f;
            tensor[0, 1, y, x] = px.G / 255.0f;
            tensor[0, 2, y, x] = px.B / 255.0f;
        }
        return tensor;
    }

    // YOLOv8 output: [1, 84, 8400] => transpose to [8400, 84]
    // First 4 = cx,cy,w,h (normalized to 640); next 80 = class confidences
    private static IReadOnlyList<DetectedObject> PostProcess(Tensor<float> output, float scaleX, float scaleY)
    {
        int numBoxes = 8400;
        int numClasses = 80;
        var candidates = new List<(float cx, float cy, float w, float h, int cls, float conf)>();

        for (int i = 0; i < numBoxes; i++)
        {
            float maxConf = 0;
            int bestCls = 0;
            for (int c = 0; c < numClasses; c++)
            {
                float conf = output[0, 4 + c, i];
                if (conf > maxConf) { maxConf = conf; bestCls = c; }
            }
            if (maxConf < ConfidenceThreshold) continue;
            candidates.Add((output[0, 0, i], output[0, 1, i], output[0, 2, i], output[0, 3, i], bestCls, maxConf));
        }

        // Simple NMS per class
        var results = new List<DetectedObject>();
        foreach (var group in candidates.GroupBy(c => c.cls))
        {
            var sorted = group.OrderByDescending(c => c.conf).ToList();
            var suppressed = new HashSet<int>();
            for (int i = 0; i < sorted.Count; i++)
            {
                if (suppressed.Contains(i)) continue;
                var (cx, cy, w, h, cls, conf) = sorted[i];
                results.Add(new DetectedObject(
                    CocoLabels[cls], conf,
                    (cx - w / 2) / InputSize, (cy - h / 2) / InputSize,
                    w / InputSize, h / InputSize));
                for (int j = i + 1; j < sorted.Count; j++)
                    if (!suppressed.Contains(j) && Iou(sorted[i], sorted[j]) > IouThreshold)
                        suppressed.Add(j);
            }
        }
        return results;
    }

    private static float Iou(
        (float cx, float cy, float w, float h, int cls, float conf) a,
        (float cx, float cy, float w, float h, int cls, float conf) b)
    {
        float ax1 = a.cx - a.w / 2, ay1 = a.cy - a.h / 2, ax2 = a.cx + a.w / 2, ay2 = a.cy + a.h / 2;
        float bx1 = b.cx - b.w / 2, by1 = b.cy - b.h / 2, bx2 = b.cx + b.w / 2, by2 = b.cy + b.h / 2;
        float ix1 = MathF.Max(ax1, bx1), iy1 = MathF.Max(ay1, by1);
        float ix2 = MathF.Min(ax2, bx2), iy2 = MathF.Min(ay2, by2);
        float intersection = MathF.Max(0, ix2 - ix1) * MathF.Max(0, iy2 - iy1);
        float areaA = (ax2 - ax1) * (ay2 - ay1);
        float areaB = (bx2 - bx1) * (by2 - by1);
        float union = areaA + areaB - intersection;
        return union <= 0 ? 0 : intersection / union;
    }

    public void Dispose() { _session?.Dispose(); _initLock.Dispose(); }
}
