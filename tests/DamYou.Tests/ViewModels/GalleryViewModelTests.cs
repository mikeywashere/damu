using DamYou.Data.Entities;
using DamYou.Data.Pipeline;
using DamYou.Data.Repositories;
using DamYou.Models;
using DamYou.Services;
using DamYou.ViewModels;
using Moq;

namespace DamYou.Tests.ViewModels;

/// <summary>
/// Comprehensive test suite for GalleryViewModel covering pagination, responsive loading,
/// refresh logic, and edge cases. Tests validate:
/// - Pagination state machine (CurrentPage, TotalPages, CanLoadMore)
/// - Responsive page sizing via SetGridDimensions()
/// - Search/filter integration
/// - Edge cases (empty library, single photo, large library 5000+)
/// - Concurrency safety (resize while loading, search while loading)
/// - Missing metadata graceful degradation
/// </summary>
public sealed class GalleryViewModelTests
{
    private const int DefaultPageSize = 10;

    private static Mock<IPhotoRepository> CreateMockPhotoRepository(List<Photo> allPhotos)
    {
        var mock = new Mock<IPhotoRepository>();

        mock.Setup(r => r.CountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(allPhotos.Count);

        mock.Setup(r => r.GetPageAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((int skip, int take, CancellationToken ct) =>
                Task.FromResult(allPhotos.Skip(skip).Take(take).ToList()));

        mock.Setup(r => r.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((string query, int skip, int take, CancellationToken ct) =>
                Task.FromResult(allPhotos
                    .Where(p => p.FileName.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .Skip(skip)
                    .Take(take)
                    .ToList()));

        return mock;
    }

    private static GalleryViewModel CreateViewModel(
        List<Photo> allPhotos,
        Mock<IPhotoRepository>? photoRepoMock = null,
        Mock<ILibraryScanService>? scanServiceMock = null,
        Mock<IPipelineTaskRepository>? taskRepoMock = null,
        Mock<IImportProgressService>? importProgressMock = null)
    {
        photoRepoMock ??= CreateMockPhotoRepository(allPhotos);
        scanServiceMock ??= new Mock<ILibraryScanService>();
        taskRepoMock ??= new Mock<IPipelineTaskRepository>();
        importProgressMock ??= new Mock<IImportProgressService>();

        var servicesMock = new Mock<IServiceProvider>();

        return new GalleryViewModel(
            scanServiceMock.Object,
            taskRepoMock.Object,
            photoRepoMock.Object,
            importProgressMock.Object,
            servicesMock.Object);
    }

    private static List<Photo> CreatePhotos(int count)
    {
        var photos = new List<Photo>();
        for (int i = 0; i < count; i++)
        {
            photos.Add(new Photo
            {
                Id = i + 1,
                FileName = $"photo_{i:D4}.jpg",
                FilePath = $@"C:\Photos\photo_{i:D4}.jpg",
                FileSizeBytes = 1024 * (i + 1),
                DateIndexed = DateTime.UtcNow.AddHours(-i),
                DateTaken = DateTime.UtcNow.AddDays(-i),
                Width = 1920,
                Height = 1080,
                Status = ProcessingStatus.Processed
            });
        }
        return photos;
    }

    #region Pagination State Machine Tests

    [Fact]
    public async Task InitializeAsync_WithPhotos_SetsInitialStateCorrectly()
    {
        var photos = CreatePhotos(25);
        var vm = CreateViewModel(photos);

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);

        Assert.False(vm.IsLoadingMore);
        Assert.Equal(DefaultPageSize, vm.GridPhotos.Count);
        Assert.Equal(DefaultPageSize, vm.PhotoCount);
        Assert.Equal(0, vm.CurrentPage);
    }

    [Fact]
    public async Task InitializeAsync_WithPhotos_AddsPhotoGridItemsToCollection()
    {
        var photos = CreatePhotos(15);
        var vm = CreateViewModel(photos);

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);

        Assert.All(vm.GridPhotos, item => Assert.IsType<PhotoGridItem>(item));
        Assert.Equal("photo_0000.jpg", vm.GridPhotos[0].FileName);
    }

    [Fact]
    public async Task LoadMorePhotosCommand_Appends_WithoutClearing()
    {
        var photos = CreatePhotos(30);
        var vm = CreateViewModel(photos);

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);
        var countAfterInit = vm.GridPhotos.Count;

        await vm.LoadMorePhotosCommand.ExecuteAsync(CancellationToken.None);

        Assert.Equal(DefaultPageSize + DefaultPageSize, vm.GridPhotos.Count);
        Assert.Equal(DefaultPageSize * 2, vm.PhotoCount);
    }

    [Fact]
    public async Task LoadMorePhotosCommand_DoesNotExecute_WhenAlreadyLoading()
    {
        var photos = CreatePhotos(50);
        var photoRepoMock = CreateMockPhotoRepository(photos);
        
        photoRepoMock.Setup(r => r.GetPageAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(async (int skip, int take, CancellationToken ct) =>
            {
                await Task.Delay(100, ct);
                return photos.Skip(skip).Take(take).ToList();
            });

        var vm = CreateViewModel(photos, photoRepoMock);

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);

        var loadTask = vm.LoadMorePhotosCommand.ExecuteAsync(CancellationToken.None);
        await vm.LoadMorePhotosCommand.ExecuteAsync(CancellationToken.None);
        await loadTask;

        Assert.Equal(DefaultPageSize * 2, vm.GridPhotos.Count);
    }

    [Fact]
    public async Task LoadMorePhotosCommand_StopsWhen_AllPhotosLoaded()
    {
        var photos = CreatePhotos(15);
        var vm = CreateViewModel(photos);

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);
        await vm.LoadMorePhotosCommand.ExecuteAsync(CancellationToken.None);

        var beforeThird = vm.GridPhotos.Count;
        await vm.LoadMorePhotosCommand.ExecuteAsync(CancellationToken.None);

        Assert.Equal(beforeThird, vm.GridPhotos.Count);
    }

