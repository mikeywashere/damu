using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DamYou.Data.Pipeline;
using System.Collections.ObjectModel;

namespace DamYou.ViewModels;

public sealed partial class RunningTasksViewModel : ObservableObject
{
    private readonly IPipelineTaskRepository _taskRepository;
    private CancellationTokenSource? _refreshCts;

    public ObservableCollection<PipelineTaskDisplayItem> Tasks { get; } = new();

    [ObservableProperty]
    private int _totalQueued;

    [ObservableProperty]
    private int _totalRunning;

    public RunningTasksViewModel(IPipelineTaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
        Tasks.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));
    }

    public bool IsEmpty => Tasks.Count == 0;

    [RelayCommand]
    private async Task InitializeAsync()
    {
        // Initial load
        await LoadTasksAsync();

        // Start background refresh every 500ms
        _refreshCts = new CancellationTokenSource();
        _ = RefreshTasksPeriodicAsync(_refreshCts.Token);
    }

    [RelayCommand]
    private async Task LoadTasksAsync(CancellationToken ct = default)
    {
        try
        {
            var activeTasks = await _taskRepository.GetActiveTasksAsync(ct);

            Tasks.Clear();
            foreach (var task in activeTasks)
                Tasks.Add(PipelineTaskDisplayItem.From(task));

            TotalQueued = activeTasks.Count(t => t.Status == PipelineTaskStatus.Queued);
            TotalRunning = activeTasks.Count(t => t.Status == PipelineTaskStatus.Running);
        }
        catch (OperationCanceledException)
        {
            // expected on cancellation
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading tasks: {ex.Message}");
        }
    }

    private async Task RefreshTasksPeriodicAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(500, ct);
                await LoadTasksAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            // expected when view is unloaded
        }
    }

    public void Cleanup()
    {
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
    }
}
