using DamYou.ViewModels;

namespace DamYou.Views;

public partial class RunningTasksView : ContentPage
{
    private readonly RunningTasksViewModel _viewModel;

    public RunningTasksView(RunningTasksViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        await _viewModel.InitializeCommand.ExecuteAsync(null);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.Cleanup();
    }
}
