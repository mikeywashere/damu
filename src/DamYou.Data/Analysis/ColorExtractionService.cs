using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

namespace DamYou.Data.Analysis;

[SupportedOSPlatform("windows")]
public sealed class ColorExtractionService : IColorExtractionService
{
    public async Task<IReadOnlyList<string>> ExtractDominantColorsAsync(
        string imagePath, int count = 5, CancellationToken ct = default)
    {
        return await Task.Run(() => ExtractColors(imagePath, count), ct);
    }

    private static IReadOnlyList<string> ExtractColors(string imagePath, int count)
    {
        try
        {
            using var original = Image.FromFile(imagePath);
            // Downsample to 50x50 for speed
            using var small = new Bitmap(50, 50, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(small))
                g.DrawImage(original, 0, 0, 50, 50);

            var pixels = new List<(int R, int G, int B)>(2500);
            for (int y = 0; y < 50; y++)
            for (int x = 0; x < 50; x++)
            {
                var px = small.GetPixel(x, y);
                pixels.Add((px.R, px.G, px.B));
            }

            var colors = KMeans(pixels, count);
            return colors.Select(c => $"#{c.R:X2}{c.G:X2}{c.B:X2}").ToList();
        }
        catch
        {
            return [];
        }
    }

    private static List<(int R, int G, int B)> KMeans(List<(int R, int G, int B)> pixels, int k)
    {
        // Initialize centroids by spreading across sorted luminance
        var sorted = pixels.OrderBy(p => 0.299 * p.R + 0.587 * p.G + 0.114 * p.B).ToList();
        var centroids = Enumerable.Range(0, k)
            .Select(i => sorted[i * sorted.Count / k])
            .ToList();

        for (int iter = 0; iter < 10; iter++)
        {
            var clusters = Enumerable.Range(0, k).Select(_ => new List<(int R, int G, int B)>()).ToList();
            foreach (var p in pixels)
            {
                int nearest = 0;
                double minDist = double.MaxValue;
                for (int i = 0; i < k; i++)
                {
                    double dist = Math.Pow(p.R - centroids[i].R, 2)
                                + Math.Pow(p.G - centroids[i].G, 2)
                                + Math.Pow(p.B - centroids[i].B, 2);
                    if (dist < minDist) { minDist = dist; nearest = i; }
                }
                clusters[nearest].Add(p);
            }

            bool changed = false;
            for (int i = 0; i < k; i++)
            {
                if (clusters[i].Count == 0) continue;
                var newC = (
                    R: (int)clusters[i].Average(p => p.R),
                    G: (int)clusters[i].Average(p => p.G),
                    B: (int)clusters[i].Average(p => p.B));
                if (newC != centroids[i]) { centroids[i] = newC; changed = true; }
            }
            if (!changed) break;
        }

        return centroids;
    }
}
