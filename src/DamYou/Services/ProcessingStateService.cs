using DamYou.Data.Analysis;

namespace DamYou.Services;

/// <summary>
/// Default implementation of IProcessingStateService.
/// A simple event broadcaster that allows the background worker to notify subscribers
/// of processing state changes without holding direct references.
/// Thread-safe: callers are responsible for marshaling to the UI thread.
/// </summary>
public sealed class ProcessingStateService : IProcessingStateService
{
    public event Action<int>? ProcessingStarted;
    public event Action? ProcessingStopped;
    public event Action<AnalysisProgress>? ProgressReported;
    public event Action<int, int, string?, string>? QueueCountsChanged;

    public void NotifyProcessingStarted(int totalCount)
    {
        ProcessingStarted?.Invoke(totalCount);
    }

    public void NotifyProcessingStopped()
    {
        ProcessingStopped?.Invoke();
    }

    public void NotifyProgress(AnalysisProgress progress)
    {
        ProgressReported?.Invoke(progress);
    }

    public void NotifyQueueCountsChanged(int folderCount, int fileCount, string? currentItem, string activeQueue)
    {
        QueueCountsChanged?.Invoke(folderCount, fileCount, currentItem, activeQueue);
    }
}
