using DamYou.Data.Analysis;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DamYou.Services;

/// <summary>
/// Background IHostedService that periodically processes the pipeline queue.
/// 
/// Lifecycle:
/// - StartAsync: Initialize timer on app startup
/// - StopAsync: Gracefully shut down on app exit
/// 
/// Architecture:
/// - Uses IServiceScopeFactory to create a scoped DbContext for each ProcessQueueAsync call
/// - Polls every 2 seconds to check for pending work
/// - Routes progress events via IProcessingStateService events for UI binding
/// - Respects CancellationToken for graceful shutdown
/// </summary>
public sealed class ProcessingHostedService : IHostedService, IProcessingWorker
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IProcessingStateService _processingStateService;
    private readonly ILogger<ProcessingHostedService> _logger;
    private Timer? _processingTimer;
    private CancellationTokenSource? _stoppingCts;

    public ProcessingHostedService(
        IServiceScopeFactory scopeFactory,
        IProcessingStateService processingStateService,
        ILogger<ProcessingHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _processingStateService = processingStateService;
        _logger = logger;
    }

    /// <summary>
    /// Called when the app starts. Initializes cancellation token source and starts the processing timer.
    /// Processing will begin automatically and periodically check for pending work.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ProcessingHostedService started (auto-timer mode)");
        _stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Start a timer that checks for work every 5 seconds
        _processingTimer = new Timer(
            async (_) => await ProcessQueueIfPendingAsync(),
            null,
            TimeSpan.Zero,           // Start immediately
            TimeSpan.FromSeconds(5)  // Check every 5 seconds
        );

        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the app shuts down. Cancels processing and cleans up.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ProcessingHostedService stopping");

        // Stop the timer
        if (_processingTimer is not null)
        {
            await _processingTimer.DisposeAsync();
            _processingTimer = null;
        }

        // Signal cancellation
        _stoppingCts?.Cancel();
        _stoppingCts?.Dispose();

        _processingStateService.NotifyProcessingStopped();
    }

    /// <summary>
    /// Periodically called by the timer to check for pending work and process it.
    /// Uses a scoped DbContext for safe, isolated work.
    /// </summary>
    private async Task ProcessQueueIfPendingAsync()
    {
        try
        {
            if (_stoppingCts?.Token.IsCancellationRequested ?? true)
                return;

            // Create a scope for this processing attempt
            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<IPipelineProcessorService>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<ProcessingHostedService>>();

            // Check if there's work to do
            var pendingCount = await processor.GetPendingCountAsync(_stoppingCts.Token);
            if (pendingCount == 0)
            {
                // No work, idle — check if we were processing and stop
                _processingStateService.NotifyProcessingStopped();
                return;
            }

            // Start processing
            _processingStateService.NotifyProcessingStarted(pendingCount);

            // Create a progress reporter that broadcasts to subscribers
            var progress = new Progress<AnalysisProgress>(p =>
            {
                _processingStateService.NotifyProgress(p);
            });

            // Process the queue
            await processor.ProcessQueueAsync(progress, _stoppingCts.Token);

            // When done, mark as complete
            _processingStateService.NotifyProcessingStopped();
            logger.LogInformation($"Pipeline processing completed. {pendingCount} items processed.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Pipeline processing cancelled");
            _processingStateService.NotifyProcessingStopped();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during pipeline processing");
            // Don't update UI with error — log internally only
            // Processing will retry on next timer tick
            _processingStateService.NotifyProcessingStopped();
        }
    }

    /// <summary>
    /// Manual trigger to start processing immediately (called after scan completes).
    /// Bypasses the timer for immediate feedback.
    /// </summary>
    public async Task TriggerProcessingAsync()
    {
        _logger.LogInformation("Triggering immediate pipeline processing");
        await ProcessQueueIfPendingAsync();
    }
}
