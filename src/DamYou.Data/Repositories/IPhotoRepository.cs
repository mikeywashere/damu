using DamYou.Data.Entities;

namespace DamYou.Data.Repositories;

public interface IPhotoRepository
{
    Task<bool> ExistsByPathAsync(string filePath, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    Task AddPhotosAsync(IEnumerable<Photo> photos, CancellationToken ct = default);
}
