namespace DamYou.Views;

/// <summary>
/// Status bar component — displays processing state and provides action buttons.
/// Data binding is handled via BindingContext set in XAML (App.ProcessingState).
/// </summary>
public partial class StatusBar : Grid
{
    public StatusBar()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Opens the folder management modal when "Folders" button is clicked.
    /// </summary>
    private async void OnFoldersClicked(object? sender, EventArgs e)
    {
        // Resolve LibrarySetupModal from the app's service provider (cast Application to App)
        var app = (App)Application.Current!;
        var modal = app.Services.GetRequiredService<LibrarySetupModal>();
        await Application.Current!.MainPage!.Navigation.PushModalAsync(modal);
    }
}