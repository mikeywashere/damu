namespace DamYou.Services;

/// <summary>
/// Provides read/write access to queue processing configuration.
/// Implemented by DefaultQueueSettings (Preferences-backed); Parker's multi-queue
/// work may extend or replace this with per-queue granularity.
/// </summary>
public interface IQueueSettings
{
    /// <summary>Returns the wait time between queue processing cycles in milliseconds.</summary>
    int GetQueueWaitTimeMs();

    /// <summary>Persists the wait time between queue processing cycles.</summary>
    void SetQueueWaitTimeMs(int ms);

    /// <summary>
    /// Returns the one-time startup delay before the first processing cycle, in milliseconds.
    /// Default is 30 000 ms (30 seconds). Override in tests for fast-cycle verification.
    /// </summary>
    int GetStartupDelayMs() => 30_000;

    /// <summary>Persists the startup delay. No-op by default; concrete implementations may override.</summary>
    void SetStartupDelayMs(int ms) { }
}
