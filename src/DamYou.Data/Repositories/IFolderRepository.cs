using DamYou.Data.Entities;

namespace DamYou.Data.Repositories;

public interface IFolderRepository
{
    Task<IReadOnlyList<WatchedFolder>> GetActiveFoldersAsync(CancellationToken ct = default);
    Task AddFoldersAsync(IEnumerable<string> paths, CancellationToken ct = default);
    Task DeactivateFolderAsync(int id, CancellationToken ct = default);
}
