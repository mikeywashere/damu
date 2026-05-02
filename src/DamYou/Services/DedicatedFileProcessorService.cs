using DamYou.Data.Analysis;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DamYou.Services;

/// <summary>
/// Background IHostedService that exclusively processes files from the pipeline queue.
/// 
/// Unlike ProcessingHostedService, this service:
/// - Does NOT perform folder scans
/// - Focuses solely on executing queued pipeline tasks
/// - Runs independently and can operate in parallel with folder scanning
/// - Can be enabled/disabled separately from the main scanner
///
/// Lifecycle:
/// - StartAsync: Initialize timer on app startup
/// - StopAsync: Gracefully shut down on app exit
/// 
/// Architecture:
/// - Uses IServiceScopeFactory to create a scoped DbContext for each processing attempt
/// - Polls at configurable intervals (default: 1 second) to check for pending tasks
/// - Routes processing through IPipelineProcessorService (same as main processor)
/// - Respects CancellationToken for graceful shutdown
/// </summary>
public sealed class DedicatedFileProcessorService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DedicatedFileProcessorService> _logger;
    private Timer? _processingTimer;
    private CancellationTokenSource? _stoppingCts;
    private readonly TimeSpan _pollingInterval;

    public DedicatedFileProcessorService(
        IServiceScopeFactory scopeFactory,
        ILogger<DedicatedFileProcessorService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        // Configurable polling interval (default: 1 second for dedicated processor)
        _pollingInterval = TimeSpan.FromSeconds(1);
    }

    /// <summary>
    /// Called when the app starts. Initializes the processing timer.
    /// File processing will begin automatically and periodically check for pending tasks.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DedicatedFileProcessorService started (dedicated file processing mode, interval: {IntervalMs}ms)", _pollingInterval.TotalMilliseconds);
        _stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Start a timer that checks for work at the specified interval
        _processingTimer = new Timer(
            async (_) => await ProcessPendingFilesAsync(),
            null,
            TimeSpan.Zero,           // Start immediately
            _pollingInterval         // Check at configured interval
        );

        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the app shuts down. Cancels processing and cleans up.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DedicatedFileProcessorService stopping");

        // Stop the timer
        if (_processingTimer is not null)
        {
            await _processingTimer.DisposeAsync();
            _processingTimer = null;
        }

        // Signal cancellation
        _stoppingCts?.Cancel();
        _stoppingCts?.Dispose();

        _logger.LogInformation("DedicatedFileProcessorService stopped");
    }

    /// <summary>
    /// Periodically called by the timer to check for pending files in the queue and process them.
    /// Uses a scoped DbContext for safe, isolated work.
    /// This runs independently of folder scanning and focuses solely on pipeline task execution.
    /// </summary>
    private async Task ProcessPendingFilesAsync()
    {
        try
        {
            if (_stoppingCts?.Token.IsCancellationRequested ?? true)
                return;

            // Create a scope for this processing attempt
            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<IPipelineProcessorService>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<DedicatedFileProcessorService>>();

            // Check if there's work to do
            var pendingCount = await processor.GetPendingCountAsync(_stoppingCts.Token);
            if (pendingCount == 0)
            {
                // No work, idle
                return;
            }

            logger.LogDebug("DedicatedFileProcessorService found {PendingCount} pending file(s) to process", pendingCount);

            // Create a progress reporter
            var progress = new Progress<AnalysisProgress>(p =>
            {
                logger.LogDebug("DedicatedFileProcessorService processing progress: {Completed}/{Total}", p.Completed, p.Total);
            });

            // Process the queue
            await processor.ProcessQueueAsync(progress, _stoppingCts.Token);

            logger.LogInformation("DedicatedFileProcessorService completed processing. {PendingCount} items processed.", pendingCount);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("DedicatedFileProcessorService processing cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during dedicated file processing");
            // Processing will retry on next timer tick
        }
    }
}
