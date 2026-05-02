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

    /// <summary>
    /// Computed property: Display text like "Processing: 5/42 items"
    /// </summary>
    public string ProgressText =>
        TotalItems == 0 ? "Idle" : $"{CurrentProgress}/{TotalItems}";

    public ProcessingStateViewModel(IProcessingWorker processingWorker, IProcessingStateService processingStateService)
    {
        _processingWorker = processingWorker;
        _processingStateService = processingStateService;

        // Subscribe to processing state events
        _processingStateService.ProcessingStarted += OnProcessingStarted;
        _processingStateService.ProcessingStopped += OnProcessingStopped;
        _processingStateService.ProgressReported += OnProgressReported;
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
        MainThread.BeginInvokeOnMainThread(() =>
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
        MainThread.BeginInvokeOnMainThread(() =>
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
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsProcessing = false;
            StatusText = "Complete";
            StartProcessingCommand.NotifyCanExecuteChanged();
        });
    }
}
