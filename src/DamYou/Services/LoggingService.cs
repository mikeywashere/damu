using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;

namespace DamYou.Services;

/// <summary>
/// Encapsulates Serilog configuration and provides a global logger.
/// Supports dynamic log level adjustment for verbose logging without app restart.
/// Call ConfigureLogging before any app initialization to enable file logging.
/// </summary>
public static class LoggingService
{
    private static Logger? _logger;
    private static string? _logFilePath;
    private static string? _diagnosticLogPath;
    private static LoggingLevelSwitch? _levelSwitch;

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

            // Create a level switch to allow dynamic log level changes at runtime
            _levelSwitch = new LoggingLevelSwitch(LogEventLevel.Information);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(_levelSwitch)
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
            // Fallback to debug-only logging with level switch
            _levelSwitch = new LoggingLevelSwitch(LogEventLevel.Information);
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(_levelSwitch)
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
    /// Sets the minimum log level dynamically at runtime.
    /// When verbose is true, sets level to Verbose (most detailed).
    /// When verbose is false, sets level to Information (normal operation).
    /// </summary>
    public static void SetVerboseLogging(bool isVerbose)
    {
        if (_levelSwitch == null)
        {
            Debug.WriteLine("[LoggingService] Level switch not initialized; verbose logging cannot be changed dynamically");
            return;
        }

        var targetLevel = isVerbose ? LogEventLevel.Verbose : LogEventLevel.Information;
        _levelSwitch.MinimumLevel = targetLevel;
        Debug.WriteLine($"[LoggingService] Log level changed to: {targetLevel}");
    }

    /// <summary>
    /// Flushes and closes the logger. Call this during app shutdown.
    /// </summary>
    public static void CloseAndFlush()
    {
        Log.CloseAndFlush();
    }
}
