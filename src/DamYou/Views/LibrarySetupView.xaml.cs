using DamYou.ViewModels;

namespace DamYou.Views;

public partial class LibrarySetupView : ContentPage
{
    public LibrarySetupView(LibrarySetupViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
