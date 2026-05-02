using DamYou.ViewModels;

namespace DamYou.Views;

public partial class TabbedView : ContentPage
{
    private readonly TabbedViewModel _viewModel;

    public TabbedView(TabbedViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        if (_viewModel is IAsyncInitialize asyncInit)
        {
            await asyncInit.InitializeAsync();
        }
    }
}
