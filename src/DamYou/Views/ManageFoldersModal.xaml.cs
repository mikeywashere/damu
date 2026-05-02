using DamYou.Data.Pipeline;
using DamYou.ViewModels;

namespace DamYou.Views;

public partial class ManageFoldersModal : ContentPage
{
    private readonly ManageFoldersViewModel _vm;

    public ManageFoldersModal(ManageFoldersViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        _vm = vm;
    }

    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        await _vm.InitializeCommand.ExecuteAsync(null);
    }

    protected override bool OnBackButtonPressed()
    {
        // Execute the back command which triggers scan and navigation
        _vm.BackCommand.Execute(null);
        return true;
    }
}

