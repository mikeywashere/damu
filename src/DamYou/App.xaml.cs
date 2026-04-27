using DamYou.Data.Repositories;
using DamYou.Views;

namespace DamYou;

public partial class App : Application
{
    private readonly IFolderRepository _folderRepository;
    private readonly IServiceProvider _services;

    public App(IFolderRepository folderRepository, IServiceProvider services)
    {
        InitializeComponent();
        _folderRepository = folderRepository;
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new NavigationPage(_services.GetRequiredService<LibrarySetupView>()))
        {
            Title = "dam-you",
            MinimumWidth = 800,
            MinimumHeight = 600,
        };
    }

    public async Task NavigateAfterSetupAsync()
    {
        var folders = await _folderRepository.GetActiveFoldersAsync();
        var targetPage = folders.Count > 0
            ? (Page)_services.GetRequiredService<LibraryView>()
            : _services.GetRequiredService<LibrarySetupView>();

        if (Windows.Count > 0 && Windows[0].Page is NavigationPage nav)
        {
            await nav.PushAsync(targetPage);
        }
    }
}