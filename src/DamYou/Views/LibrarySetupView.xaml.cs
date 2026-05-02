using DamYou.ViewModels;

namespace DamYou.Views;

public partial class LibrarySetupModal : ContentPage
{
    public LibrarySetupModal(LibrarySetupViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        
        // When setup completes, close modal and navigate to library view
        viewModel.PropertyChanged += async (s, e) =>
        {
            if (e.PropertyName == nameof(LibrarySetupViewModel.IsComplete) && viewModel.IsComplete)
            {
                await Navigation.PopModalAsync();
                if (Application.Current is App app)
                {
                    await app.NavigateAfterSetupAsync();
                }
            }
        };
    }

    /// <summary>
    /// Cancel button closes the modal without making changes.
    /// </summary>
    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}
