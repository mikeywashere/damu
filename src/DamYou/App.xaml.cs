using DamYou.Data.Repositories;
using DamYou.ViewModels;
using DamYou.Views;

namespace DamYou;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    /// <summary>
    /// Service provider for DI resolution across the app.
    /// </summary>
    public IServiceProvider Services => _services;

    /// <summary>
    /// Static reference to the shared ProcessingStateViewModel.
    /// Used by views to bind to processing status.
    /// </summary>
    public static ProcessingStateViewModel? ProcessingState { get; private set; }

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;

        // Cache the ProcessingStateViewModel for XAML binding via x:Static
        ProcessingState = services.GetRequiredService<ProcessingStateViewModel>();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Always start with splash screen
        var splashPage = _services.GetRequiredService<SplashScreenView>();
        var window = new Window(new NavigationPage(splashPage))
        {
            Title = "DAMu",
            MinimumWidth = 800,
            MinimumHeight = 600,
        };

        // After 2 seconds, transition to Shell with tabs
        _ = SplashTransitionAsync();

        return window;
    }

    /// <summary>
    /// Schedules the transition from splash screen after 2 seconds.
    /// Separated to ensure proper async handling and error management.
    /// </summary>
    private async Task SplashTransitionAsync()
    {
        try
        {
            await Task.Delay(2000);
            await NavigateFromSplashAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error during splash transition: {ex.Message}");
        }
    }

    /// <summary>
    /// Navigate from splash screen to AppShell with tabs.
    /// Replaces the entire window with the Shell-based tabbed interface.
    /// </summary>
    private async Task NavigateFromSplashAsync()
    {
        try
        {
            var folderRepository = _services.GetRequiredService<IFolderRepository>();
            var folders = await folderRepository.GetActiveFoldersAsync();

            // Get the AppShell and set it as the main shell
            var appShell = _services.GetRequiredService<AppShell>();
            Application.Current!.MainPage = appShell;

            // Route to the appropriate tab: folders if no folders configured, else gallery
            var route = folders.Count == 0 ? "folders" : "gallery";
            await Shell.Current.GoToAsync(route);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error navigating from splash: {ex.Message}");
        }
    }

    public async Task NavigateAfterSetupAsync()
    {
        // Legacy method kept for compatibility with LibrarySetupModal
        // In tabbed navigation, this just closes the modal and returns to TabbedView
        await Task.CompletedTask;
    }
}