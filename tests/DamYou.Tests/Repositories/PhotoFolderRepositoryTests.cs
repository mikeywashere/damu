using DamYou.Data;
using DamYou.Data.Entities;
using DamYou.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DamYou.Tests.Repositories;

public sealed class PhotoFolderRepositoryTests : IDisposable
{
    private readonly DamYouDbContext _db;
    private readonly FolderRepository _repository;

    public PhotoFolderRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<DamYouDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new DamYouDbContext(options);
        _repository = new FolderRepository(_db);
    }

    [Fact]
    public async Task AddFoldersAsync_CreatesNewFolder_WithUniqueId()
    {
        var path = @"C:\Photos";

        await _repository.AddFoldersAsync([path], CancellationToken.None);

        var folders = await _db.WatchedFolders.ToListAsync(CancellationToken.None);
        Assert.Single(folders);
        Assert.True(folders[0].Id > 0);
        Assert.Equal(path, folders[0].Path);
        Assert.True(folders[0].DateAdded != default);
    }

    [Fact]
    public async Task AddFoldersAsync_DuplicatePath_DoesNotAddDuplicate()
    {
        var path = @"C:\Photos";

        await _repository.AddFoldersAsync([path], CancellationToken.None);
        await _repository.AddFoldersAsync([path], CancellationToken.None);

        var folders = await _db.WatchedFolders.ToListAsync(CancellationToken.None);
        Assert.Single(folders);
        Assert.Equal(path, folders[0].Path);
    }

    [Fact]
    public async Task GetActiveFoldersAsync_ReturnsAllManagedFolders()
    {
        var paths = new[]
        {
            @"C:\Photos",
            @"C:\Pictures\2024",
            @"C:\Videos"
        };

        await _repository.AddFoldersAsync(paths, CancellationToken.None);

        var folders = await _repository.GetActiveFoldersAsync(CancellationToken.None);

        Assert.Equal(3, folders.Count);
#pragma warning disable HAA0301 // Closure Allocation Source
        Assert.Contains(folders, f => f.Path == paths[0]);
        Assert.Contains(folders, f => f.Path == paths[1]);
        Assert.Contains(folders, f => f.Path == paths[2]);
#pragma warning restore HAA0301 // Closure Allocation Source
    }

    [Fact]
    public async Task GetActiveFoldersAsync_ReturnsSortedByDateAdded()
    {
        await _repository.AddFoldersAsync([@"C:\Photos1"], CancellationToken.None);
        await Task.Delay(10, CancellationToken.None);
        await _repository.AddFoldersAsync([@"C:\Photos2"], CancellationToken.None);
        await Task.Delay(10, CancellationToken.None);
        await _repository.AddFoldersAsync([@"C:\Photos3"], CancellationToken.None);

        var folders = await _repository.GetActiveFoldersAsync(CancellationToken.None);

        Assert.Equal(3, folders.Count);
        Assert.Equal(@"C:\Photos1", folders[0].Path);
        Assert.Equal(@"C:\Photos2", folders[1].Path);
        Assert.Equal(@"C:\Photos3", folders[2].Path);
    }

    [Fact]
    public async Task GetActiveFoldersAsync_ExcludesInactiveFolders()
    {
        var path1 = @"C:\Photos";
        var path2 = @"C:\Pictures";

        await _repository.AddFoldersAsync([path1, path2], CancellationToken.None);
        var folders = await _repository.GetActiveFoldersAsync(CancellationToken.None);
#pragma warning disable HAA0301 // Closure Allocation Source
        var folder1Id = folders.First(f => f.Path == path1).Id;
#pragma warning restore HAA0301 // Closure Allocation Source

        await _repository.DeactivateFolderAsync(folder1Id, CancellationToken.None);

        var activeFolders = await _repository.GetActiveFoldersAsync(CancellationToken.None);
        Assert.Single(activeFolders);
        Assert.Equal(path2, activeFolders[0].Path);
    }

    [Fact]
    public async Task RemoveFolderAsync_DeactivatesFolder_ById()
    {
        var path = @"C:\Photos";
        await _repository.AddFoldersAsync([path], CancellationToken.None);

        var folders = await _repository.GetActiveFoldersAsync(CancellationToken.None);
        var folderId = folders[0].Id;

        await _repository.DeactivateFolderAsync(folderId, CancellationToken.None);

        var remaining = await _repository.GetActiveFoldersAsync(CancellationToken.None);
        Assert.Empty(remaining);

        var allFolders = await _db.WatchedFolders.ToListAsync(CancellationToken.None);
        Assert.Single(allFolders);
        Assert.False(allFolders[0].IsActive);
    }

    [Fact]
    public async Task RemoveFolderAsync_CascadeDeletesPhotos_WhenFolderIsDeleted()
    {
        var folderPath = @"C:\Photos";
        await _repository.AddFoldersAsync([folderPath], CancellationToken.None);

        var folder = (await _repository.GetActiveFoldersAsync(CancellationToken.None))[0];
        var photos = new List<Photo>();
        for (int i = 0; i < 5; i++)
        {
            photos.Add(new Photo
            {
                WatchedFolderId = folder.Id,
                FileName = $"photo_{i}.jpg",
                FilePath = $@"C:\Photos\photo_{i}.jpg",
                FileSizeBytes = 1024,
                DateIndexed = DateTime.UtcNow,
                Status = ProcessingStatus.Unprocessed
            });
        }
        await _db.Photos.AddRangeAsync(photos, CancellationToken.None);
        await _db.SaveChangesAsync(CancellationToken.None);

        // When folder is actually deleted (cascade), photos are removed
        var folderToDelete = _db.WatchedFolders.First(f => f.Id == folder.Id);
        _db.WatchedFolders.Remove(folderToDelete);
        await _db.SaveChangesAsync(CancellationToken.None);

        var remainingPhotos = await _db.Photos.Where(p => p.WatchedFolderId == folder.Id).ToListAsync(CancellationToken.None);

        Assert.Empty(remainingPhotos);
    }

    [Fact]
    public async Task RemoveFolderAsync_LeavesOtherFoldersPhotos_Untouched()
    {
        var path1 = @"C:\Photos";
        var path2 = @"C:\Pictures";
        await _repository.AddFoldersAsync([path1, path2], CancellationToken.None);

        var folders = await _repository.GetActiveFoldersAsync(CancellationToken.None);
#pragma warning disable HAA0301 // Closure Allocation Source
        var folder1 = folders.First(f => f.Path == path1);
        var folder2 = folders.First(f => f.Path == path2);
#pragma warning restore HAA0301 // Closure Allocation Source

        var photosFolder1 = new List<Photo>();
        for (int i = 0; i < 3; i++)
        {
#pragma warning disable HAA0601 // Value type to reference type conversion causing boxing allocation
            photosFolder1.Add(new Photo
            {
                WatchedFolderId = folder1.Id,
                FileName = $"photo1_{i}.jpg",
                FilePath = $@"C:\Photos\photo1_{i}.jpg",
                FileSizeBytes = 1024,
                DateIndexed = DateTime.UtcNow,
                Status = ProcessingStatus.Unprocessed
            });
#pragma warning restore HAA0601 // Value type to reference type conversion causing boxing allocation
        }

        var photosFolder2 = new List<Photo>();
        for (int i = 0; i < 2; i++)
        {
#pragma warning disable HAA0601 // Value type to reference type conversion causing boxing allocation
            photosFolder2.Add(new Photo
            {
                WatchedFolderId = folder2.Id,
                FileName = $"photo2_{i}.jpg",
                FilePath = $@"C:\Pictures\photo2_{i}.jpg",
                FileSizeBytes = 1024,
                DateIndexed = DateTime.UtcNow,
                Status = ProcessingStatus.Unprocessed
            });
#pragma warning restore HAA0601 // Value type to reference type conversion causing boxing allocation
        }

        await _db.Photos.AddRangeAsync(photosFolder1, CancellationToken.None);
        await _db.Photos.AddRangeAsync(photosFolder2, CancellationToken.None);
        await _db.SaveChangesAsync(CancellationToken.None);

        await _repository.DeactivateFolderAsync(folder1.Id, CancellationToken.None);

        var remainingPhotos = await _db.Photos.ToListAsync(CancellationToken.None);

        // Deactivating folder doesn't delete photos - they remain but folder is inactive
        Assert.Equal(5, remainingPhotos.Count);
#pragma warning disable HAA0301 // Closure Allocation Source
        Assert.Equal(3, remainingPhotos.Count(p => p.WatchedFolderId == folder1.Id));
        Assert.Equal(2, remainingPhotos.Count(p => p.WatchedFolderId == folder2.Id));
#pragma warning restore HAA0301 // Closure Allocation Source
    }

    [Fact]
    public async Task DeactivateFolderAsync_DoesNotThrow_WhenIdDoesNotExist()
    {
        // Should not throw and should be idempotent
        await _repository.DeactivateFolderAsync(99999, CancellationToken.None);

        var folders = await _repository.GetActiveFoldersAsync(CancellationToken.None);
        Assert.Empty(folders);
    }

    [Fact]
    public async Task GetAllFoldersAsync_ReturnsAllFolders_IncludingInactive()
    {
        var path1 = @"C:\Photos";
        var path2 = @"C:\Pictures";

        await _repository.AddFoldersAsync([path1, path2], CancellationToken.None);
        var folders = await _repository.GetActiveFoldersAsync(CancellationToken.None);
        var folder1Id = folders.First(f => f.Path == path1).Id;

        await _repository.DeactivateFolderAsync(folder1Id, CancellationToken.None);

        var allFolders = await _db.WatchedFolders.ToListAsync(CancellationToken.None);

        Assert.Equal(2, allFolders.Count);
        Assert.Single(allFolders, f => f.IsActive);
        Assert.Single(allFolders, f => !f.IsActive);
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}

