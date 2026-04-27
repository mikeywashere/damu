using DamYou.ViewModels;

namespace DamYou.Views;

public partial class LibraryView : ContentPage
{
    public LibraryView(LibraryViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
