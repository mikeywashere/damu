using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DamYou.Data.Entities;
using DamYou.Data.Repositories;
using DamYou.Services;
using System.Collections.ObjectModel;

namespace DamYou.ViewModels;

public class PhotoFolderViewModel : ObservableObject
{
    private readonly WatchedFolder _folder;

    public int Id => _folder.Id;
    public string Path => _folder.Path;
    public int PhotoCount { get; set; }

    public PhotoFolderViewModel(WatchedFolder folder, int photoCount)
    {
        _folder = folder;
        PhotoCount = photoCount;
    }
}

public sealed partial class ManageFoldersViewModel : ObservableObject
{
    private readonly IFolderRepository _folderRepository;
    private readonly IPhotoRepository _photoRepository;
    private readonly IFolderPickerService _folderPickerService;

    public ObservableCollection<PhotoFolderViewModel> Folders { get; } = new();

    [ObservableProperty]
    private bool isLoading;

    public ManageFoldersViewModel(
        IFolderRepository folderRepository,
        IPhotoRepository photoRepository,
        IFolderPickerService folderPickerService)
    {
        _folderRepository = folderRepository;
        _photoRepository = photoRepository;
        _folderPickerService = folderPickerService;
    }

    [RelayCommand]
    public async Task InitializeAsync(CancellationToken ct)
    {
        await LoadFoldersAsync(ct);
    }

    private async Task LoadFoldersAsync(CancellationToken ct)
    {
        IsLoading = true;
        try
        {
            Folders.Clear();
            var folderList = await _folderRepository.GetActiveFoldersAsync(ct);
            
            foreach (var folder in folderList)
            {
                var count = await _photoRepository.CountByFolderAsync(folder.Id, ct);
                Folders.Add(new PhotoFolderViewModel(folder, count));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading folders: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task AddFolderAsync(CancellationToken ct)
    {
        try
        {
            var folderPath = await _folderPickerService.PickFolderAsync();
            if (string.IsNullOrEmpty(folderPath))
                return;

            await _folderRepository.AddFoldersAsync(new[] { folderPath }, ct);
            await LoadFoldersAsync(ct);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error adding folder: {ex}");
        }
    }

    [RelayCommand]
    public async Task DeleteFolderAsync(int folderId, CancellationToken ct)
    {
        try
        {
            var folder = Folders.FirstOrDefault(f => f.Id == folderId);
            if (folder == null)
                return;

            var mainPage = Application.Current?.MainPage;
            if (mainPage == null)
                return;

            bool confirmed = await mainPage.DisplayAlertAsync(
                "Remove Folder",
                $"Remove folder '{folder.Path}' and all its photos?",
                "Remove",
                "Cancel");

            if (!confirmed)
                return;

            await _folderRepository.DeactivateFolderAsync(folderId, ct);
            await LoadFoldersAsync(ct);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error deleting folder: {ex}");
        }
    }
}
