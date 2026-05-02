using DamYou.Data.Pipeline;
using DamYou.Data.Repositories;
using DamYou.Services;
using DamYou.ViewModels;
using Moq;

namespace DamYou.Tests.ViewModels;

public sealed class StartupLogicTests
{
    [Fact]
    public async Task OnStartup_EmptyLibrary_InitializeAsyncLoadsWithZeroPhotos()
    {
        var mockPhotoRepo = new Mock<IPhotoRepository>();
        mockPhotoRepo.Setup(r => r.CountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        mockPhotoRepo.Setup(r => r.GetPageAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DamYou.Data.Entities.Photo>());

        var mockScanService = new Mock<ILibraryScanService>();
        var mockTaskRepo = new Mock<IPipelineTaskRepository>();
        mockTaskRepo.Setup(r => r.GetQueueDepthAsync())
            .ReturnsAsync(0);
        var mockImportProgressService = new Mock<IImportProgressService>();

        var vm = new LibraryViewModel(mockScanService.Object, mockTaskRepo.Object, mockPhotoRepo.Object, mockImportProgressService.Object, new MockServiceProvider());

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);

        Assert.Equal(0, vm.PhotoCount);
        // CountAsync is called during initialization
        mockPhotoRepo.Verify(r => r.CountAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task OnStartup_HasPhotos_InitializeAsyncLoadsPhotos()
    {
        var mockPhotos = new List<DamYou.Data.Entities.Photo>
        {
            new() { Id = 1, FileName = "photo1.jpg", FilePath = @"C:\photo1.jpg", FileSizeBytes = 1024, DateIndexed = DateTime.UtcNow },
            new() { Id = 2, FileName = "photo2.jpg", FilePath = @"C:\photo2.jpg", FileSizeBytes = 1024, DateIndexed = DateTime.UtcNow },
            new() { Id = 3, FileName = "photo3.jpg", FilePath = @"C:\photo3.jpg", FileSizeBytes = 1024, DateIndexed = DateTime.UtcNow },
            new() { Id = 4, FileName = "photo4.jpg", FilePath = @"C:\photo4.jpg", FileSizeBytes = 1024, DateIndexed = DateTime.UtcNow },
            new() { Id = 5, FileName = "photo5.jpg", FilePath = @"C:\photo5.jpg", FileSizeBytes = 1024, DateIndexed = DateTime.UtcNow },
        };

        var mockPhotoRepo = new Mock<IPhotoRepository>();
        mockPhotoRepo.Setup(r => r.CountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);
        mockPhotoRepo.Setup(r => r.GetPageAsync(0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockPhotos);

        var mockScanService = new Mock<ILibraryScanService>();
        var mockTaskRepo = new Mock<IPipelineTaskRepository>();
        mockTaskRepo.Setup(r => r.GetQueueDepthAsync())
            .ReturnsAsync(0);
        var mockImportProgressService = new Mock<IImportProgressService>();

        var vm = new LibraryViewModel(mockScanService.Object, mockTaskRepo.Object, mockPhotoRepo.Object, mockImportProgressService.Object, new MockServiceProvider());

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);

        Assert.Equal(5, vm.PhotoCount);
        Assert.Equal(5, vm.GridPhotos.Count);
    }

    [Fact]
    public async Task OnStartup_HasPhotos_ShowsManageFoldersCommand()
    {
        var mockPhotoRepo = new Mock<IPhotoRepository>();
        mockPhotoRepo.Setup(r => r.CountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        mockPhotoRepo.Setup(r => r.GetPageAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DamYou.Data.Entities.Photo>());

        var mockScanService = new Mock<ILibraryScanService>();
        var mockTaskRepo = new Mock<IPipelineTaskRepository>();
        mockTaskRepo.Setup(r => r.GetQueueDepthAsync())
            .ReturnsAsync(0);
        var mockImportProgressService = new Mock<IImportProgressService>();

        var vm = new LibraryViewModel(mockScanService.Object, mockTaskRepo.Object, mockPhotoRepo.Object, mockImportProgressService.Object, new MockServiceProvider());

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);

        Assert.True(vm.RescanLibraryCommand.CanExecute(null));
    }

    [Fact]
    public async Task RescanLibraryCommand_ExecutesSuccessfully()
    {
        var mockPhotoRepo = new Mock<IPhotoRepository>();
        mockPhotoRepo.Setup(r => r.CountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        mockPhotoRepo.Setup(r => r.GetPageAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DamYou.Data.Entities.Photo>());

        var mockScanService = new Mock<ILibraryScanService>();
        mockScanService.Setup(s => s.ScanAsync(It.IsAny<IProgress<ScanProgress>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockTaskRepo = new Mock<IPipelineTaskRepository>();
        mockTaskRepo.Setup(r => r.GetQueueDepthAsync())
            .ReturnsAsync(0);
        var mockImportProgressService = new Mock<IImportProgressService>();

        var vm = new LibraryViewModel(mockScanService.Object, mockTaskRepo.Object, mockPhotoRepo.Object, mockImportProgressService.Object, new MockServiceProvider());

        await vm.RescanLibraryCommand.ExecuteAsync(CancellationToken.None);

        Assert.False(vm.IsScanning);
        mockScanService.Verify(s => s.ScanAsync(It.IsAny<IProgress<ScanProgress>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

internal class MockServiceProvider : IServiceProvider
{
    public object? GetService(Type serviceType) => null;
}
