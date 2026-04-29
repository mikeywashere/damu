using DamYou.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DamYou.Data.Repositories;

public sealed class PhotoFolderRepository : IPhotoFolderRepository
{
    private readonly DamYouDbContext _db;

    public PhotoFolderRepository(DamYouDbContext db) => _db = db;

    public async Task<PhotoFolder> AddFolderAsync(string folderPath, CancellationToken ct = default)
    {
        var folder = new PhotoFolder
        {
            FolderPath = folderPath,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.PhotoFolders.Add(folder);
        await _db.SaveChangesAsync(ct);
        return folder;
    }

    public async Task RemoveFolderAsync(int folderId, CancellationToken ct = default)
    {
        var folder = await _db.PhotoFolders.FindAsync([folderId], ct);
        if (folder is null) return;

        _db.PhotoFolders.Remove(folder);
        await _db.SaveChangesAsync(ct);
    }

    public Task<List<PhotoFolder>> GetAllFoldersAsync(CancellationToken ct = default) =>
        _db.PhotoFolders
            .OrderBy(x => x.CreatedAt)
            .AsNoTracking()
            .ToListAsync(ct);

    public Task<PhotoFolder?> GetFolderByPathAsync(string path, CancellationToken ct = default) =>
        _db.PhotoFolders
            .FirstOrDefaultAsync(x => x.FolderPath == path, ct);

    public Task<bool> ExistsByPathAsync(string path, CancellationToken ct = default) =>
        _db.PhotoFolders.AnyAsync(x => x.FolderPath == path, ct);
}
