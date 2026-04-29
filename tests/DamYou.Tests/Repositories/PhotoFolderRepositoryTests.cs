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

        await _repository.AddFoldersAsync([path]);

        var folders = await _db.WatchedFolders.ToListAsync();
        Assert.Single(folders);
        Assert.True(folders[0].Id > 0);
        Assert.Equal(path, folders[0].Path);
        Assert.True(folders[0].DateAdded != default);
    }

    [Fact]
    public async Task AddFoldersAsync_DuplicatePath_DoesNotAddDuplicate()
    {
        var path = @"C:\Photos";

        await _repository.AddFoldersAsync([path]);
        await _repository.AddFoldersAsync([path]);

        var folders = await _db.WatchedFolders.ToListAsync();
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

        await _repository.AddFoldersAsync(paths);

        var folders = await _repository.GetActiveFoldersAsync();

        Assert.Equal(3, folders.Count);
        Assert.Contains(folders, f => f.Path == paths[0]);
        Assert.Contains(folders, f => f.Path == paths[1]);
        Assert.Contains(folders, f => f.Path == paths[2]);
    }

    [Fact]
    public async Task GetActiveFoldersAsync_ReturnsSortedByDateAdded()
    {
        await _repository.AddFoldersAsync([@"C:\Photos1"]);
        await Task.Delay(10);
        await _repository.AddFoldersAsync([@"C:\Photos2"]);
        await Task.Delay(10);
        await _repository.AddFoldersAsync([@"C:\Photos3"]);

        var folders = await _repository.GetActiveFoldersAsync();

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

        await _repository.AddFoldersAsync([path1, path2]);
        var folders = await _repository.GetActiveFoldersAsync();
        var folder1Id = folders.First(f => f.Path == path1).Id;

        await _repository.DeactivateFolderAsync(folder1Id);

        var activeFolders = await _repository.GetActiveFoldersAsync();
        Assert.Single(activeFolders);
        Assert.Equal(path2, activeFolders[0].Path);
    }

    [Fact]
    public async Task RemoveFolderAsync_DeactivatesFolder_ById()
    {
        var path = @"C:\Photos";
        await _repository.AddFoldersAsync([path]);

        var folders = await _repository.GetActiveFoldersAsync();
        var folderId = folders[0].Id;

        await _repository.DeactivateFolderAsync(folderId);

        var remaining = await _repository.GetActiveFoldersAsync();
        Assert.Empty(remaining);

        var allFolders = await _db.WatchedFolders.ToListAsync();
        Assert.Single(allFolders);
        Assert.False(allFolders[0].IsActive);
    }

    [Fact]
    public async Task RemoveFolderAsync_CascadeDeletesPhotos_WhenFolderIsDeleted()
    {
        var folderPath = @"C:\Photos";
        await _repository.AddFoldersAsync([folderPath]);

        var folder = (await _repository.GetActiveFoldersAsync()).First();
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
        await _db.Photos.AddRangeAsync(photos);
        await _db.SaveChangesAsync();

        // When folder is actually deleted (cascade), photos are removed
        var folderToDelete = _db.WatchedFolders.First(f => f.Id == folder.Id);
        _db.WatchedFolders.Remove(folderToDelete);
        await _db.SaveChangesAsync();

        var remainingPhotos = await _db.Photos.Where(p => p.WatchedFolderId == folder.Id).ToListAsync();

        Assert.Empty(remainingPhotos);
    }

    [Fact]
    public async Task RemoveFolderAsync_LeavesOtherFoldersPhotos_Untouched()
    {
        var path1 = @"C:\Photos";
        var path2 = @"C:\Pictures";
        await _repository.AddFoldersAsync([path1, path2]);

        var folders = await _repository.GetActiveFoldersAsync();
        var folder1 = folders.First(f => f.Path == path1);
        var folder2 = folders.First(f => f.Path == path2);

        var photosFolder1 = new List<Photo>();
        for (int i = 0; i < 3; i++)
        {
            photosFolder1.Add(new Photo
            {
                WatchedFolderId = folder1.Id,
                FileName = $"photo1_{i}.jpg",
                FilePath = $@"C:\Photos\photo1_{i}.jpg",
                FileSizeBytes = 1024,
                DateIndexed = DateTime.UtcNow,
                Status = ProcessingStatus.Unprocessed
            });
        }

        var photosFolder2 = new List<Photo>();
        for (int i = 0; i < 2; i++)
        {
            photosFolder2.Add(new Photo
            {
                WatchedFolderId = folder2.Id,
                FileName = $"photo2_{i}.jpg",
                FilePath = $@"C:\Pictures\photo2_{i}.jpg",
                FileSizeBytes = 1024,
                DateIndexed = DateTime.UtcNow,
                Status = ProcessingStatus.Unprocessed
            });
        }

        await _db.Photos.AddRangeAsync(photosFolder1);
        await _db.Photos.AddRangeAsync(photosFolder2);
        await _db.SaveChangesAsync();

        await _repository.DeactivateFolderAsync(folder1.Id);

        var remainingPhotos = await _db.Photos.ToListAsync();

        // Deactivating folder doesn't delete photos - they remain but folder is inactive
        Assert.Equal(5, remainingPhotos.Count);
        Assert.Equal(3, remainingPhotos.Count(p => p.WatchedFolderId == folder1.Id));
        Assert.Equal(2, remainingPhotos.Count(p => p.WatchedFolderId == folder2.Id));
    }

    [Fact]
    public async Task DeactivateFolderAsync_DoesNotThrow_WhenIdDoesNotExist()
    {
        // Should not throw and should be idempotent
        await _repository.DeactivateFolderAsync(99999);

        var folders = await _repository.GetActiveFoldersAsync();
        Assert.Empty(folders);
    }

    [Fact]
    public async Task GetAllFoldersAsync_ReturnsAllFolders_IncludingInactive()
    {
        var path1 = @"C:\Photos";
        var path2 = @"C:\Pictures";

        await _repository.AddFoldersAsync([path1, path2]);
        var folders = await _repository.GetActiveFoldersAsync();
        var folder1Id = folders.First(f => f.Path == path1).Id;

        await _repository.DeactivateFolderAsync(folder1Id);

        var allFolders = await _db.WatchedFolders.ToListAsync();

        Assert.Equal(2, allFolders.Count);
        Assert.Single(allFolders, f => f.IsActive);
        Assert.Single(allFolders, f => !f.IsActive);
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}

