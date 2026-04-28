using DamYou.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DamYou.Data.Repositories;

public sealed class PhotoRepository : IPhotoRepository
{
    private readonly DamYouDbContext _db;

    public PhotoRepository(DamYouDbContext db) => _db = db;

    public Task<bool> ExistsByPathAsync(string filePath, CancellationToken ct = default) =>
        _db.Photos.AnyAsync(p => p.FilePath == filePath, ct);

    public Task<int> CountAsync(CancellationToken ct = default) =>
        _db.Photos.CountAsync(ct);

    public async Task AddPhotosAsync(IEnumerable<Photo> photos, CancellationToken ct = default)
    {
        await _db.Photos.AddRangeAsync(photos, ct);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Fetches a page of photos ordered by date indexed (newest first).
    /// </summary>
    public Task<List<Photo>> GetPageAsync(int skip, int take, CancellationToken ct = default) =>
        _db.Photos
            .Where(p => !p.IsDeleted)
            .OrderByDescending(p => p.DateIndexed)
            .Skip(skip)
            .Take(take)
            .AsNoTracking()
            .ToListAsync(ct);

    /// <summary>
    /// Searches photos by filename or folder path and returns a page of results.
    /// </summary>
    public Task<List<Photo>> SearchAsync(string searchText, int skip, int take, CancellationToken ct = default)
    {
        var lower = searchText.ToLower();
        return _db.Photos
            .Where(p => !p.IsDeleted && (
                p.FileName.ToLower().Contains(lower) ||
                p.FilePath.ToLower().Contains(lower)))
            .OrderByDescending(p => p.DateIndexed)
            .Skip(skip)
            .Take(take)
            .AsNoTracking()
            .ToListAsync(ct);
    }
}
