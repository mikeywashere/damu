using DamYou.Data.Repositories;
using DamYou.Views;
using DamYou.ViewModels;

namespace DamYou;

public partial class App : Application
{
    private readonly IFolderRepository _folderRepository;
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

    public App(IFolderRepository folderRepository, IServiceProvider services)
    {
        InitializeComponent();
        _folderRepository = folderRepository;
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
            Title = "dam-you",
            MinimumWidth = 800,
            MinimumHeight = 600,
        };

        // After 2 seconds, transition to the main page
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(2000);
            await NavigateFromSplashAsync();
        });

        return window;
    }

    /// <summary>
    /// Navigate from splash screen to the appropriate main page (LibraryView or setup).
    /// Called after 2-second splash delay.
    /// </summary>
    private async Task NavigateFromSplashAsync()
    {
        try
        {
            var folders = await _folderRepository.GetActiveFoldersAsync();
            var targetPage = folders.Count > 0
                ? (Page)_services.GetRequiredService<LibraryView>()
                : _services.GetRequiredService<LibrarySetupModal>();

            if (Windows.Count > 0 && Windows[0].Page is NavigationPage nav)
            {
                await nav.PushAsync(targetPage);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error navigating from splash: {ex.Message}");
        }
    }

    public async Task NavigateAfterSetupAsync()
    {
        var folders = await _folderRepository.GetActiveFoldersAsync();
        var targetPage = folders.Count > 0
            ? (Page)_services.GetRequiredService<LibraryView>()
            : _services.GetRequiredService<LibrarySetupModal>();

        if (Windows.Count > 0 && Windows[0].Page is NavigationPage nav)
        {
            await nav.PushAsync(targetPage);
        }
    }
}