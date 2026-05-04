namespace DamYou.Services;

/// <summary>
/// Implements verbose logging for photo analysis pipeline steps.
/// Uses MAUI Preferences to persist settings across sessions.
/// </summary>
public sealed class VerboseLoggingService : IVerboseLoggingService
{
    private const string VerboseEnabledKey = "verbose_logging_enabled";
    private const string LogFolderPathKey = "verbose_log_folder_path";

    private static readonly object _lock = new();

    /// <summary>
    /// Gets the default log folder: AppData/Roaming/dam-you/logs
    /// </summary>
    private static string DefaultLogFolderPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "dam-you",
            "logs");

    public bool IsVerboseEnabled()
        => Preferences.Default.Get(VerboseEnabledKey, false);

    public void SetVerboseEnabled(bool enabled)
        => Preferences.Default.Set(VerboseEnabledKey, enabled);

    public string GetLogFolderPath()
        => Preferences.Default.Get(LogFolderPathKey, DefaultLogFolderPath);

    public void SetLogFolderPath(string folderPath)
        => Preferences.Default.Set(LogFolderPathKey, folderPath);

    public async Task LogStepAsync(string step, string filename, DateTime timestamp, CancellationToken ct = default)
    {
        // Early exit if verbose logging is disabled
        if (!IsVerboseEnabled())
            return;

        await Task.Run(() =>
        {
            try
            {
                lock (_lock)
                {
                    var logFolder = GetLogFolderPath();
                    Directory.CreateDirectory(logFolder);

                    // Use date-based log file naming: verbose_YYYYMMDD.log
                    var logFileName = $"verbose_{timestamp:yyyyMMdd}.log";
                    var logFilePath = Path.Combine(logFolder, logFileName);

                    // Format: [timestamp] filename: step
                    var logEntry = $"[{timestamp:yyyy-MM-dd HH:mm:ss.fff}] {filename}: {step}";

                    File.AppendAllText(logFilePath, logEntry + Environment.NewLine);
                }
            }
            catch
            {
                // Silently fail — logging should never crash the app
            }
        }, ct);
    }
}
