namespace DamYou.Services;

/// <summary>
/// Service for step-level logging of photo analysis pipeline.
/// Logs each processing step with timestamps when verbose mode is enabled.
/// </summary>
public interface IVerboseLoggingService
{
    /// <summary>
    /// Checks if verbose logging is enabled via preferences.
    /// </summary>
    bool IsVerboseEnabled();

    /// <summary>
    /// Gets the configured log folder path, or default if not set.
    /// Default: AppData/Roaming/dam-you/logs
    /// </summary>
    string GetLogFolderPath();

    /// <summary>
    /// Sets the log folder path in preferences.
    /// </summary>
    void SetLogFolderPath(string folderPath);

    /// <summary>
    /// Toggles verbose logging on/off in preferences.
    /// </summary>
    void SetVerboseEnabled(bool enabled);

    /// <summary>
    /// Logs a single analysis step for a photo.
    /// Format: [timestamp] filename: step
    /// No-op if verbose mode is disabled.
    /// </summary>
    Task LogStepAsync(string step, string filename, DateTime timestamp, CancellationToken ct = default);
}
