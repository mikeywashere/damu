using DamYou.Data.Entities;

namespace DamYou.Data.Repositories;

public interface IPhotoRepository
{
    Task<bool> ExistsByPathAsync(string filePath, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    Task AddPhotosAsync(IEnumerable<Photo> photos, CancellationToken ct = default);
    Task<List<Photo>> GetPageAsync(int skip, int take, CancellationToken ct = default);
    Task<List<Photo>> SearchAsync(string searchText, int skip, int take, CancellationToken ct = default);
}
