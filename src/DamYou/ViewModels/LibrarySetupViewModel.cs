using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DamYou.Data.Import;
using DamYou.Data.Repositories;
using DamYou.Services;
using System.Collections.ObjectModel;

namespace DamYou.ViewModels;

public sealed partial class LibrarySetupViewModel : ObservableObject
{
    private readonly IFolderRepository _folderRepository;
    private readonly IFolderPickerService _folderPickerService;
    private readonly IPhotoImportService _importService;

    public ObservableCollection<string> SelectedFolders { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanComplete))]
    private int _folderCount;

    [ObservableProperty]
    private bool _isComplete;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanComplete))]
    private bool _isBusy;

    partial void OnIsBusyChanged(bool value)
    {
        CompleteSetupCommand.NotifyCanExecuteChanged();
    }

    [ObservableProperty]
    private bool _isImporting;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ImportProgressFraction))]
    [NotifyPropertyChangedFor(nameof(ImportProgressText))]
    private int _importTotal;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ImportProgressFraction))]
    [NotifyPropertyChangedFor(nameof(ImportProgressText))]
    private int _importProcessed;

    [ObservableProperty]
    private string? _importCurrentFile;

    public double ImportProgressFraction =>
        ImportTotal == 0 ? 0.0 : (double)ImportProcessed / ImportTotal;

    public string ImportProgressText =>
        ImportTotal == 0 ? "Discovering files…" : $"{ImportProcessed:N0} of {ImportTotal:N0} files";

    public bool CanComplete => SelectedFolders.Count > 0 && !IsBusy;

    private bool CanCompleteSetup() => CanComplete;

    public LibrarySetupViewModel(
        IFolderRepository folderRepository,
        IFolderPickerService folderPickerService,
        IPhotoImportService importService)
    {
        _folderRepository = folderRepository;
        _folderPickerService = folderPickerService;
        _importService = importService;
        SelectedFolders.CollectionChanged += (_, _) =>
        {
            FolderCount = SelectedFolders.Count;
            OnPropertyChanged(nameof(CanComplete));
            CompleteSetupCommand.NotifyCanExecuteChanged();
        };
    }

    [RelayCommand]
    private async Task AddFolderAsync()
    {
        var path = await _folderPickerService.PickFolderAsync();
        if (path is not null && !SelectedFolders.Contains(path))
        {
            SelectedFolders.Add(path);
        }
    }

    [RelayCommand]
    private void RemoveFolder(string path)
    {
        SelectedFolders.Remove(path);
    }

    [RelayCommand(CanExecute = nameof(CanCompleteSetup))]
    private async Task CompleteSetupAsync()
    {
        if (!CanComplete) return;
        IsBusy = true;
        try
        {
            await _folderRepository.AddFoldersAsync(SelectedFolders);

            IsImporting = true;
            var progress = new Progress<ImportProgress>(p =>
            {
                ImportTotal = p.TotalDiscovered;
                ImportProcessed = p.Processed;
                ImportCurrentFile = p.CurrentFile;
            });

            await _importService.ImportAsync(progress);
            IsComplete = true;
        }
        finally
        {
            IsBusy = false;
            IsImporting = false;
        }
    }
}
