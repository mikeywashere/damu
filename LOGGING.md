# DAMu Diagnostic Logging Guide

## Overview

DAMu includes built-in diagnostic logging to help troubleshoot initialization issues, stalling, or unexpected behavior. Logs capture key events during app startup, database initialization, view creation, and navigation.

## Using Diagnostic Logging

### Enable Logging

Run the app with the `--log` argument to write diagnostics to a file:

```bash
DAMu.exe --log mylogfile.log
```

The app will create the log file in the specified path (or current directory if relative path is used).

### Log File Location

You can specify:
- **Relative path:** `DAMu.exe --log debug.log` → creates log in current working directory
- **Absolute path:** `DAMu.exe --log "C:\Logs\damyou_2026-05-02.log"` → creates in C:\Logs\
- **User temp:** `DAMu.exe --log "%TEMP%\damyou_debug.log"` → creates in Windows Temp folder

### Without Logging

```bash
DAMu.exe
```

If no `--log` argument is provided, the app runs without file logging. Debug output may still be captured in Visual Studio's Debug Output window (IDE development only).

## Log File Format

Each log entry follows this format:

```
2026-05-02 10:25:12.927 -07:00 [INF] App constructor: Initialization complete
```

- **Timestamp:** ISO 8601 format with timezone
- **Level Code:** 
  - `[DBG]` = Debug (detailed diagnostic info)
  - `[INF]` = Information (key milestones)
  - `[WRN]` = Warning (potential issues)
  - `[ERR]` = Error (failures)
- **Message:** Human-readable event description with structured data

## Key Log Checkpoints

### Startup (`MauiProgram.CreateMauiApp`)
- Command-line arguments parsed
- Log file path configured
- Database path and migrations status

### App Initialization (`App.xaml.cs`)
- Component initialization
- ViewModel resolution (ProcessingStateViewModel)
- Window creation
- Splash screen timing
- Navigation routing (folders vs. gallery tab)

### Shell & Views
- AppShell initialization
- View/ViewModel instantiation
- Route navigation events

### Database
- Context initialization
- Migration execution status
- Folder repository queries

## Interpreting Logs

### Finding Startup Bottlenecks

Look for large time gaps between consecutive log entries:

```
2026-05-02 10:25:12.927 -07:00 [INF] CreateWindow: Window created with splash screen
2026-05-02 10:25:14.102 -07:00 [INF] Splash and navigation complete. Total time: 1175ms
```

This shows ~1.2 seconds for splash → main navigation, which is expected (2 second delay built-in).

### Debugging Initialization Failures

If the app crashes or hangs, search the log for `[ERR]`:

```
2026-05-02 10:25:15.650 -07:00 [ERR] Error navigating from splash
System.IO.IOException: The file is locked...
```

The stack trace follows, showing exactly where the failure occurred.

### Database Issues

Search for `[DBG] Initializing database` or `[INF] Database migrations`:

```
2026-05-02 10:25:13.200 -07:00 [DBG] Initializing database context...
2026-05-02 10:25:13.210 -07:00 [DBG] Database path: C:\Users\user\AppData\Local\DamYou\dam-you.db
2026-05-02 10:25:13.500 -07:00 [INF] Database migrations completed successfully.
```

If migrations fail, the error will be logged and the app will not proceed.

## Log File Management

Logs are **rotated daily** with a retention policy of **10 files** per day. This prevents unbounded disk usage:

- Day 1: `debug.log` (first entries)
- Day 2: `debug.20260502.log` (rotated from yesterday)
- Etc., up to 10 files

Old logs are automatically removed after the retention limit is exceeded.

## Performance Considerations

Logging has minimal performance impact:
- File I/O is buffered
- Structured logging avoids expensive string formatting
- Debug sink has negligible overhead

For production or performance-critical scenarios, disable logging by running without `--log`.

## Troubleshooting

### Log file is not created
- Check that the directory path is valid and you have write permissions
- Review error messages in Visual Studio Output window (if running from IDE)

### Log file is empty
- The app may have crashed before any logging setup
- Try running with a simple relative path: `DAMu.exe --log test.log`

### Need higher detail
- Current implementation logs Debug level and above
- For more detailed tracing, contact the development team

## Example Session

```
2026-05-02 10:25:12.900 -07:00 [INF] === DAMu App Initialization Started ===
2026-05-02 10:25:12.901 -07:00 [INF] Command-line arguments: DAMu.exe --log debug.log
2026-05-02 10:25:12.902 -07:00 [INF] Logging to: debug.log
2026-05-02 10:25:12.950 -07:00 [DBG] Initializing database context...
2026-05-02 10:25:12.951 -07:00 [DBG] Database path: C:\Users\user\AppData\Local\DamYou\dam-you.db
2026-05-02 10:25:13.100 -07:00 [INF] App constructor: Initializing component...
2026-05-02 10:25:13.101 -07:00 [DBG] App constructor: Resolving ProcessingStateViewModel...
2026-05-02 10:25:13.102 -07:00 [INF] App constructor: Initialization complete
2026-05-02 10:25:13.103 -07:00 [INF] CreateWindow: Starting splash screen presentation...
2026-05-02 10:25:13.104 -07:00 [DBG] CreateWindow: Resolving SplashScreenView...
2026-05-02 10:25:13.105 -07:00 [INF] CreateWindow: Window created with splash screen
2026-05-02 10:25:13.106 -07:00 [DBG] AppShell constructor: Initializing component...
2026-05-02 10:25:13.107 -07:00 [INF] AppShell initialization complete
2026-05-02 10:25:15.110 -07:00 [DBG] SplashTransitionAsync: Waiting 2 seconds before transition...
2026-05-02 10:25:15.113 -07:00 [DBG] SplashTransitionAsync: Delay complete, navigating from splash...
2026-05-02 10:25:15.114 -07:00 [DBG] NavigateFromSplashAsync: Resolving folder repository...
2026-05-02 10:25:15.200 -07:00 [DBG] NavigateFromSplashAsync: Found 3 active folders
2026-05-02 10:25:15.201 -07:00 [DBG] NavigateFromSplashAsync: Resolving AppShell...
2026-05-02 10:25:15.202 -07:00 [DBG] NavigateFromSplashAsync: MainPage set to AppShell
2026-05-02 10:25:15.203 -07:00 [DBG] NavigateFromSplashAsync: Navigating to route: gallery
2026-05-02 10:25:15.250 -07:00 [INF] NavigateFromSplashAsync: Splash and navigation complete. Total time: 2350ms
2026-05-02 10:25:15.251 -07:00 [INF] === MauiProgram initialization completed ===
```

This example shows a successful startup with logging enabled. The app completes initialization in about 2.4 seconds.
