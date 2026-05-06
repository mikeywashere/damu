using DamYou.Data.Repositories;
using DamYou.Services;
using DamYou.ViewModels;
using DamYou.Views;
using Microsoft.Extensions.Hosting;
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
        
        // CRITICAL: Set MainPage immediately (synchronously) in the constructor.
        // MAUI requires MainPage to be set BEFORE app initialization completes.
        // Set it to SplashScreenView, then proceed with async operations.
        Log.Debug("App constructor: Setting MainPage to SplashScreenView (synchronous)...");
        var splashScreen = services.GetRequiredService<SplashScreenView>();
        MainPage = splashScreen;
        Log.Information("App constructor: MainPage set to SplashScreenView");
        
        Log.Debug("App constructor: Starting hosted services and splash transition...");
        var splashStartTime = DateTime.UtcNow;
        
        // CRITICAL: Use Task.Run() to ensure async execution, not just fire-and-forget
        // Fire-and-forget (_) doesn't guarantee execution in MAUI constructor context
        Task.Run(async () => await StartHostedServicesAsync());
        _ = SplashTransitionAsync(splashStartTime);
        
        Log.Information("App constructor: Initialization complete");
    }

    /// <summary>
    /// Manually start hosted services.
    /// MAUI's MauiApp does NOT automatically call StartAsync on IHostedService implementations
    /// like the .NET Generic Host does. This method bridges that gap.
    /// </summary>
    private async Task StartHostedServicesAsync()
    {
        try
        {
            Log.Information("[HOSTED_SERVICES] StartHostedServicesAsync starting...");
            
            var hostedServices = _services.GetServices<IHostedService>().ToList();
            Log.Information("[HOSTED_SERVICES] Found {Count} hosted services", hostedServices.Count);
            
            if (hostedServices.Count == 0)
            {
                Log.Warning("[HOSTED_SERVICES] No hosted services found in DI container");
                return;
            }
            
            foreach (var service in hostedServices)
            {
                var serviceName = service.GetType().Name;
                Log.Information("[HOSTED_SERVICES] About to start: {ServiceType}", serviceName);
                
                try
                {
                    await service.StartAsync(CancellationToken.None);
                    Log.Information("[HOSTED_SERVICES] Successfully started: {ServiceType}", serviceName);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[HOSTED_SERVICES] Failed to start {ServiceType}", serviceName);
                    throw;
                }
            }
            
            Log.Information("[HOSTED_SERVICES] All hosted services started successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[HOSTED_SERVICES] Error in StartHostedServicesAsync");
        }
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

            // Route to gallery tab - always show gallery, even if empty (user can use "Scan Folders" button to add folders)
            var route = "gallery";
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