using DamYou.Data.Entities;

namespace DamYou.Data.Repositories;

public interface IPhotoRepository
{
    Task<bool> ExistsByPathAsync(string filePath, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
    Task<int> CountByFolderAsync(int folderId, CancellationToken ct = default);
    /// <summary>
    /// Adds photos to the database. 
    /// INTEGRATION POINT: Lambert can extend this to accept photoFolderId per photo if needed.
    /// </summary>
    Task AddPhotosAsync(IEnumerable<Photo> photos, CancellationToken ct = default);
    Task<List<Photo>> GetPageAsync(int skip, int take, CancellationToken ct = default);
    Task<List<Photo>> SearchAsync(string searchText, int skip, int take, CancellationToken ct = default);
    Task<List<string>> GetDuplicatePathsAsync(int photoId, CancellationToken ct = default);
}
