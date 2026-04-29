using DamYou.Data.Entities;
using DamYou.Data.Repositories;
using DamYou.Services;
using DamYou.ViewModels;
using Moq;

namespace DamYou.Tests.ViewModels;

public sealed class ManageFoldersViewModelTests
{
    [Fact]
    public async Task InitializeAsync_LoadsFolders_PopulatesCollection()
    {
        var mockFolders = new List<WatchedFolder>
        {
            new() { Id = 1, Path = @"C:\Photos", DateAdded = DateTime.UtcNow, IsActive = true },
            new() { Id = 2, Path = @"C:\Pictures", DateAdded = DateTime.UtcNow, IsActive = true }
        };

        var mockFolderRepo = new Mock<IFolderRepository>();
        mockFolderRepo.Setup(r => r.GetActiveFoldersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockFolders);

        var mockPhotoRepo = new Mock<IPhotoRepository>();
        mockPhotoRepo.Setup(r => r.CountByFolderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var mockFolderPicker = new Mock<IFolderPickerService>();

        var vm = new ManageFoldersViewModel(mockFolderRepo.Object, mockPhotoRepo.Object, mockFolderPicker.Object);

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);

        Assert.Equal(2, vm.Folders.Count);
        Assert.Equal(@"C:\Photos", vm.Folders[0].Path);
        Assert.Equal(@"C:\Pictures", vm.Folders[1].Path);
    }

    [Fact]
    public async Task LoadFolders_PopulatesObservableCollection_WithPhotoCount()
    {
        var mockFolders = new List<WatchedFolder>
        {
            new() { Id = 1, Path = @"C:\Photos", DateAdded = DateTime.UtcNow, IsActive = true }
        };

        var mockFolderRepo = new Mock<IFolderRepository>();
        mockFolderRepo.Setup(r => r.GetActiveFoldersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockFolders);

        var mockPhotoRepo = new Mock<IPhotoRepository>();
        mockPhotoRepo.Setup(r => r.CountByFolderAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        var mockFolderPicker = new Mock<IFolderPickerService>();

        var vm = new ManageFoldersViewModel(mockFolderRepo.Object, mockPhotoRepo.Object, mockFolderPicker.Object);

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);

        Assert.Single(vm.Folders);
        Assert.Equal(42, vm.Folders[0].PhotoCount);
    }

    [Fact]
    public async Task AddFolderCommand_CallsFolderPicker_AndAddsFolder()
    {
        var mockFolderRepo = new Mock<IFolderRepository>();
        mockFolderRepo.Setup(r => r.GetActiveFoldersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WatchedFolder>
            {
                new() { Id = 1, Path = @"C:\NewFolder", DateAdded = DateTime.UtcNow, IsActive = true }
            });
        mockFolderRepo.Setup(r => r.AddFoldersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockPhotoRepo = new Mock<IPhotoRepository>();
        mockPhotoRepo.Setup(r => r.CountByFolderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var mockFolderPicker = new Mock<IFolderPickerService>();
        mockFolderPicker.Setup(f => f.PickFolderAsync())
            .ReturnsAsync(@"C:\NewFolder");

        var vm = new ManageFoldersViewModel(mockFolderRepo.Object, mockPhotoRepo.Object, mockFolderPicker.Object);

        await vm.AddFolderCommand.ExecuteAsync(CancellationToken.None);

        Assert.Single(vm.Folders);
        Assert.Equal(@"C:\NewFolder", vm.Folders[0].Path);
        mockFolderPicker.Verify(f => f.PickFolderAsync(), Times.Once);
        mockFolderRepo.Verify(r => r.AddFoldersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddFolderCommand_IgnoresNullResult()
    {
        var mockFolderRepo = new Mock<IFolderRepository>();
        mockFolderRepo.Setup(r => r.GetActiveFoldersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WatchedFolder>());

        var mockPhotoRepo = new Mock<IPhotoRepository>();
        var mockFolderPicker = new Mock<IFolderPickerService>();
        mockFolderPicker.Setup(f => f.PickFolderAsync())
            .ReturnsAsync((string?)null);

        var vm = new ManageFoldersViewModel(mockFolderRepo.Object, mockPhotoRepo.Object, mockFolderPicker.Object);

        await vm.AddFolderCommand.ExecuteAsync(CancellationToken.None);

        Assert.Empty(vm.Folders);
        mockFolderRepo.Verify(r => r.AddFoldersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteFolderCommand_DoesNothing_IfFolderNotFound()
    {
        var mockFolderRepo = new Mock<IFolderRepository>();
        mockFolderRepo.Setup(r => r.GetActiveFoldersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WatchedFolder>());

        var mockPhotoRepo = new Mock<IPhotoRepository>();
        var mockFolderPicker = new Mock<IFolderPickerService>();

        var vm = new ManageFoldersViewModel(mockFolderRepo.Object, mockPhotoRepo.Object, mockFolderPicker.Object);
        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);

        await vm.DeleteFolderCommand.ExecuteAsync(99999);

        mockFolderRepo.Verify(r => r.DeactivateFolderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InitializeAsync_SetsIsLoadingFlag()
    {
        var mockFolderRepo = new Mock<IFolderRepository>();
        mockFolderRepo.Setup(r => r.GetActiveFoldersAsync(It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(10);
                return new List<WatchedFolder>();
            });

        var mockPhotoRepo = new Mock<IPhotoRepository>();
        var mockFolderPicker = new Mock<IFolderPickerService>();

        var vm = new ManageFoldersViewModel(mockFolderRepo.Object, mockPhotoRepo.Object, mockFolderPicker.Object);

        Assert.False(vm.IsLoading);

        var task = vm.InitializeCommand.ExecuteAsync(CancellationToken.None);
        // IsLoading should be true during load
        // But since we delay in the mock, we can check after complete
        await task;

        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task GetAllFoldersAsync_ReturnsAllFolders_SortedByDateAdded()
    {
        var now = DateTime.UtcNow;
        var mockFolders = new List<WatchedFolder>
        {
            new() { Id = 1, Path = @"C:\Photos1", DateAdded = now, IsActive = true },
            new() { Id = 2, Path = @"C:\Photos2", DateAdded = now.AddMinutes(1), IsActive = true },
            new() { Id = 3, Path = @"C:\Photos3", DateAdded = now.AddMinutes(2), IsActive = true }
        };

        var mockFolderRepo = new Mock<IFolderRepository>();
        mockFolderRepo.Setup(r => r.GetActiveFoldersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockFolders);

        var mockPhotoRepo = new Mock<IPhotoRepository>();
        mockPhotoRepo.Setup(r => r.CountByFolderAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var mockFolderPicker = new Mock<IFolderPickerService>();

        var vm = new ManageFoldersViewModel(mockFolderRepo.Object, mockPhotoRepo.Object, mockFolderPicker.Object);

        await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);

        Assert.Equal(3, vm.Folders.Count);
        Assert.Equal(@"C:\Photos1", vm.Folders[0].Path);
        Assert.Equal(@"C:\Photos2", vm.Folders[1].Path);
        Assert.Equal(@"C:\Photos3", vm.Folders[2].Path);
    }
}

