using DamYou.Models;
using DamYou.ViewModels;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

namespace DamYou.Views;

public partial class GalleryView : ContentPage
{
    private readonly GalleryViewModel _vm;

    public GalleryView(GalleryViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        _vm = vm;

        // Set up collection change notification
        _vm.GridPhotos.CollectionChanged += OnGridPhotosChanged;
        _vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    /// <summary>
    /// Called when the page loads to initialize the grid with photos.
    /// </summary>
    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        await _vm.InitializeCommand.ExecuteAsync(null);
        PopulateGrid();
    }

    /// <summary>
    /// Handles grid size changes to recalculate pagination based on viewport.
    /// </summary>
    private void OnGridViewSizeChanged(object? sender, EventArgs e)
    {
        if (sender is Grid gridView && gridView.Width > 0 && gridView.Height > 0)
        {
            _vm.SetGridDimensions(gridView.Width, gridView.Height);
        }
    }

    /// <summary>
    /// Handles scroll events to load more photos when nearing the bottom.
    /// </summary>
    private async void OnGridScrolled(object? sender, ScrolledEventArgs e)
    {
        if (GridScrollView == null || PhotoGrid == null)
            return;

        // Check if we're near the bottom (within 200 points)
        double scrollPosition = e.ScrollY;
        double scrollHeight = GridScrollView.ContentSize.Height;
        double viewportHeight = GridScrollView.Height;
        
        if (scrollHeight > 0 && scrollPosition > scrollHeight - viewportHeight - 200)
        {
            // User is near the bottom, try to load more
            if (_vm.CanLoadMore && !_vm.IsLoadingMore)
            {
                await _vm.LoadMorePhotosCommand.ExecuteAsync(null);
            }
        }
    }

    /// <summary>
    /// Populates the FlexLayout grid with photo cells.
    /// </summary>
    private void PopulateGrid()
    {
        PhotoGrid.Children.Clear();
        foreach (var item in _vm.GridPhotos)
        {
            var cell = CreatePhotoCell(item);
            PhotoGrid.Children.Add(cell);
        }
    }

