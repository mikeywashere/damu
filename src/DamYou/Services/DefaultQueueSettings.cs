namespace DamYou.Services;

/// <summary>
/// Persists queue settings using MAUI Preferences (per-device storage).
/// Parker's multi-queue work can inject a different IQueueSettings implementation
/// without changing the UI layer.
/// </summary>
public sealed class DefaultQueueSettings : IQueueSettings
{
    private const string WaitTimeMsKey = "queue_wait_time_ms";
    private const int DefaultWaitTimeMs = 5000;
    private const string ProcessingDelayMsKey = "processing_delay_ms";
    private const int DefaultProcessingDelayMs = 250;

    public int GetQueueWaitTimeMs()
        => Preferences.Default.Get(WaitTimeMsKey, DefaultWaitTimeMs);

    public void SetQueueWaitTimeMs(int ms)
        => Preferences.Default.Set(WaitTimeMsKey, ms);

    public int GetProcessingDelayMs()
        => Preferences.Default.Get(ProcessingDelayMsKey, DefaultProcessingDelayMs);

    public void SetProcessingDelayMs(int ms)
        => Preferences.Default.Set(ProcessingDelayMsKey, ms);
}
