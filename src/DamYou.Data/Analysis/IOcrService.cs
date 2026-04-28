namespace DamYou.Data.Analysis;

public interface IOcrService
{
    Task<string?> ExtractTextAsync(string imagePath, CancellationToken ct = default);
}
