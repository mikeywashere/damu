using DamYou.ViewModels;

namespace DamYou.Views;

public partial class FoldersView : ContentPage
{
    private readonly FoldersViewModel _viewModel;

    public FoldersView(FoldersViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        await _viewModel.InitializeCommand.ExecuteAsync(null);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
        
        // Visual feedback when dragging over
        if (sender is Border border)
        {
            border.BackgroundColor = AppTheme == AppTheme.Light 
                ? Color.FromArgb("#ECECEC") 
                : Color.FromArgb("#2A2A2A");
            border.StrokeThickness = 3;
        }
    }

    private async void OnDrop(object? sender, DropEventArgs e)
    {
        if (sender is Border border)
        {
            border.BackgroundColor = AppTheme == AppTheme.Light 
                ? Color.FromArgb("#F9F9F9") 
                : Color.FromArgb("#1A1A1A");
            border.StrokeThickness = 2;
        }

        if (e.Data.Properties.ContainsKey("text/uri-list"))
        {
            var uriList = e.Data.Properties["text/uri-list"];
            if (uriList is string uris)
            {
                var paths = uris.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(uri => uri.Trim())
                    .Where(uri => !string.IsNullOrWhiteSpace(uri))
                    .ToList();

                if (paths.Any())
                {
                    await _viewModel.AddFoldersCommand.ExecuteAsync(paths);
                }
            }
        }
    }
}
