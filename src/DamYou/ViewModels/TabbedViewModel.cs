using CommunityToolkit.Mvvm.ComponentModel;

namespace DamYou.ViewModels;

public interface IAsyncInitialize
{
    Task InitializeAsync();
}

public sealed partial class TabbedViewModel : ObservableObject, IAsyncInitialize
{
    [ObservableProperty]
    private int _selectedTabIndex = 0;

    public bool GalleryTabSelected => SelectedTabIndex == 0;
    public bool FoldersTabSelected => SelectedTabIndex == 1;
    public bool TasksTabSelected => SelectedTabIndex == 2;

    public async Task InitializeAsync()
    {
        // Any async initialization can happen here if needed
        await Task.CompletedTask;
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(GalleryTabSelected));
        OnPropertyChanged(nameof(FoldersTabSelected));
        OnPropertyChanged(nameof(TasksTabSelected));
    }
}
