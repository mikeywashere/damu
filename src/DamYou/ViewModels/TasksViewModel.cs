using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DamYou.Data.Pipeline;
using System.Collections.ObjectModel;

namespace DamYou.ViewModels;

public sealed class PipelineTaskDisplayItem
{
    public int Id { get; init; }
    public string TaskName { get; init; } = string.Empty;
    public string StatusText { get; init; } = string.Empty;
    public int? PhotoId { get; init; }
    public DateTime CreatedAt { get; init; }
    public bool IsRunningOrQueued { get; init; }

    // Progress tracking
    public string? CurrentItemName { get; init; }
    public int CurrentItemIndex { get; init; }
    public int TotalItems { get; init; }

    public double ProgressPercentage => TotalItems > 0 ? (CurrentItemIndex * 100.0) / TotalItems : 0;

    public string ProgressText => TotalItems > 0
        ? $"{CurrentItemIndex} of {TotalItems}"
        : string.Empty;

    public static PipelineTaskDisplayItem From(DamYou.Data.Entities.PipelineTask task) => new()
    {
        Id = task.Id,
        TaskName = task.TaskName,
        StatusText = task.Status switch
        {
            PipelineTaskStatus.Queued => "⏳ Queued",
            PipelineTaskStatus.Running => "🔄 Running",
            PipelineTaskStatus.Completed => "✅ Completed",
            PipelineTaskStatus.Failed => "❌ Failed",
            _ => task.Status.ToString()
        },
        PhotoId = task.PhotoId,
        CreatedAt = task.CreatedAt,
        IsRunningOrQueued = task.Status is PipelineTaskStatus.Queued or PipelineTaskStatus.Running,
        CurrentItemName = task.CurrentItemName,
        CurrentItemIndex = task.CurrentItemIndex,
        TotalItems = task.TotalItems
    };
}

public sealed partial class TasksViewModel : ObservableObject
{
    private readonly IPipelineTaskRepository _taskRepository;

    public ObservableCollection<PipelineTaskDisplayItem> Tasks { get; } = new();

    [ObservableProperty]
    private int _totalQueued;

    [ObservableProperty]
    private int _totalRunning;

    public bool IsEmpty => Tasks.Count == 0;

    public TasksViewModel(IPipelineTaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
        Tasks.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private async Task LoadTasksAsync()
    {
        var activeTasks = await _taskRepository.GetActiveTasksAsync();

        Tasks.Clear();
        foreach (var task in activeTasks)
            Tasks.Add(PipelineTaskDisplayItem.From(task));

        TotalQueued = activeTasks.Count(t => t.Status == PipelineTaskStatus.Queued);
        TotalRunning = activeTasks.Count(t => t.Status == PipelineTaskStatus.Running);
    }
}
