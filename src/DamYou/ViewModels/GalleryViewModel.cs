using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DamYou.Data.Pipeline;
using DamYou.Data.Repositories;
using DamYou.Models;
using DamYou.Services;
using System.Collections.ObjectModel;

namespace DamYou.ViewModels;

public sealed partial class GalleryViewModel : ObservableObject
{
    private readonly ILibraryScanService _scanService;
    private readonly IPipelineTaskRepository _taskRepository;
    private readonly IPhotoRepository _photoRepository;
    private readonly IImportProgressService _importProgressService;
    private readonly IServiceProvider _services;

    private const int DefaultPageSize = 10;
    private int _totalPhotoCount = 0;
    private int _currentSkip = 0;
    private string _currentSearchText = string.Empty;
    private bool _isLoadingFromImport = false;
    private int _calculatedPageSize = DefaultPageSize;
    private double _gridWidth = 0;
    private double _gridHeight = 0;

    public ObservableCollection<PhotoGridItem> GridPhotos { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScanProgressText))]
    private bool _isScanning;

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
    private bool _isPropertiesPanelVisible;

    [ObservableProperty]
    private bool _isLoadingMore;

    [ObservableProperty]
    private double _currentGridCellSize = 120;

    [ObservableProperty]
    private int _photoCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLoadMore))]
    private int _currentPage = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLoadMore))]
    private int _totalPages = 0;

    public bool CanLoadMore => CurrentPage < TotalPages && !IsLoadingMore;

    public string ScanProgressText =>
        IsScanning && ScanDiscovered == 0
            ? "Scanning…"
            : $"Discovered {ScanDiscovered} files, enqueued {ScanEnqueued}";

    public GalleryViewModel(
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

    /// <summary>
    /// Sets grid dimensions and calculates page size based on viewport and cell size.
    /// Call this from UI whenever the grid container size changes (SizeChanged event).
    /// </summary>
    public void SetGridDimensions(double gridWidth, double gridHeight)
    {
        if (gridWidth <= 0 || gridHeight <= 0)
            return;

        _gridWidth = gridWidth;
        _gridHeight = gridHeight;

        int newPageSize = CalculatePageSize(gridWidth, gridHeight, CurrentGridCellSize);
        
        // If page size changed significantly, recalculate what we need to load
        if (newPageSize != _calculatedPageSize && newPageSize > 0)
        {
            _calculatedPageSize = newPageSize;
            // Trigger a load if we don't have enough items on screen yet
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await EnsureViewportFilledAsync();
            });
        }
    }

    /// <summary>
    /// Calculates how many photos fit in the viewport based on grid dimensions and cell size.
    /// Formula: (gridWidth / cellWidth) * (gridHeight / cellHeight) + 1 row buffer
    /// </summary>
    private int CalculatePageSize(double gridWidth, double gridHeight, double cellSize)
    {
        if (gridWidth <= 0 || gridHeight <= 0 || cellSize <= 0)
            return DefaultPageSize;

        // Account for padding (Padding="12" on grid = 24 total, plus margins on cells)
        const double padding = 48;
        const double cellMargin = 8;
        double availableWidth = Math.Max(gridWidth - padding, cellSize);
        double availableHeight = Math.Max(gridHeight - padding, cellSize);

        int columnsPerRow = Math.Max(1, (int)(availableWidth / (cellSize + cellMargin)));
        int rowsInViewport = Math.Max(1, (int)(availableHeight / (cellSize + cellMargin)));
        
        // Load one extra row as a lazy buffer
        int calculated = (columnsPerRow * (rowsInViewport + 1));
        return Math.Max(DefaultPageSize, calculated);
    }

    /// <summary>
    /// Ensures the viewport is filled with photos. Called whenever grid size changes
    /// or when loading completes but we don't have enough items on screen.
    /// </summary>
    private async Task EnsureViewportFilledAsync()
    {
        // If we don't have enough photos to fill the viewport and there are more available
        if (GridPhotos.Count < _calculatedPageSize && CanLoadMore && !IsLoadingMore)
        {
            await LoadMorePhotosAsync(CancellationToken.None);
        }
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
                CurrentPage = 0;
                _currentSkip = 0;
                GridPhotos.Clear();
                
                var pageSize = _calculatedPageSize > 0 ? _calculatedPageSize : DefaultPageSize;
                var photos = await _photoRepository.GetPageAsync(0, pageSize);
                foreach (var photo in photos)
                {
                    GridPhotos.Add(new PhotoGridItem(photo));
                }
                
                _currentSkip = photos.Count;
                PhotoCount = GridPhotos.Count;
                RecalculateTotalPages();
            }
        }
        finally
        {
            _isLoadingFromImport = false;
        }
    }

    /// <summary>
    /// Recalculates TotalPages based on current photo count and page size.
    /// </summary>
    private void RecalculateTotalPages()
    {
        int pageSize = _calculatedPageSize > 0 ? _calculatedPageSize : DefaultPageSize;
        TotalPages = (int)Math.Ceiling((double)_totalPhotoCount / pageSize);
    }

    [RelayCommand]
    private async Task InitializeAsync(CancellationToken ct)
    {
        await LoadPhotosAsync(ct);
    }

    private async Task LoadPhotosAsync(CancellationToken ct)
    {
        if (IsLoadingMore)
            return;

        IsLoadingMore = true;
        try
        {
            CurrentPage = 0;
            _currentSkip = 0;
            GridPhotos.Clear();
            
            int pageSize = _calculatedPageSize > 0 ? _calculatedPageSize : DefaultPageSize;
            
            List<DamYou.Data.Entities.Photo> photos;
            if (string.IsNullOrWhiteSpace(_currentSearchText))
            {
                _totalPhotoCount = await _photoRepository.CountAsync(ct);
                photos = await _photoRepository.GetPageAsync(0, pageSize, ct);
            }
            else
            {
                photos = await _photoRepository.SearchAsync(_currentSearchText, 0, pageSize, ct);
            }

            foreach (var photo in photos)
            {
                GridPhotos.Add(new PhotoGridItem(photo));
            }

            _currentSkip = photos.Count;
            PhotoCount = GridPhotos.Count;
            RecalculateTotalPages();
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    [RelayCommand]
    private async Task LoadMorePhotosAsync(CancellationToken ct)
    {
        if (IsLoadingMore || _currentSkip >= _totalPhotoCount)
            return;

        IsLoadingMore = true;
        try
        {
            int pageSize = _calculatedPageSize > 0 ? _calculatedPageSize : DefaultPageSize;
            
            List<DamYou.Data.Entities.Photo> photos;
            if (string.IsNullOrWhiteSpace(_currentSearchText))
            {
                photos = await _photoRepository.GetPageAsync(_currentSkip, pageSize, ct);
            }
            else
            {
                photos = await _photoRepository.SearchAsync(_currentSearchText, _currentSkip, pageSize, ct);
            }

            foreach (var photo in photos)
            {
                GridPhotos.Add(new PhotoGridItem(photo));
            }

            _currentSkip += photos.Count;
            PhotoCount = GridPhotos.Count;
            
            CurrentPage = (int)Math.Ceiling((double)_currentSkip / pageSize);
            RecalculateTotalPages();
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken ct)
    {
        // Reset pagination state
        CurrentPage = 0;
        TotalPages = 0;
        _currentSkip = 0;
        _currentSearchText = string.Empty;
        SearchText = string.Empty;
        GridPhotos.Clear();
        
        // Reload first page while maintaining viewport size
        await LoadPhotosAsync(ct);
    }

    [RelayCommand]
    private async Task SearchTextChangedAsync(CancellationToken ct)
    {
        _currentSearchText = SearchText;
        await LoadPhotosAsync(ct);
    }

    public void ResizeGrid(double delta)
    {
        const double minSize = 80;
        const double maxSize = 400;
        const double step = 20;

        var newSize = CurrentGridCellSize + (delta > 0 ? step : -step);
        CurrentGridCellSize = Math.Clamp(newSize, minSize, maxSize);
        
        // When cell size changes, recalculate page size if grid dimensions are known
        if (_gridWidth > 0 && _gridHeight > 0)
        {
            SetGridDimensions(_gridWidth, _gridHeight);
        }
    }

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

            await _scanService.ScanAsync(progress, ct);
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
}
