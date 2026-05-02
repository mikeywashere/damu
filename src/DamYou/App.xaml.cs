using DamYou.Data.Repositories;
using DamYou.Services;
using DamYou.ViewModels;
using DamYou.Views;
using Serilog;

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
        Log.Information("App constructor: Initializing component...");
        InitializeComponent();
        _services = services;

        Log.Debug("App constructor: Resolving ProcessingStateViewModel...");
        // Cache the ProcessingStateViewModel for XAML binding via x:Static
        ProcessingState = services.GetRequiredService<ProcessingStateViewModel>();
        Log.Information("App constructor: Initialization complete");
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        Log.Information("CreateWindow: Starting splash screen presentation...");
        var startTime = DateTime.UtcNow;
        
        // Start with splash screen (wrapped in NavigationPage temporarily)
        Log.Debug("CreateWindow: Resolving SplashScreenView...");
        var splashPage = _services.GetRequiredService<SplashScreenView>();
        var navPage = new NavigationPage(splashPage);
        
        // Register handler for page unloaded to flush logs on app shutdown
        navPage.Unloaded += OnPageUnloaded;
        
        var window = new Window(navPage)
        {
            Title = "DAMu",
            MinimumWidth = 800,
            MinimumHeight = 600,
        };

        Log.Information("CreateWindow: Window created with splash screen");
        // After 2 seconds, transition to AppShell (NOT wrapped in NavigationPage)
        _ = SplashTransitionAsync(startTime);

        return window;
    }

    /// <summary>
    /// Handles page unloaded event by flushing and closing the logger.
    /// Ensures all buffered log entries are written to disk before app termination.
    /// </summary>
    private void OnPageUnloaded(object? sender, EventArgs e)
    {
        Log.Information("App is unloading, flushing logs...");
        LoggingService.CloseAndFlush();
    }

    /// <summary>
    /// Schedules the transition from splash screen after 2 seconds.
    /// Separated to ensure proper async handling and error management.
    /// </summary>
    private async Task SplashTransitionAsync(DateTime splashStartTime)
    {
        try
        {
            Log.Debug("SplashTransitionAsync: Waiting 2 seconds before transition...");
            await Task.Delay(2000);
            Log.Debug("SplashTransitionAsync: Delay complete, navigating from splash...");
            await NavigateFromSplashAsync(splashStartTime);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during splash transition");
        }
    }

    /// <summary>
    /// Navigate from splash screen to AppShell with tabs.
    /// Replaces the entire window with the Shell-based tabbed interface.
    /// AppShell must NOT be wrapped in NavigationPage.
    /// </summary>
    private async Task NavigateFromSplashAsync(DateTime splashStartTime)
    {
        try
        {
            Log.Debug("NavigateFromSplashAsync: Resolving folder repository...");
            var folderRepository = _services.GetRequiredService<IFolderRepository>();
            var folders = await folderRepository.GetActiveFoldersAsync();
            Log.Debug("NavigateFromSplashAsync: Found {FolderCount} active folders", folders.Count);

            // Replace the window's page with AppShell directly (NOT wrapped in NavigationPage)
            Log.Debug("NavigateFromSplashAsync: Resolving AppShell...");
            var appShell = _services.GetRequiredService<AppShell>();
            
            // Initialize tab content BEFORE assigning to MainPage to avoid platform rendering errors
            Log.Debug("NavigateFromSplashAsync: Initializing AppShell tab content...");
            appShell.InitializeTabContent(_services);
            
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Application.Current!.MainPage = appShell;
                Log.Debug("NavigateFromSplashAsync: MainPage set to AppShell");
            });

            // Route to the appropriate tab: folders if no folders configured, else gallery
            var route = folders.Count == 0 ? "folders" : "gallery";
            Log.Debug("NavigateFromSplashAsync: Navigating to route: {Route}", route);
            await Shell.Current.GoToAsync(route);
            
            var elapsedMs = (DateTime.UtcNow - splashStartTime).TotalMilliseconds;
            Log.Information("NavigateFromSplashAsync: Splash and navigation complete. Total time: {ElapsedMs:F0}ms", elapsedMs);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error navigating from splash");
        }
    }

    public async Task NavigateAfterSetupAsync()
    {
        // Legacy method kept for compatibility with LibrarySetupModal
        // In tabbed navigation, this just closes the modal and returns to TabbedView
        await Task.CompletedTask;
    }
}