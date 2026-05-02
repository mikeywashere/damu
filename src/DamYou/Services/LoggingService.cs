using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;

namespace DamYou.Services;

/// <summary>
/// Encapsulates Serilog configuration and provides a global logger.
/// Call ConfigureLogging before any app initialization to enable file logging.
/// </summary>
public static class LoggingService
{
    private static Logger? _logger;
    private static string? _logFilePath;
    private static string? _diagnosticLogPath;

    /// <summary>
    /// Configures Serilog to write to a file. Call this early during app initialization.
    /// If logFilePath is null, logging defaults to a diagnostic file in AppData.
    /// </summary>
    public static void ConfigureLogging(string? logFilePath)
    {
        _logFilePath = logFilePath;

        // If no log path provided, use diagnostic fallback in AppData
        var effectiveLogPath = logFilePath;
        if (string.IsNullOrWhiteSpace(effectiveLogPath))
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DamYou");
            Directory.CreateDirectory(appDataPath);
            effectiveLogPath = Path.Combine(appDataPath, "app-startup-diagnostic.log");
            _diagnosticLogPath = effectiveLogPath;
            Debug.WriteLine($"[LoggingService] No log path provided; using diagnostic fallback: {effectiveLogPath}");
        }

        try
        {
            var directory = Path.GetDirectoryName(effectiveLogPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    effectiveLogPath,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 10)
                .WriteTo.Debug()
                .CreateLogger();

            _logger = Log.Logger as Logger;
            Debug.WriteLine($"[LoggingService] Successfully configured logging to: {effectiveLogPath}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LoggingService] Failed to configure file logging: {ex.Message}");
            // Fallback to debug-only logging
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Debug()
                .CreateLogger();
        }
    }

    /// <summary>
    /// Gets the global ILogger instance for use in services.
    /// </summary>
    public static ILogger GetLogger() => Log.Logger;

    /// <summary>
    /// Returns the configured log file path, or null if logging is disabled.
    /// </summary>
    public static string? GetLogFilePath() => _logFilePath;

    /// <summary>
    /// Flushes and closes the logger. Call this during app shutdown.
    /// </summary>
    public static void CloseAndFlush()
    {
        Log.CloseAndFlush();
    }
}
