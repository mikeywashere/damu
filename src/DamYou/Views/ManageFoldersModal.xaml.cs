using DamYou.Data.Pipeline;
using DamYou.ViewModels;

namespace DamYou.Views;

public partial class ManageFoldersModal : ContentPage
{
    private readonly ManageFoldersViewModel _vm;
    private readonly ILibraryScanService _scanService;

    public ManageFoldersModal(ManageFoldersViewModel vm, ILibraryScanService scanService)
    {
        InitializeComponent();
        BindingContext = vm;
        _vm = vm;
        _scanService = scanService;
    }

    private async void OnPageLoaded(object? sender, EventArgs e)
    {
        await _vm.InitializeCommand.ExecuteAsync(null);
    }

    private async void OnDoneClicked(object? sender, EventArgs e)
    {
        // Trigger a library scan to index photos from newly added folders
        try
        {
            var progress = new Progress<ScanProgress>(p =>
            {
                // Could show progress here if needed
            });
            await _scanService.ScanAsync(progress);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error scanning library: {ex}");
        }
        
        await Navigation.PopAsync();
    }
}

