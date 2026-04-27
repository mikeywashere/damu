using DamYou.Data;
using DamYou.Data.Repositories;
using DamYou.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace DamYou.Tests.Data;

public sealed class FolderRepositoryTests : IDisposable
{
    private readonly DamYouDbContext _db;
    private readonly FolderRepository _repository;
    private readonly SyntheticPhotoFixture _photoFixture;

    public FolderRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<DamYouDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new DamYouDbContext(options);
        _repository = new FolderRepository(_db);
        _photoFixture = new SyntheticPhotoFixture();
    }

    [Fact]
    public async Task GetActiveFolders_ReturnsEmpty_WhenNoFoldersAdded()
    {
        var result = await _repository.GetActiveFoldersAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task AddFolders_AddsSinglePath()
    {
        var path = _photoFixture.CreateSubFolder("vacation");

        await _repository.AddFoldersAsync([path]);

        var folders = await _repository.GetActiveFoldersAsync();
        Assert.Single(folders);
        Assert.Equal(path, folders[0].Path);
        Assert.True(folders[0].IsActive);
    }

    [Fact]
    public async Task AddFolders_AddsMultiplePaths()
    {
        var paths = new[]
        {
            _photoFixture.CreateSubFolder("2022"),
            _photoFixture.CreateSubFolder("2023"),
            _photoFixture.CreateSubFolder("2024"),
        };

        await _repository.AddFoldersAsync(paths);

        var folders = await _repository.GetActiveFoldersAsync();
        Assert.Equal(3, folders.Count);
    }

    [Fact]
    public async Task AddFolders_DoesNotAddDuplicates()
    {
        var path = _photoFixture.CreateSubFolder("photos");

        await _repository.AddFoldersAsync([path]);
        await _repository.AddFoldersAsync([path]); // duplicate

        var folders = await _repository.GetActiveFoldersAsync();
        Assert.Single(folders);
    }

    [Fact]
    public async Task DeactivateFolder_SetsIsActiveFalse()
    {
        var path = _photoFixture.CreateSubFolder("to-remove");
        await _repository.AddFoldersAsync([path]);
        var folders = await _repository.GetActiveFoldersAsync();
        var folderId = folders[0].Id;

        await _repository.DeactivateFolderAsync(folderId);

        var remaining = await _repository.GetActiveFoldersAsync();
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task DeactivateFolder_DoesNotThrow_WhenIdDoesNotExist()
    {
        // Should silently no-op on missing id
        await _repository.DeactivateFolderAsync(99999);
        var folders = await _repository.GetActiveFoldersAsync();
        Assert.Empty(folders);
    }

    [Fact]
    public async Task GetActiveFolders_OrderedByDateAdded()
    {
        // Add folders with slight delays to ensure ordering
        await _repository.AddFoldersAsync([_photoFixture.CreateSubFolder("first")]);
        await Task.Delay(10);
        await _repository.AddFoldersAsync([_photoFixture.CreateSubFolder("second")]);
        await Task.Delay(10);
        await _repository.AddFoldersAsync([_photoFixture.CreateSubFolder("third")]);

        var folders = await _repository.GetActiveFoldersAsync();

        Assert.Equal(3, folders.Count);
        Assert.True(folders[0].DateAdded <= folders[1].DateAdded);
        Assert.True(folders[1].DateAdded <= folders[2].DateAdded);
    }

    public void Dispose()
    {
        _db.Dispose();
        _photoFixture.Dispose();
    }
}
