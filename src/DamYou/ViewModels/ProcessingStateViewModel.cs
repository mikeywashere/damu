using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DamYou.Data.Analysis;
using DamYou.Services;

namespace DamYou.ViewModels;

/// <summary>
/// Shared processing state ViewModel — observed by the status bar UI.
/// Subscribes to events from IProcessingStateService to receive state transitions and progress updates.
/// Decoupled from the background worker implementation.
/// Thread-safe: All property updates use MainThread.BeginInvokeOnMainThread() to marshal to UI thread.
/// </summary>
public sealed partial class ProcessingStateViewModel : ObservableObject
{
    private readonly IProcessingWorker _processingWorker;
    private readonly IProcessingStateService _processingStateService;
    private readonly Action<Action> _dispatcher;
    private bool _isProcessingNow = false;

    [ObservableProperty]
    private bool isProcessing = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    private int currentProgress = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    private int totalItems = 0;

    [ObservableProperty]
    private string statusText = "Ready";

    // Queue state properties (updated by QueueProcessorService via IProcessingStateService)
    [ObservableProperty]
    private int folderQueueCount = 0;

    [ObservableProperty]
    private int fileQueueCount = 0;

    /// <summary>
    /// Truncated display of the item currently being processed (center ellipsis, max ~40 chars).
    /// </summary>
    [ObservableProperty]
    private string currentItemShort = string.Empty;

    /// <summary>
    /// Full path of the item currently being processed (for tooltip binding).
    /// </summary>
    [ObservableProperty]
    private string currentItemFull = string.Empty;

    /// <summary>
    /// Which queue is active: "Folders", "Files", "Both", or "Idle".
    /// </summary>
    [ObservableProperty]
    private string activeQueue = "Idle";

    /// <summary>
    /// Computed property: Display text like "Processing: 5/42 items"
    /// </summary>
    public string ProgressText =>
        TotalItems == 0 ? "Idle" : $"{CurrentProgress}/{TotalItems}";

    public ProcessingStateViewModel(IProcessingWorker processingWorker, IProcessingStateService processingStateService, Action<Action>? dispatcher = null)
    {
        _processingWorker = processingWorker;
        _processingStateService = processingStateService;
        _dispatcher = dispatcher ?? (action =>
        {
            try { MainThread.BeginInvokeOnMainThread(action); }
            catch { action(); } // test / non-MAUI context: invoke directly
        });

        // Subscribe to processing state events
        _processingStateService.ProcessingStarted += OnProcessingStarted;
        _processingStateService.ProcessingStopped += OnProcessingStopped;
        _processingStateService.ProgressReported += OnProgressReported;
        _processingStateService.QueueCountsChanged += OnQueueCountsChanged;
    }

    /// <summary>
    /// Manual trigger to start processing. Can be called from UI button click.
    /// Prevents double-triggering by checking _isProcessingNow flag.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStartProcessing))]
    private async Task StartProcessingAsync()
    {
        if (_isProcessingNow)
            return;

        _isProcessingNow = true;
        StatusText = "Starting pipeline...";
        
        try
        {
            await _processingWorker.TriggerProcessingAsync();
        }
        finally
        {
            _isProcessingNow = false;
        }
    }

    private bool CanStartProcessing => !IsProcessing && !_isProcessingNow;

    /// <summary>
    /// Invoked when the worker reports progress.
    /// Marshals updates to the main thread for safe UI binding.
    /// </summary>
    private void OnProgressReported(AnalysisProgress progress)
    {
        _dispatcher(() =>
        {
            CurrentProgress = progress.Completed;
            TotalItems = progress.Total;
            
            // Update status text based on current file or pass
            StatusText = progress.CurrentFile != null
                ? $"Processing: {Path.GetFileName(progress.CurrentFile)}"
                : $"Processing {progress.CurrentPass}...";
        });
    }

    /// <summary>
    /// Invoked when the worker starts processing.
    /// </summary>
    private void OnProcessingStarted(int totalCount)
    {
        _dispatcher(() =>
        {
            IsProcessing = true;
            CurrentProgress = 0;
            TotalItems = totalCount;
            StatusText = "Processing photos...";
            StartProcessingCommand.NotifyCanExecuteChanged();
        });
    }

    /// <summary>
    /// Invoked when the worker stops or completes processing.
    /// </summary>
    private void OnProcessingStopped()
    {
        _dispatcher(() =>
        {
            IsProcessing = false;
            StatusText = "Complete";
            StartProcessingCommand.NotifyCanExecuteChanged();
        });
    }

    /// <summary>
    /// Invoked when QueueProcessorService reports updated queue counts.
    /// </summary>
    private void OnQueueCountsChanged(int folderCount, int fileCount, string? currentItem, string activeQueueName)
    {
        _dispatcher(() =>
        {
            FolderQueueCount = folderCount;
            FileQueueCount = fileCount;
            ActiveQueue = activeQueueName;
            CurrentItemFull = currentItem ?? string.Empty;
            CurrentItemShort = currentItem is not null
                ? TruncateCenter(currentItem, 40)
                : string.Empty;
        });
    }

    /// <summary>
    /// Truncates a long string with a center ellipsis so both the start and end are visible.
    /// E.g., "C:\...\filename.jpg" for long paths.
    /// </summary>
    private static string TruncateCenter(string value, int maxLength)
    {
        if (value.Length <= maxLength)
            return value;

        int half = (maxLength - 3) / 2;
        return string.Concat(value.AsSpan(0, half), "...", value.AsSpan(value.Length - half));
    }
}