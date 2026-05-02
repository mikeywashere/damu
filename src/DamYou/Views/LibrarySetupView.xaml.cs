using DamYou.ViewModels;

namespace DamYou.Views;

public partial class LibrarySetupModal : ContentPage
{
    public LibrarySetupModal(LibrarySetupViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    /// <summary>
    /// Cancel button closes the modal without making changes.
    /// </summary>
    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }

    /// <summary>
    /// Done button closes the modal. ViewModel should sync folder state on closing.
    /// </summary>
    private async void OnDoneClicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}
