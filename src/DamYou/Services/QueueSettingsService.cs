using Microsoft.Maui.Storage;

namespace DamYou.Services;

/// <summary>
/// MAUI Preferences-backed implementation of IQueueSettingsService.
/// Values are stored in milliseconds. Defaults: StartupDelayMs=30000, QueueWaitTimeMs=5000.
/// Injectable IPreferences constructor enables unit testing without MAUI runtime.
/// </summary>
public sealed class QueueSettingsService : IQueueSettingsService
{
    private const string StartupDelayKey = "queue_startup_delay_ms";
    private const string WaitTimeKey = "queue_wait_time_ms";
    private const int DefaultStartupDelayMs = 30_000;
    private const int DefaultWaitTimeMs = 5_000;

    private readonly IPreferences _preferences;

    public QueueSettingsService() : this(Preferences.Default) { }

    public QueueSettingsService(IPreferences preferences)
    {
        _preferences = preferences;
        StartupDelayMs = _preferences.Get(StartupDelayKey, DefaultStartupDelayMs);
        QueueWaitTimeMs = _preferences.Get(WaitTimeKey, DefaultWaitTimeMs);
    }

    public int StartupDelayMs { get; set; }

    public int QueueWaitTimeMs { get; set; }

    public void Save()
    {
        _preferences.Set(StartupDelayKey, StartupDelayMs);
        _preferences.Set(WaitTimeKey, QueueWaitTimeMs);
    }
}
