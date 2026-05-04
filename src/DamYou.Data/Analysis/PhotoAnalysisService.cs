using DamYou.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DamYou.Data.Analysis;

public sealed class PhotoAnalysisService : IPhotoAnalysisService
{
    private readonly DamYouDbContext _db;
    private readonly IClipService _clip;
    private readonly IYoloDetectionService _yolo;
    private readonly IOcrService _ocr;
    private readonly IDistilBertService _distilbert;
    private readonly IColorExtractionService _colors;

    public PhotoAnalysisService(
        DamYouDbContext db,
        IClipService clip,
        IYoloDetectionService yolo,
        IOcrService ocr,
        IDistilBertService distilbert,
        IColorExtractionService colors)
    {
        _db = db; _clip = clip; _yolo = yolo; _ocr = ocr;
        _distilbert = distilbert; _colors = colors;
    }

    public async Task AnalyzePhotoAsync(int photoId, IProgress<AnalysisProgress>? progress = null, CancellationToken ct = default)
    {
        var photo = await _db.Photos.FindAsync([photoId], ct);
        if (photo is null || !File.Exists(photo.FilePath)) return;

        void Report(string pass, string step) =>
            progress?.Report(new AnalysisProgress(1, 0, photo.FileName, pass, step));

        // Pass 1: CLIP embedding
        try
        {
            Report("CLIP", "Processing CLIP embedding");
            var existingEmbed = _db.PhotoEmbeddings
                .FirstOrDefault(e => e.PhotoId == photoId && e.ModelName == _clip.ModelVariant);
            if (existingEmbed is not null) _db.PhotoEmbeddings.Remove(existingEmbed);

            var embedding = await _clip.GetImageEmbeddingAsync(photo.FilePath, ct);
            _db.PhotoEmbeddings.Add(new PhotoEmbedding
            {
                PhotoId    = photoId,
                ModelName  = _clip.ModelVariant,
                Dimensions = _clip.EmbeddingDimensions,
                Embedding  = EmbeddingToBytes(embedding),
            });
        }
        catch (OperationCanceledException) { throw; }
        catch { /* skip if model not ready or image unreadable */ }

        // Pass 2: YOLO object detection
        try
        {
            Report("Object Detection", "Processing Object Detection");
            _db.PhotoDetectedObjects.RemoveRange(
                _db.PhotoDetectedObjects.Where(d => d.PhotoId == photoId));

            var detections = await _yolo.DetectObjectsAsync(photo.FilePath, ct);
            foreach (var d in detections)
            {
                _db.PhotoDetectedObjects.Add(new PhotoDetectedObject
                {
                    PhotoId           = photoId,
                    Label             = d.Label,
                    Confidence        = d.Confidence,
                    BoundingBoxX      = d.X,
                    BoundingBoxY      = d.Y,
                    BoundingBoxWidth  = d.Width,
                    BoundingBoxHeight = d.Height,
                });
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { }

        // Pass 3 + 4: OCR + DistilBERT
        try
        {
            Report("OCR", "Processing OCR extraction");
            var existingOcr = _db.PhotoOcrTexts.FirstOrDefault(o => o.PhotoId == photoId);
            if (existingOcr is not null) _db.PhotoOcrTexts.Remove(existingOcr);

            var ocrText = await _ocr.ExtractTextAsync(photo.FilePath, ct);
            byte[]? textEmbedding = null;
            if (!string.IsNullOrWhiteSpace(ocrText))
            {
                try
                {
                    Report("Text Embedding", "Processing Text Embedding");
                    var textVec = await _distilbert.GetTextEmbeddingAsync(ocrText, ct);
                    textEmbedding = EmbeddingToBytes(textVec);
                }
                catch { }
            }
            _db.PhotoOcrTexts.Add(new PhotoOcrText
            {
                PhotoId       = photoId,
                FullText      = ocrText ?? string.Empty,
                TextEmbedding = textEmbedding,
            });
        }
        catch (OperationCanceledException) { throw; }
        catch { }

        // Pass 5: Color palette
        try
        {
            Report("Color Palette", "Processing Color Palette");
            var existingPalette = _db.PhotoColorPalettes.FirstOrDefault(p => p.PhotoId == photoId);
            if (existingPalette is not null) _db.PhotoColorPalettes.Remove(existingPalette);

            var palette = await _colors.ExtractDominantColorsAsync(photo.FilePath, 5, ct);
            _db.PhotoColorPalettes.Add(new PhotoColorPalette
            {
                PhotoId    = photoId,
                ColorsJson = JsonSerializer.Serialize(palette),
            });
        }
        catch (OperationCanceledException) { throw; }
        catch { }

        photo.Status = ProcessingStatus.Processed;
        await _db.SaveChangesAsync(ct);
    }

    private static byte[] EmbeddingToBytes(float[] embedding)
    {
        var bytes = new byte[embedding.Length * sizeof(float)];
        Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}
