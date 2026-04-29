using DamYou.Data.Entities;

namespace DamYou.Data.Repositories;

public interface IPhotoFolderRepository
{
    Task<PhotoFolder> AddFolderAsync(string folderPath, CancellationToken ct = default);
    Task RemoveFolderAsync(int folderId, CancellationToken ct = default);
    Task<List<PhotoFolder>> GetAllFoldersAsync(CancellationToken ct = default);
    Task<PhotoFolder?> GetFolderByPathAsync(string path, CancellationToken ct = default);
    Task<bool> ExistsByPathAsync(string path, CancellationToken ct = default);
}
