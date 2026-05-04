namespace DamYou.Services;

/// <summary>
/// Property-based queue settings for MAUI Preferences-backed configuration.
/// Extends IQueueSettings so implementations are compatible with QueueProcessorService.
/// </summary>
public interface IQueueSettingsService : IQueueSettings
{
    /// <summary>Milliseconds to wait after startup before first processing cycle. Default: 30000.</summary>
    int StartupDelayMs { get; set; }

    /// <summary>Milliseconds to wait between processing cycles. Default: 5000.</summary>
    int QueueWaitTimeMs { get; set; }

    /// <summary>Persists current settings to the underlying Preferences store.</summary>
    void Save();

    // Bridge IQueueSettings methods to property-based access
    int IQueueSettings.GetQueueWaitTimeMs() => QueueWaitTimeMs;
    void IQueueSettings.SetQueueWaitTimeMs(int ms) => QueueWaitTimeMs = ms;
    int IQueueSettings.GetStartupDelayMs() => StartupDelayMs;
    void IQueueSettings.SetStartupDelayMs(int ms) => StartupDelayMs = ms;
}