    /// <summary>
    /// Creates a single photo cell with thumbnail, status indicator, and interactive features.
    /// </summary>
    private Border CreatePhotoCell(PhotoGridItem gridItem)
    {
        var cellSize = _vm.CurrentGridCellSize;

        var border = new Border
        {
            Stroke = Colors.Transparent,
            Padding = new Thickness(4),
            Margin = new Thickness(4),
            WidthRequest = cellSize,
            HeightRequest = cellSize,
            BackgroundColor = Colors.White
        };
        border.StrokeShape = new RoundRectangle
        {
            CornerRadius = new CornerRadius(4)
        };

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            },
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            }
        };

        // Thumbnail image with fallback to placeholder
        var image = new Image
        {
            Aspect = Aspect.AspectFill,
            BackgroundColor = Colors.LightGray
        };
        image.SetBinding(Image.SourceProperty, new Binding("FilePath", BindingMode.OneWay, 
            converter: new DamYou.Converters.FilePathToImageSourceConverter()));
        
        // Add click handler to open modal
        var tapGestureRecognizer = new TapGestureRecognizer();
        tapGestureRecognizer.Tapped += (s, e) => OnPhotoClicked(gridItem);
        image.GestureRecognizers.Add(tapGestureRecognizer);
        
        // Add tooltip with metadata: filename, date, resolution
        var tooltipText = BuildPhotoTooltip(gridItem);
        ToolTipProperties.SetText(image, tooltipText);
        
        grid.Add(image);

        // Status indicator overlay
        var statusOverlay = new Grid
        {
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start,
            Padding = new Thickness(4),
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition { Width = new GridLength(20) }
            },
            RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition { Height = new GridLength(20) }
            }
        };

        var statusBgBorder = new Border
        {
            Stroke = Colors.Transparent,
            Padding = new Thickness(2),
            WidthRequest = 20,
            HeightRequest = 20,
            BackgroundColor = Colors.White,
            IsVisible = !string.IsNullOrEmpty(gridItem.ProcessingStatusIcon)
        };
        statusBgBorder.StrokeShape = new RoundRectangle
        {
            CornerRadius = new CornerRadius(10)
        };

        var statusLabel = new Label
        {
            Text = gridItem.ProcessingStatusIcon ?? string.Empty,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            TextColor = Colors.White
        };

        statusBgBorder.Content = statusLabel;
        statusOverlay.Add(statusBgBorder);
        grid.Add(statusOverlay);
        border.Content = grid;
        border.BindingContext = gridItem;

        // Bind the background color to the status color
        statusBgBorder.BindingContext = gridItem;
        statusBgBorder.SetBinding(Border.BackgroundColorProperty, "ProcessingStatusColor");

        return border;
    }

    /// <summary>
    /// Refreshes grid display when size changes.
    /// </summary>
    private void RefreshGridDisplay()
    {
        PopulateGrid();
    }

    /// <summary>
    /// Handles collection changes to add new items.
    /// </summary>
    private void OnGridPhotosChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems?.Count > 0)
        {
            foreach (PhotoGridItem item in e.NewItems)
            {
                var cell = CreatePhotoCell(item);
                PhotoGrid.Children.Add(cell);
            }
        }
        else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
        {
            PopulateGrid();
        }
    }

    /// <summary>
    /// Watches for grid size changes to refresh layout.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GalleryViewModel.CurrentGridCellSize))
        {
            RefreshGridDisplay();
        }
    }

    private void OnProcessingStatusChanged(object? sender, EventArgs e)
    {
        // TODO: Implement filtering by processing status
    }

    private void OnSortChanged(object? sender, EventArgs e)
    {
        // TODO: Implement sorting by different criteria
    }

    /// <summary>
    /// Called when a photo thumbnail is clicked.
    /// Opens a modal to view the full-size image.
    /// </summary>
    private async void OnPhotoClicked(PhotoGridItem gridItem)
    {
        await ShowPhotoModalAsync(gridItem);
    }

    /// <summary>
    /// Shows a modal dialog with the full-size photo.
    /// </summary>
    private async Task ShowPhotoModalAsync(PhotoGridItem photo)
    {
        var modalPage = new ContentPage
        {
            BackgroundColor = Colors.Black
        };

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitionCollection { new RowDefinition { Height = new GridLength(1, GridUnitType.Star) } },
            ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) } }
        };

        // Full-size image
        var image = new Image
        {
            Aspect = Aspect.AspectFit,
            HorizontalOptions = LayoutOptions.FillAndExpand,
            VerticalOptions = LayoutOptions.FillAndExpand
        };
        image.SetBinding(Image.SourceProperty, new Binding("FilePath", BindingMode.OneWay,
            converter: new DamYou.Converters.FilePathToImageSourceConverter()));
        image.BindingContext = photo;

        grid.Add(image);

        // Close button (X) in top-right corner
        var closeButton = new Button
        {
            Text = "✕",
            FontSize = 24,
            WidthRequest = 50,
            HeightRequest = 50,
            CornerRadius = 25,
            Padding = 0,
            Margin = new Thickness(12),
            BackgroundColor = Colors.Transparent,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start
        };
        closeButton.Clicked += async (s, e) => await modalPage.Navigation.PopModalAsync();

        var overlay = new Grid
        {
            RowDefinitions = new RowDefinitionCollection { new RowDefinition { Height = new GridLength(1, GridUnitType.Star) } },
            ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) } },
            Padding = 0
        };

        overlay.Add(grid);
        overlay.Add(closeButton);

        modalPage.Content = overlay;

        // Add keyboard support for ESC key
        modalPage.Loaded += (s, e) =>
        {
            // Platform-specific keyboard handling would go here
        };

        await Navigation.PushModalAsync(modalPage);
    }

    /// <summary>
    /// Builds a tooltip string with photo metadata: filename, date modified, and resolution.
    /// </summary>
    private string BuildPhotoTooltip(PhotoGridItem photo)
    {
        var tooltipParts = new List<string>();

        if (!string.IsNullOrEmpty(photo.FileName))
            tooltipParts.Add($"📄 {System.IO.Path.GetFileName(photo.FileName)}");

        if (photo.DateTaken.HasValue)
            tooltipParts.Add($"📅 {photo.DateTaken:yyyy-MM-dd HH:mm}");
        else if (photo.DateIndexed != default)
            tooltipParts.Add($"📅 {photo.DateIndexed:yyyy-MM-dd HH:mm}");

        if (photo.Width.HasValue && photo.Height.HasValue)
            tooltipParts.Add($"📐 {photo.Width}×{photo.Height}");

        return string.Join("\n", tooltipParts);
    }
}
