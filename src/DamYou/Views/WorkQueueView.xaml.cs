using DamYou.ViewModels;

namespace DamYou.Views;

public partial class WorkQueueView : ContentPage
{
    private readonly WorkQueueViewModel _viewModel;

    public WorkQueueView(WorkQueueViewModel viewModel)
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
