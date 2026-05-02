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
}
