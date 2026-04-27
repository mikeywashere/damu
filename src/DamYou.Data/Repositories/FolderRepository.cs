using DamYou.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DamYou.Data.Repositories;

public sealed class FolderRepository : IFolderRepository
{
    private readonly DamYouDbContext _db;

    public FolderRepository(DamYouDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<WatchedFolder>> GetActiveFoldersAsync(CancellationToken ct = default)
    {
        return await _db.WatchedFolders
            .Where(f => f.IsActive)
            .OrderBy(f => f.DateAdded)
            .ToListAsync(ct);
    }

    public async Task AddFoldersAsync(IEnumerable<string> paths, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var existingList = await _db.WatchedFolders
            .Select(f => f.Path)
            .ToListAsync(ct);
        var existing = existingList.ToHashSet();

        var toAdd = paths
            .Where(p => !existing.Contains(p))
            .Select(p => new WatchedFolder
            {
                Path = p,
                DateAdded = now,
                IsActive = true
            });

        await _db.WatchedFolders.AddRangeAsync(toAdd, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeactivateFolderAsync(int id, CancellationToken ct = default)
    {
        var folder = await _db.WatchedFolders.FindAsync([id], ct);
        if (folder is not null)
        {
            folder.IsActive = false;
            await _db.SaveChangesAsync(ct);
        }
    }
}
