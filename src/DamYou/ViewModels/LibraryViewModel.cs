using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace DamYou.ViewModels;

public sealed partial class LibraryViewModel : ObservableObject
{
    public ObservableCollection<string> Photos { get; } = new();

    [RelayCommand]
    private Task RefreshAsync() => Task.CompletedTask; // stub
}
