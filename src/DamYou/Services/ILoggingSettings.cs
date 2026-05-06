namespace DamYou.Services;

/// <summary>
/// Provides read/write access to logging configuration.
/// Persists user preferences for logging behavior (e.g., verbose logging).
/// </summary>
public interface ILoggingSettings
{
    /// <summary>
    /// Returns whether verbose logging is enabled.
    /// When enabled, the minimum log level is set to Verbose.
    /// When disabled, the minimum log level is set to Information.
    /// </summary>
    bool IsVerboseLoggingEnabled();

    /// <summary>
    /// Persists the verbose logging setting.
    /// Changes take effect immediately if the logging system supports dynamic reconfiguration,
    /// or on the next app startup.
    /// </summary>
    void SetVerboseLoggingEnabled(bool enabled);
}
