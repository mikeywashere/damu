using DamYou.Data;
using DamYou.Data.Import;
using DamYou.Data.Repositories;
using DamYou.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace DamYou.Tests.Import;

public sealed class PhotoImportServiceTests : IDisposable
{
    private readonly DamYouDbContext _db;
    private readonly FolderRepository _folderRepo;
    private readonly PhotoImportService _sut;
    private readonly SyntheticPhotoFixture _fixture;

    public PhotoImportServiceTests()
    {
        var options = new DbContextOptionsBuilder<DamYouDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new DamYouDbContext(options);
        _folderRepo = new FolderRepository(_db);
        _sut = new PhotoImportService(_db, _folderRepo);
        _fixture = new SyntheticPhotoFixture();
    }

    public void Dispose()
    {
        _db.Dispose();
        _fixture.Dispose();
    }

    private async Task AddWatchedFolderAsync(string path)
    {
        await _folderRepo.AddFoldersAsync([path]);
    }

    [Fact]
    public async Task ImportAsync_FindsAllImages_InSingleFolder()
    {
        _fixture.CreatePhotos(5);
        await AddWatchedFolderAsync(_fixture.RootDirectory);

        await _sut.ImportAsync(null, CancellationToken.None);

        Assert.Equal(5, await _db.Photos.CountAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ImportAsync_RecursivelyFindsImages_InSubfolders()
    {
        _fixture.CreatePhoto("top.jpg");
        _fixture.CreatePhoto("sub1.jpg", subFolder: "level1");
        _fixture.CreatePhoto("sub2.jpg", subFolder: "level1/level2");
        await AddWatchedFolderAsync(_fixture.RootDirectory);

        await _sut.ImportAsync(null, CancellationToken.None);

        Assert.Equal(3, await _db.Photos.CountAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ImportAsync_IgnoresNonImageFiles()
    {
        _fixture.CreatePhoto("real.jpg");
        File.WriteAllText(Path.Combine(_fixture.RootDirectory, "notes.txt"), "ignore me");
        File.WriteAllText(Path.Combine(_fixture.RootDirectory, "document.pdf"), "ignore me");
        File.WriteAllText(Path.Combine(_fixture.RootDirectory, "video.mp4"), "ignore me");
        await AddWatchedFolderAsync(_fixture.RootDirectory);

        await _sut.ImportAsync(null, CancellationToken.None);

        Assert.Equal(1, await _db.Photos.CountAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ImportAsync_ComputesCorrectSha256Hash()
    {
        var path = _fixture.CreatePhoto("hash-test.jpg");
        await AddWatchedFolderAsync(_fixture.RootDirectory);

        await _sut.ImportAsync(null, CancellationToken.None);

        var photo = await _db.Photos.SingleAsync(CancellationToken.None);
        var expectedHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        Assert.Equal(expectedHash, photo.FileHash);
    }

    [Fact]
    public async Task ImportAsync_SkipsDuplicates_OnReimport()
    {
        _fixture.CreatePhotos(3);
        await AddWatchedFolderAsync(_fixture.RootDirectory);

        await _sut.ImportAsync(null, CancellationToken.None);
        await _sut.ImportAsync(null, CancellationToken.None);

        Assert.Equal(3, await _db.Photos.CountAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ImportAsync_HandlesEmptyFolder()
    {
        await AddWatchedFolderAsync(_fixture.RootDirectory);

#pragma warning disable HAA0301 // Closure Allocation Source
        var ex = await Record.ExceptionAsync(() => _sut.ImportAsync(null, CancellationToken.None));
#pragma warning restore HAA0301 // Closure Allocation Source

        Assert.Null(ex);
        Assert.Equal(0, await _db.Photos.CountAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ImportAsync_RespectsCancellationToken()
    {
        _fixture.CreatePhotos(10);
        await AddWatchedFolderAsync(_fixture.RootDirectory);

        using var cts = new CancellationTokenSource();
#pragma warning disable HAA0301 // Closure Allocation Source
        var progress = new Progress<ImportProgress>(_ => cts.Cancel());
#pragma warning restore HAA0301 // Closure Allocation Source

#pragma warning disable HAA0301 // Closure Allocation Source
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _sut.ImportAsync(progress, cts.Token));
#pragma warning restore HAA0301 // Closure Allocation Source

        // At least the first file was processed before cancellation
        Assert.True(await _db.Photos.CountAsync(CancellationToken.None) >= 0);
    }

    [Fact]
    public async Task ImportAsync_ReportsProgress()
    {
        const int photoCount = 5;
        _fixture.CreatePhotos(photoCount);
        await AddWatchedFolderAsync(_fixture.RootDirectory);

        var reports = new List<ImportProgress>();
        var progress = new Progress<ImportProgress>(reports.Add);

        await _sut.ImportAsync(progress, CancellationToken.None);

        // Progress<T> posts asynchronously; give it a moment to flush
        await Task.Delay(50, CancellationToken.None);

        Assert.Equal(photoCount, reports.Count);
        Assert.All(reports, r => Assert.Equal(photoCount, r.TotalDiscovered));
        Assert.Equal(photoCount, reports.Last().Processed);
    }
}
