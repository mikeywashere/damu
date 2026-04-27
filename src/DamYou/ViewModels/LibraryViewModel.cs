using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DamYou.Data.Pipeline;
using DamYou.Views;
using System.Collections.ObjectModel;

namespace DamYou.ViewModels;

public sealed partial class LibraryViewModel : ObservableObject
{
    private readonly ILibraryScanService _scanService;
    private readonly IPipelineTaskRepository _taskRepository;
    private readonly IServiceProvider _services;

    public ObservableCollection<string> Photos { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScanProgressText))]
    private bool _isScanning;

    [ObservableProperty]
    private int _queueDepth;

    [ObservableProperty]
    private string? _scanCurrentFolder;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScanProgressText))]
    private int _scanDiscovered;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScanProgressText))]
    private int _scanEnqueued;

    public string ScanProgressText =>
        IsScanning && ScanDiscovered == 0
            ? "Scanning…"
            : $"Discovered {ScanDiscovered} files, enqueued {ScanEnqueued}";

    public LibraryViewModel(ILibraryScanService scanService, IPipelineTaskRepository taskRepository, IServiceProvider services)
    {
        _scanService = scanService;
        _taskRepository = taskRepository;
        _services = services;
    }

    [RelayCommand]
    private async Task RescanLibraryAsync(CancellationToken ct)
    {
        IsScanning = true;
        ScanDiscovered = 0;
        ScanEnqueued = 0;
        ScanCurrentFolder = null;

        try
        {
            var progress = new Progress<ScanProgress>(p =>
            {
                ScanDiscovered = p.TotalDiscovered;
                ScanEnqueued = p.Enqueued;
                ScanCurrentFolder = p.CurrentFolder;
            });

            await _scanService.ScanAsync(progress, ct);
            await RefreshQueueDepthAsync();
        }
        catch (OperationCanceledException)
        {
            // expected on cancellation
        }
        catch (Exception)
        {
            // surface via future error handling
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private async Task ViewTasksAsync()
    {
        var tasksView = _services.GetRequiredService<TasksView>();
        if (Application.Current?.Windows.Count > 0 && Application.Current.Windows[0].Page is NavigationPage nav)
            await nav.PushAsync(tasksView);
    }

    [RelayCommand]
    private async Task RefreshQueueDepthAsync()
    {
        QueueDepth = await _taskRepository.GetQueueDepthAsync();
    }
}
