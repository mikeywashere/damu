using DamYou.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace DamYou.Data.Repositories;

#pragma warning disable HAA0301 // Closure allocations are expected in EF Core LINQ queries
public sealed class PhotoRepository : IPhotoRepository
{
    private readonly DamYouDbContext _db;

    public PhotoRepository(DamYouDbContext db) => _db = db;

    public Task<bool> ExistsByPathAsync(string filePath, CancellationToken ct = default)
    {
        Expression<Func<Photo, bool>> predicate = p => p.FilePath == filePath;
        return _db.Photos.AnyAsync(predicate, ct);
    }

    public Task<int> CountAsync(CancellationToken ct = default) =>
        _db.Photos.CountAsync(ct);

    public Task<int> CountByFolderAsync(int folderId, CancellationToken ct = default)
    {
        Expression<Func<Photo, bool>> predicate = p => p.WatchedFolderId == folderId;
        return _db.Photos.Where(predicate).CountAsync(ct);
    }

    public async Task AddPhotosAsync(IEnumerable<Photo> photos, CancellationToken ct = default)
    {
        await _db.Photos.AddRangeAsync(photos, ct);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Fetches a page of photos ordered by date indexed (newest first).
    /// </summary>
    public Task<List<Photo>> GetPageAsync(int skip, int take, CancellationToken ct = default)
    {
        Expression<Func<Photo, bool>> predicate = p => !p.IsDeleted;
        return _db.Photos
            .Where(predicate)
            .OrderByDescending(p => p.DateIndexed)
            .Skip(skip)
            .Take(take)
            .AsNoTracking()
            .ToListAsync(ct);
    }

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

    /// <summary>
    /// Gets all file paths for a photo (including the original and any duplicates).
    /// </summary>
    public async Task<List<string>> GetDuplicatePathsAsync(int photoId, CancellationToken ct = default)
    {
        var photo = await _db.Photos
            .Where(p => p.Id == photoId)
            .Select(p => new { p.FilePath })
            .FirstOrDefaultAsync(ct);

        if (photo is null)
            return [];

        var duplicatePaths = await _db.PhotoDuplicates
            .Where(d => d.PhotoId == photoId)
            .Select(d => d.FilePath)
            .AsNoTracking()
            .ToListAsync(ct);

        var allPaths = new List<string> { photo.FilePath };
        allPaths.AddRange(duplicatePaths);
        return allPaths;
    }
}
#pragma warning restore HAA0301
