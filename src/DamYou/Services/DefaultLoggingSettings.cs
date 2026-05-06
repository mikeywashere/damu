namespace DamYou.Services;

/// <summary>
/// Persists logging settings using MAUI Preferences (per-device storage).
/// Supports toggling verbose logging on/off without restarting the app.
/// </summary>
public sealed class DefaultLoggingSettings : ILoggingSettings
{
    private const string VerboseLoggingKey = "verbose_logging_enabled";
    private const bool DefaultVerboseLoggingEnabled = false;

    public bool IsVerboseLoggingEnabled()
        => Preferences.Default.Get(VerboseLoggingKey, DefaultVerboseLoggingEnabled);

    public void SetVerboseLoggingEnabled(bool enabled)
        => Preferences.Default.Set(VerboseLoggingKey, enabled);
}
