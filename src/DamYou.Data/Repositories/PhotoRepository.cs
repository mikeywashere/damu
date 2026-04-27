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
}
