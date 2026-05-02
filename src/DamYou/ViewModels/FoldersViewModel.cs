using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DamYou.Data.Import;
using DamYou.Data.Repositories;
using DamYou.Services;
using System.Collections.ObjectModel;

namespace DamYou.ViewModels;

public sealed partial class FoldersViewModel : ObservableObject
{
    private readonly IFolderRepository _folderRepository;
    private readonly IFolderPickerService _folderPickerService;

    public ObservableCollection<string> SelectedFolders { get; } = new();

    [ObservableProperty]
    private int _folderCount;

    [ObservableProperty]
    private bool _isBusy;

    private Dictionary<string, int> _folderIdMap = new(); // Track folder paths to IDs

    public FoldersViewModel(
        IFolderRepository folderRepository,
        IFolderPickerService folderPickerService)
    {
        _folderRepository = folderRepository;
        _folderPickerService = folderPickerService;
        SelectedFolders.CollectionChanged += (_, _) =>
        {
            FolderCount = SelectedFolders.Count;
        };
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        var folders = await _folderRepository.GetActiveFoldersAsync();
        SelectedFolders.Clear();
        _folderIdMap.Clear();
        foreach (var folder in folders)
        {
            SelectedFolders.Add(folder.Path);
            _folderIdMap[folder.Path] = folder.Id;
        }
    }

    [RelayCommand]
    private async Task AddFolderAsync()
    {
        var path = await _folderPickerService.PickFolderAsync();
        if (path is not null && !SelectedFolders.Contains(path))
        {
            SelectedFolders.Add(path);
            // Save to database immediately
            await _folderRepository.AddFoldersAsync(new[] { path });
        }
    }

    [RelayCommand]
    private async Task RemoveFolderAsync(string path)
    {
        SelectedFolders.Remove(path);
        // Remove from database
        if (_folderIdMap.TryGetValue(path, out var id))
        {
            await _folderRepository.DeactivateFolderAsync(id);
            _folderIdMap.Remove(path);
        }
    }
}

