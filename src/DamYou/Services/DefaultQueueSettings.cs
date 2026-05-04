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

    public int GetQueueWaitTimeMs()
        => Preferences.Default.Get(WaitTimeMsKey, DefaultWaitTimeMs);

    public void SetQueueWaitTimeMs(int ms)
        => Preferences.Default.Set(WaitTimeMsKey, ms);
}
