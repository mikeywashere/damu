using DamYou.Models;
using DamYou.ViewModels;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

namespace DamYou.Views;

public partial class LibraryView : ContentPage
{
    private readonly LibraryViewModel _vm;
    private readonly IServiceProvider _services;

    public LibraryView(LibraryViewModel vm, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = vm;
        _vm = vm;
        _services = services;

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
    /// Creates a single photo cell with thumbnail and status indicator.
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
        if (e.PropertyName == nameof(LibraryViewModel.CurrentGridCellSize))
        {
            RefreshGridDisplay();
        }
    }

    /// <summary>
    /// Placeholder for processing status filter changes.
    /// </summary>
    private void OnProcessingStatusChanged(object? sender, EventArgs e)
    {
        // TODO: Implement filtering by processing status
    }

    /// <summary>
    /// Placeholder for sort order changes.
    /// </summary>
    private void OnSortChanged(object? sender, EventArgs e)
    {
        // TODO: Implement sorting by different criteria
    }
}