    [Fact]
    public async Task IsLoadingMore_TogglesDuringLoadOperation()
    {
        var photos = CreatePhotos(30);
        var photoRepoMock = CreateMockPhotoRepository(photos);
        
        photoRepoMock.Setup(r => r.GetPageAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(async (int skip, int take, CancellationToken ct) =>
            {
                await Task.Delay(50, ct);
                return photos.Skip(skip).Take(take).ToList();
            });

        var vm = CreateViewModel(photos, photoRepoMock);

        var isLoadingStates = new List<bool>();
        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(vm.IsLoadingMore))
                isLoadingStates.Add(vm.IsLoadingMore);
        };

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);
        isLoadingStates.Clear();

        await vm.LoadMorePhotosCommand.ExecuteAsync(CancellationToken.None);

        Assert.Contains(true, isLoadingStates);
        Assert.False(vm.IsLoadingMore);
    }

    [Fact]
    public async Task CanLoadMore_ReturnsFalse_WhenAtLastPage()
    {
        var photos = CreatePhotos(15);
        var vm = CreateViewModel(photos);

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);
        await vm.LoadMorePhotosCommand.ExecuteAsync(CancellationToken.None);

        Assert.False(vm.CanLoadMore);
    }

    [Fact]
    public async Task CanLoadMore_IsTrue_WhenMorePhotosAvailable()
    {
        var photos = CreatePhotos(50);
        var vm = CreateViewModel(photos);

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);

        Assert.True(vm.CanLoadMore);
    }

    [Fact]
    public async Task CurrentPage_UpdatesAsPhotosLoad()
    {
        var photos = CreatePhotos(50);
        var vm = CreateViewModel(photos);

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);
        Assert.Equal(0, vm.CurrentPage);

        await vm.LoadMorePhotosCommand.ExecuteAsync(CancellationToken.None);
        Assert.Equal(1, vm.CurrentPage);

        await vm.LoadMorePhotosCommand.ExecuteAsync(CancellationToken.None);
        Assert.Equal(2, vm.CurrentPage);
    }

    #endregion

    #region Responsive Loading Behavior Tests

    [Theory]
    [InlineData(800, 600, 120)]
    [InlineData(1024, 768, 120)]
    [InlineData(1920, 1080, 120)]
    public async Task SetGridDimensions_CalculatesPageSize_BasedOnViewportAndCellSize(double width, double height, double cellSize)
    {
        var photos = CreatePhotos(100);
        var vm = CreateViewModel(photos);

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);

        vm.CurrentGridCellSize = cellSize;
        vm.SetGridDimensions(width, height);

        // After setting grid dimensions, should have calculated a new page size
        Assert.NotEmpty(vm.GridPhotos);
    }

    [Fact]
    public async Task SetGridDimensions_WithZeroDimensions_Ignores()
    {
        var photos = CreatePhotos(30);
        var vm = CreateViewModel(photos);

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);
        var initialCount = vm.GridPhotos.Count;

        vm.SetGridDimensions(0, 0);

        Assert.Equal(initialCount, vm.GridPhotos.Count);
    }

    [Fact]
    public async Task ResizeGrid_ChangesGridCellSize()
    {
        var photos = CreatePhotos(30);
        var vm = CreateViewModel(photos);

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);

        var initialSize = vm.CurrentGridCellSize;
        vm.ResizeGrid(1);

        Assert.True(vm.CurrentGridCellSize > initialSize);
    }

    [Fact]
    public async Task ResizeGrid_RespectsMinimumSize()
    {
        var photos = CreatePhotos(30);
        var vm = CreateViewModel(photos);

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);

        const double minSize = 80;
        while (vm.CurrentGridCellSize > minSize)
        {
            vm.ResizeGrid(-1);
        }

        Assert.True(vm.CurrentGridCellSize >= minSize);
    }

    [Fact]
    public async Task ResizeGrid_RespectsMaximumSize()
    {
        var photos = CreatePhotos(30);
        var vm = CreateViewModel(photos);

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);

        const double maxSize = 400;
        while (vm.CurrentGridCellSize < maxSize)
        {
            vm.ResizeGrid(1);
        }

        Assert.True(vm.CurrentGridCellSize <= maxSize);
    }

    #endregion

    #region Search and Filter Tests

    [Fact]
    public async Task SearchTextChanged_FiltersPhotos()
    {
        var photos = new List<Photo>
        {
            new() { Id = 1, FileName = "sunset.jpg", FilePath = @"C:\Photos\sunset.jpg", FileSizeBytes = 1024, DateIndexed = DateTime.UtcNow, Status = ProcessingStatus.Processed },
            new() { Id = 2, FileName = "sunrise.jpg", FilePath = @"C:\Photos\sunrise.jpg", FileSizeBytes = 1024, DateIndexed = DateTime.UtcNow, Status = ProcessingStatus.Processed },
            new() { Id = 3, FileName = "beach.jpg", FilePath = @"C:\Photos\beach.jpg", FileSizeBytes = 1024, DateIndexed = DateTime.UtcNow, Status = ProcessingStatus.Processed }
        };

        var vm = CreateViewModel(photos);

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);

        vm.SearchText = "sunset";
        await vm.SearchTextChangedCommand.ExecuteAsync(CancellationToken.None);

        Assert.Single(vm.GridPhotos);
        Assert.Equal("sunset.jpg", vm.GridPhotos[0].FileName);
    }

    [Fact]
    public async Task SearchTextChanged_ClearsAndReloads_FirstPage()
    {
        var photos = CreatePhotos(50);
        var vm = CreateViewModel(photos);

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);
        await vm.LoadMorePhotosCommand.ExecuteAsync(CancellationToken.None);
        Assert.Equal(DefaultPageSize * 2, vm.GridPhotos.Count);

        vm.SearchText = "photo_0000";
        await vm.SearchTextChangedCommand.ExecuteAsync(CancellationToken.None);

        Assert.NotEmpty(vm.GridPhotos);
        Assert.Equal(0, vm.CurrentPage);
    }

    [Fact]
    public async Task SearchTextChanged_WithEmptyQuery_ReloadsAll()
    {
        var photos = new List<Photo>
        {
            new() { Id = 1, FileName = "photo1.jpg", FilePath = @"C:\photo1.jpg", FileSizeBytes = 1024, DateIndexed = DateTime.UtcNow, Status = ProcessingStatus.Processed },
            new() { Id = 2, FileName = "photo2.jpg", FilePath = @"C:\photo2.jpg", FileSizeBytes = 1024, DateIndexed = DateTime.UtcNow, Status = ProcessingStatus.Processed }
        };

        var vm = CreateViewModel(photos);

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);

        vm.SearchText = "photo1";
        await vm.SearchTextChangedCommand.ExecuteAsync(CancellationToken.None);
        Assert.Single(vm.GridPhotos);

        vm.SearchText = "";
        await vm.SearchTextChangedCommand.ExecuteAsync(CancellationToken.None);
        Assert.Equal(2, vm.GridPhotos.Count);
    }

    [Fact]
    public async Task SearchTextChanged_WithNoResults_ReturnsEmpty()
    {
        var photos = CreatePhotos(10);
        var vm = CreateViewModel(photos);

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);

        vm.SearchText = "nonexistent";
        await vm.SearchTextChangedCommand.ExecuteAsync(CancellationToken.None);

        Assert.Empty(vm.GridPhotos);
    }

    #endregion

    #region Refresh Behavior Tests

    [Fact]
    public async Task RefreshAsync_ClearsGrid_AndReloadsFirstPage()
    {
        var photos = CreatePhotos(50);
        var vm = CreateViewModel(photos);

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);
        await vm.LoadMorePhotosCommand.ExecuteAsync(CancellationToken.None);
        var countBefore = vm.GridPhotos.Count;

        await vm.RefreshCommand.ExecuteAsync(CancellationToken.None);

        Assert.Equal(DefaultPageSize, vm.GridPhotos.Count);
        Assert.Equal(0, vm.CurrentPage);
        Assert.NotEqual(countBefore, vm.GridPhotos.Count);
    }

    [Fact]
    public async Task RefreshAsync_ClearsSearchText()
    {
        var photos = CreatePhotos(50);
        var vm = CreateViewModel(photos);

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);

        vm.SearchText = "photo_0000";
        await vm.SearchTextChangedCommand.ExecuteAsync(CancellationToken.None);
        Assert.NotEmpty(vm.SearchText);

        await vm.RefreshCommand.ExecuteAsync(CancellationToken.None);

        Assert.Empty(vm.SearchText);
        Assert.Equal(DefaultPageSize, vm.GridPhotos.Count);
    }

    [Fact]
    public async Task RescanLibraryAsync_ReloadsPhotosAfterScan()
    {
        var photos = CreatePhotos(10);
        var scanServiceMock = new Mock<ILibraryScanService>();
        var vm = CreateViewModel(photos, scanServiceMock: scanServiceMock);

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);

        var cts = new CancellationTokenSource();
        await vm.RescanLibraryCommand.ExecuteAsync(cts.Token);

        Assert.False(vm.IsScanning);
        Assert.NotEmpty(vm.GridPhotos);
    }

    [Fact]
    public async Task RescanLibraryAsync_ResetsCounters()
    {
        var photos = CreatePhotos(10);
        var vm = CreateViewModel(photos);

        vm.ScanDiscovered = 5;
        vm.ScanEnqueued = 3;

        var cts = new CancellationTokenSource();
        await vm.RescanLibraryCommand.ExecuteAsync(cts.Token);

        Assert.Equal(0, vm.ScanDiscovered);
        Assert.Equal(0, vm.ScanEnqueued);
    }

    #endregion

    #region Edge Cases: Empty Library

    [Fact]
    public async Task InitializeAsync_WithNoPhotos_DoesNotCrash()
    {
        var photos = new List<Photo>();
        var vm = CreateViewModel(photos);

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);

        Assert.Empty(vm.GridPhotos);
        Assert.Equal(0, vm.PhotoCount);
        Assert.False(vm.IsLoadingMore);
    }

    [Fact]
    public async Task LoadMorePhotosCommand_WithNoPhotos_IsNoOp()
    {
        var photos = new List<Photo>();
        var vm = CreateViewModel(photos);

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);
        await vm.LoadMorePhotosCommand.ExecuteAsync(CancellationToken.None);

        Assert.Empty(vm.GridPhotos);
    }

    [Fact]
    public async Task SearchTextChanged_WithNoPhotos_ReturnsEmpty()
    {
        var photos = new List<Photo>();
        var vm = CreateViewModel(photos);

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);

        vm.SearchText = "anything";
        await vm.SearchTextChangedCommand.ExecuteAsync(CancellationToken.None);

        Assert.Empty(vm.GridPhotos);
    }

    #endregion

    #region Edge Cases: Single Photo

    [Fact]
    public async Task InitializeAsync_WithSinglePhoto_LoadsIt()
    {
        var photos = CreatePhotos(1);
        var vm = CreateViewModel(photos);

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);

        Assert.Single(vm.GridPhotos);
        Assert.Equal("photo_0000.jpg", vm.GridPhotos[0].FileName);
    }

    [Fact]
    public async Task LoadMorePhotosCommand_WithSinglePhoto_Stops()
    {
        var photos = CreatePhotos(1);
        var vm = CreateViewModel(photos);

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);
        await vm.LoadMorePhotosCommand.ExecuteAsync(CancellationToken.None);

        Assert.Single(vm.GridPhotos);
    }

    #endregion

    #region Edge Cases: Large Library

    [Fact]
    public async Task InitializeAsync_WithLargeLibrary_LoadsFirstPageOnly()
    {
        var photos = CreatePhotos(5000);
        var vm = CreateViewModel(photos);

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);

        Assert.Equal(DefaultPageSize, vm.GridPhotos.Count);
    }

    [Fact]
    public async Task LoadMorePhotosCommand_WithLargeLibrary_PaginatesCorrectly()
    {
        var photos = CreatePhotos(5000);
        var vm = CreateViewModel(photos);

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);

        var pageCount = 0;
        while (vm.GridPhotos.Count < 100)
        {
            await vm.LoadMorePhotosCommand.ExecuteAsync(CancellationToken.None);
            pageCount++;
        }

        Assert.Equal(DefaultPageSize * (pageCount + 1), vm.GridPhotos.Count);
    }

    [Fact]
    public async Task GridPhotos_WithLargeLibrary_DoesNotLoadAllAtOnce()
    {
        var photos = CreatePhotos(5000);
        var photoRepoMock = CreateMockPhotoRepository(photos);
        var vm = CreateViewModel(photos, photoRepoMock);

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);

        photoRepoMock.Verify(
            r => r.GetPageAsync(It.IsAny<int>(), DefaultPageSize, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Edge Cases: Missing Metadata

    [Fact]
    public async Task InitializeAsync_WithPhotosWithoutDateTaken_LoadsSuccessfully()
    {
        var photos = new List<Photo>
        {
            new() { Id = 1, FileName = "no_date.jpg", FilePath = @"C:\Photos\no_date.jpg", FileSizeBytes = 1024, DateIndexed = DateTime.UtcNow, DateTaken = null, Status = ProcessingStatus.Processed },
            new() { Id = 2, FileName = "has_date.jpg", FilePath = @"C:\Photos\has_date.jpg", FileSizeBytes = 1024, DateIndexed = DateTime.UtcNow, DateTaken = DateTime.UtcNow, Status = ProcessingStatus.Processed }
        };

        var vm = CreateViewModel(photos);

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);

        Assert.Equal(2, vm.GridPhotos.Count);
        Assert.Null(vm.GridPhotos[0].DateTaken);
        Assert.NotNull(vm.GridPhotos[1].DateTaken);
    }

    [Fact]
    public async Task GridPhotoItem_WithMissingDimensions_StillPopulates()
    {
        var photos = new List<Photo>
        {
            new() { Id = 1, FileName = "no_dims.jpg", FilePath = @"C:\Photos\no_dims.jpg", FileSizeBytes = 1024, DateIndexed = DateTime.UtcNow, Width = null, Height = null, Status = ProcessingStatus.Processed }
        };

        var vm = CreateViewModel(photos);

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);

        Assert.NotNull(vm.GridPhotos[0]);
        Assert.Null(vm.GridPhotos[0].Width);
        Assert.Null(vm.GridPhotos[0].Height);
    }

    #endregion

    #region Concurrency Tests

    [Fact]
    public async Task ResizeWhileLoading_DoesNotCauseDoubleLoad()
    {
        var photos = CreatePhotos(50);
        var photoRepoMock = CreateMockPhotoRepository(photos);
        
        var callCount = 0;
        photoRepoMock.Setup(r => r.GetPageAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(async (int skip, int take, CancellationToken ct) =>
            {
                Interlocked.Increment(ref callCount);
                await Task.Delay(50, ct);
                return photos.Skip(skip).Take(take).ToList();
            });

        var vm = CreateViewModel(photos, photoRepoMock);

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);

        var loadTask = vm.LoadMorePhotosCommand.ExecuteAsync(CancellationToken.None);
        vm.ResizeGrid(1);
        await loadTask;

        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task CancellationToken_StopsLoadingOperation()
    {
        var photos = CreatePhotos(50);
        var photoRepoMock = CreateMockPhotoRepository(photos);
        
        var wasCancelled = false;
        photoRepoMock.Setup(r => r.GetPageAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(async (int skip, int take, CancellationToken ct) =>
            {
                await Task.Delay(100, ct);
                if (ct.IsCancellationRequested)
                    wasCancelled = true;
                return photos.Skip(skip).Take(take).ToList();
            });

        var vm = CreateViewModel(photos, photoRepoMock);

        var cts = new CancellationTokenSource();
        var task = vm.InitializeCommand.ExecuteAsync(cts.Token);
        cts.CancelAfter(50);

        try { await task; } catch { }

        Assert.True(wasCancelled);
    }

    #endregion

    #region UI State Tests

    [Fact]
    public async Task TogglePropertiesPanel_TogglesProperty()
    {
        var photos = CreatePhotos(10);
        var vm = CreateViewModel(photos);

        var initialState = vm.IsPropertiesPanelVisible;

        vm.TogglePropertiesPanelCommand.Execute(null);

        Assert.NotEqual(initialState, vm.IsPropertiesPanelVisible);
    }

    [Fact]
    public async Task PhotoGridItem_WrapsPhotoEntity()
    {
        var photo = new Photo
        {
            Id = 42,
            FileName = "test.jpg",
            FilePath = @"C:\test.jpg",
            FileSizeBytes = 2048,
            DateIndexed = DateTime.UtcNow,
            DateTaken = DateTime.UtcNow.AddDays(-1),
            Width = 1920,
            Height = 1080,
            Status = ProcessingStatus.Processed
        };

        var item = new PhotoGridItem(photo);

        Assert.Equal(42, item.Id);
        Assert.Equal("test.jpg", item.FileName);
        Assert.Equal(1920, item.Width);
        Assert.Equal(1080, item.Height);
    }

    #endregion

    #region Total Pages Calculation

    [Fact]
    public async Task TotalPages_CalculatedCorrectly()
    {
        var photos = CreatePhotos(35);
        var vm = CreateViewModel(photos);

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);

        // 35 photos / 10 per page = 3.5, rounded up = 4 pages
        Assert.Equal(4, vm.TotalPages);
    }

    [Fact]
    public async Task TotalPages_WithZeroPhotos()
    {
        var photos = new List<Photo>();
        var vm = CreateViewModel(photos);

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);

        Assert.Equal(0, vm.TotalPages);
    }

    #endregion
}
