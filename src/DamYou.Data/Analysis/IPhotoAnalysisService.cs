namespace DamYou.Data.Analysis;

public interface IPhotoAnalysisService
{
    Task AnalyzePhotoAsync(int photoId, IProgress<AnalysisProgress>? progress = null, CancellationToken ct = default);
}
