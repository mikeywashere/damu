using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;

namespace DamYou.Data.Analysis;

public sealed class WindowsOcrService : IOcrService
{
    public async Task<string?> ExtractTextAsync(string imagePath, CancellationToken ct = default)
    {
        try
        {
            var engine = OcrEngine.TryCreateFromUserProfileLanguages()
                      ?? OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US"));
            if (engine is null) return null;

            var file = await StorageFile.GetFileFromPathAsync(imagePath);
            using var stream = await file.OpenAsync(FileAccessMode.Read);
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var bitmap = await decoder.GetSoftwareBitmapAsync();

            // OCR requires Bgra8 or Gray8
            if (bitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8)
            {
                bitmap = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8);
            }

            var result = await engine.RecognizeAsync(bitmap);
            var text = result.Text?.Trim();
            return string.IsNullOrEmpty(text) ? null : text;
        }
        catch
        {
            return null; // OCR is best-effort; skip on error
        }
    }
}
