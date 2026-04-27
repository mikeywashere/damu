namespace DamYou.Data.Import;

public interface IPhotoImportService
{
    Task ImportAsync(IProgress<ImportProgress>? progress = null, CancellationToken ct = default);
}
