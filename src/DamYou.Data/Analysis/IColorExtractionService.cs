namespace DamYou.Data.Analysis;

public interface IColorExtractionService
{
    Task<IReadOnlyList<string>> ExtractDominantColorsAsync(string imagePath, int count = 5, CancellationToken ct = default);
}
