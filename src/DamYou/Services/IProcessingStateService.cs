using DamYou.Data.Analysis;

namespace DamYou.Services;

/// <summary>
/// Service that mediates processing state changes.
/// Decouples the background worker from the ViewModel using an event-based pattern.
/// The service is responsible for notifying subscribers of state transitions.
/// </summary>
public interface IProcessingStateService
{
    /// <summary>
    /// Invoked when processing starts. Args: total items to process.
    /// </summary>
    event Action<int>? ProcessingStarted;

    /// <summary>
    /// Invoked when processing stops.
    /// </summary>
    event Action? ProcessingStopped;

    /// <summary>
    /// Invoked when processing progress is reported.
    /// </summary>
    event Action<AnalysisProgress>? ProgressReported;

    /// <summary>
    /// Invoked by QueueProcessorService when queue counts change.
    /// Args: (folderCount, fileCount, currentItem, activeQueue).
    /// </summary>
    event Action<int, int, string?, string>? QueueCountsChanged;

    /// <summary>
    /// Notifies that processing has started.
    /// </summary>
    void NotifyProcessingStarted(int totalCount);

    /// <summary>
    /// Notifies that processing has stopped.
    /// </summary>
    void NotifyProcessingStopped();

    /// <summary>
    /// Notifies of progress during processing.
    /// </summary>
    void NotifyProgress(AnalysisProgress progress);

    /// <summary>
    /// Notifies that queue counts have changed.
    /// </summary>
    void NotifyQueueCountsChanged(int folderCount, int fileCount, string? currentItem, string activeQueue);
}

