using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DamYou.Data.Pipeline;
using DamYou.Data.Repositories;
using DamYou.Models;
using DamYou.Services;
using DamYou.Views;
using System.Collections.ObjectModel;

namespace DamYou.ViewModels;

public sealed partial class LibraryViewModel : ObservableObject
{
    private readonly ILibraryScanService _scanService;
    private readonly IPipelineTaskRepository _taskRepository;
    private readonly IPhotoRepository _photoRepository;
    private readonly IImportProgressService _importProgressService;
    private readonly IServiceProvider _services;

    private const int PageSize = 10;
    private int _totalPhotoCount = 0;
    private int _currentSkip = 0;
    private string _currentSearchText = string.Empty;
    private bool _isLoadingFromImport = false;

    public ObservableCollection<PhotoGridItem> GridPhotos { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScanProgressText))]
    private bool _isScanning;

    [ObservableProperty]
    private int _queueDepth;

    [ObservableProperty]
    private string? _scanCurrentFolder;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScanProgressText))]
    private int _scanDiscovered;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScanProgressText))]
    private int _scanEnqueued;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isSearchExpanded;

    [ObservableProperty]
    private bool _isPropertiesPanelVisible;

    [ObservableProperty]
    private bool _isLoadingMore;

    [ObservableProperty]
    private double _currentGridCellSize = 120;

    [ObservableProperty]
    private int _photoCount;

    public string ScanProgressText =>
        IsScanning && ScanDiscovered == 0
            ? "Scanning…"
            : $"Discovered {ScanDiscovered} files, enqueued {ScanEnqueued}";

    public LibraryViewModel(
        ILibraryScanService scanService,
        IPipelineTaskRepository taskRepository,
        IPhotoRepository photoRepository,
        IImportProgressService importProgressService,
        IServiceProvider services)
    {
        _scanService = scanService;
        _taskRepository = taskRepository;
        _photoRepository = photoRepository;
        _importProgressService = importProgressService;
        _services = services;

        // Subscribe to import progress to load new photos as they arrive
        _importProgressService.ImportProgressReported += OnImportProgressReported;
        _importProgressService.ImportCompleted += OnImportCompleted;
    }

    private async void OnImportProgressReported(int totalDiscovered, int processed, string? currentFile)
    {
        // Refresh photo grid periodically during import (every 50 photos to avoid excessive updates)
        if (processed % 50 == 0)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await RefreshPhotosFromImportAsync();
            });
        }
    }

    private async void OnImportCompleted()
    {
        // Do a final refresh when import completes
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            await RefreshPhotosFromImportAsync();
        });
    }

    private async Task RefreshPhotosFromImportAsync()
    {
        if (_isLoadingFromImport)
            return;

        _isLoadingFromImport = true;
        try
        {
            // Get updated photo count
            var newTotalCount = await _photoRepository.CountAsync();

            // If count increased, reload the grid
            if (newTotalCount > _totalPhotoCount)
            {
                _totalPhotoCount = newTotalCount;
                _currentSkip = 0;
                GridPhotos.Clear();

                var photos = await _photoRepository.GetPageAsync(0, PageSize);
                foreach (var photo in photos)
                {
                    GridPhotos.Add(new PhotoGridItem(photo));
                }

                _currentSkip = photos.Count;
                PhotoCount = GridPhotos.Count;
            }
        }
        finally
        {
            _isLoadingFromImport = false;
        }
    }

    /// <summary>
    /// Called when the view loads to fetch the first batch of photos and check if modal should show.
    /// </summary>
    [RelayCommand]
    private async Task InitializeAsync(CancellationToken ct)
    {
        await LoadPhotosAsync(ct);
    }

    /// <summary>
    /// Loads the initial batch of photos or searches based on current search text.
    /// </summary>
    private async Task LoadPhotosAsync(CancellationToken ct)
    {
        if (IsLoadingMore)
            return;

        IsLoadingMore = true;
        try
        {
            _currentSkip = 0;
            GridPhotos.Clear();

            List<DamYou.Data.Entities.Photo> photos;
            if (string.IsNullOrWhiteSpace(_currentSearchText))
            {
                _totalPhotoCount = await _photoRepository.CountAsync(ct);
                photos = await _photoRepository.GetPageAsync(0, PageSize, ct);
            }
            else
            {
                // For search, we need to get a count with the search filter
                photos = await _photoRepository.SearchAsync(_currentSearchText, 0, PageSize, ct);
            }

            foreach (var photo in photos)
            {
                GridPhotos.Add(new PhotoGridItem(photo));
            }

            _currentSkip = photos.Count;
            PhotoCount = GridPhotos.Count;
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    /// <summary>
    /// Loads the next batch of photos (lazy loading on scroll).
    /// </summary>
    [RelayCommand]
    private async Task LoadMorePhotosAsync(CancellationToken ct)
    {
        if (IsLoadingMore || _currentSkip >= _totalPhotoCount)
            return;

        IsLoadingMore = true;
        try
        {
            List<DamYou.Data.Entities.Photo> photos;
            if (string.IsNullOrWhiteSpace(_currentSearchText))
            {
                photos = await _photoRepository.GetPageAsync(_currentSkip, PageSize, ct);
            }
            else
            {
                photos = await _photoRepository.SearchAsync(_currentSearchText, _currentSkip, PageSize, ct);
            }

            foreach (var photo in photos)
            {
                GridPhotos.Add(new PhotoGridItem(photo));
            }

            _currentSkip += photos.Count;
            PhotoCount = GridPhotos.Count;
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    /// <summary>
    /// Called when search text changes to filter the grid.
    /// </summary>
    [RelayCommand]
    private async Task SearchTextChangedAsync(CancellationToken ct)
    {
        _currentSearchText = SearchText;
        await LoadPhotosAsync(ct);
    }

    /// <summary>
    /// Handles Ctrl+Wheel to resize grid cells.
    /// </summary>
    public void ResizeGrid(double delta)
    {
        const double minSize = 80;
        const double maxSize = 400;
        const double step = 20;

        var newSize = CurrentGridCellSize + (delta > 0 ? step : -step);
        CurrentGridCellSize = Math.Clamp(newSize, minSize, maxSize);
    }

    /// <summary>
    /// Toggles the properties panel visibility.
    /// </summary>
    [RelayCommand]
    private void TogglePropertiesPanel()
    {
        IsPropertiesPanelVisible = !IsPropertiesPanelVisible;
    }

    [RelayCommand]
    private async Task RescanLibraryAsync(CancellationToken ct)
    {
        IsScanning = true;
        ScanDiscovered = 0;
        ScanEnqueued = 0;
        ScanCurrentFolder = null;

        try
        {
            var progress = new Progress<ScanProgress>(p =>
            {
                ScanDiscovered = p.TotalDiscovered;
                ScanEnqueued = p.Enqueued;
                ScanCurrentFolder = p.CurrentFolder;
            });

            //await _scanService.ScanAsync(progress, ct);
            await RefreshQueueDepthAsync();
            await LoadPhotosAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // expected on cancellation
        }
        catch (Exception)
        {
            // surface via future error handling
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private async Task ViewTasksAsync()
    {
        var tasksView = _services.GetRequiredService<TasksView>();
        if (Application.Current?.Windows.Count > 0 && Application.Current.Windows[0].Page is NavigationPage nav)
            await nav.PushAsync(tasksView);
    }

    [RelayCommand]
    private async Task ManageFoldersAsync()
    {
        var manageFoldersModal = _services.GetRequiredService<ManageFoldersModal>();
        if (Application.Current?.Windows.Count > 0 && Application.Current.Windows[0].Page is NavigationPage nav)
            await nav.PushAsync(manageFoldersModal);
    }

    [RelayCommand]
    private async Task RefreshQueueDepthAsync()
    {
        QueueDepth = await _taskRepository.GetQueueDepthAsync();
    }
}
