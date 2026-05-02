# Project Context

- **Owner:** Michael R. Schmidt
- **Project:** dam-you — Digital asset management desktop app (MAUI) for a single user to manage their photos.
- **Stack:** .NET MAUI, C#, Windows desktop, file I/O, EXIF metadata, SQLite, async patterns
- **Created:** 2026-04-27

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-05-02 — Responsive gallery pagination and viewport-aware loading

- **Feature:** Implemented responsive photo gallery with dynamic pagination based on grid viewport size.
- **Key changes:** 
  - `GalleryViewModel` now calculates page size dynamically via `SetGridDimensions()` using formula: (gridWidth / cellWidth) * (gridHeight / cellHeight) + 1 row lazy buffer.
  - Added pagination state: `CurrentPage`, `TotalPages`, `CanLoadMore` properties to track gallery state.
  - `RefreshAsync()` command clears pagination state and reloads first page while preserving viewport size (doesn't reset grid layout).
  - `LoadMorePhotosAsync()` automatically triggers on scroll-to-bottom (within 200px), filling viewport with async calls.
  - `PhotoGridItem` already exposes metadata: FileName, DateTaken, DateIndexed, Width/Height for hover tooltips.
- **UI integration:** 
  - Grid `SizeChanged` event calls `SetGridDimensions()` to recalculate on window resize.
  - ScrollView `Scrolled` event detects scroll-to-bottom and invokes `LoadMorePhotosCommand`.
  - Image hover shows full metadata tooltip (filename, date, dimensions).
  - Image click opens full-screen modal with AspectFit image and close button (✕).
  - Added "🔄 Refresh" button separate from "🔍 Rescan" — refresh resets pagination only, rescan re-indexes library.
- **Fallback behavior:** If grid dimensions unknown on startup, defaults to 10 photos (hardcoded `DefaultPageSize`), then recalculates once window is rendered.
- **Async-first:** All DB queries use skip/take pagination; no blocking UI thread on photo loads or searches.

### 2026-05-02 — MAUI Shell tab content initialization timing (corrected)

- **Lifecycle issue:** `OnAppearing()` is too late in the MAUI Shell lifecycle. Shell tries to create platform elements for empty `ShellContent` tabs before `OnAppearing()` fires, causing `InvalidOperationException: No Content found for ShellContent`.
- **Solution:** Create a public `InitializeTabContent(IServiceProvider services)` method in AppShell that traverses the Shell hierarchy and assigns views to tabs synchronously.
- **Calling point:** In App.xaml.cs `NavigateFromSplashAsync()`, call `appShell.InitializeTabContent(_services)` immediately after resolving AppShell from DI, BEFORE setting it to `Application.Current!.MainPage`.
- **Result:** Tab content is populated synchronously before the Shell platform renderer is invoked, preventing the rendering exception. All three tabs (Gallery, Folders, Tasks) display correctly on app startup.

### 2026-05-02 — MAUI command-line argument handling & fallback logging

- **MAUI limitation:** `Environment.GetCommandLineArgs()` in MAUI apps only returns the executable path (as .dll), NOT the command-line arguments passed on the command line. This is a WinUI/MAUI-level limitation and cannot be worked around via standard .NET APIs.
- **Fallback strategy:** When no explicit log path is provided to `LoggingService.ConfigureLogging(null)`, the app now automatically logs to `{AppData}\DamYou\app-startup-diagnostic.log` with daily rolling intervals. This ensures logs are always captured, even without command-line configuration.
- **Debug output in VS:** Added `Debug.WriteLine()` calls in MauiProgram to show raw arguments received and logging configuration status. These appear in Visual Studio's Output window during debugging, helpful for troubleshooting.
- **Shutdown verification:** Tested app startup/shutdown cycle; diagnostic log successfully created, populated with initialization/navigation/shutdown entries, and persisted to disk after app close. Shutdown handler invokes `LoggingService.CloseAndFlush()` reliably.

### 2026-05-02 — App lifecycle logger shutdown

- **Serilog buffer issue:** Serilog buffers output; if the app closes before calling `Log.CloseAndFlush()`, the file sink won't flush pending entries to disk, resulting in empty or missing log files.
- **MAUI lifecycle hook:** Use the `Unloaded` event on the root NavigationPage (created in `App.CreateWindow()`) to detect app shutdown. This fires when the page is unloaded, signaling app termination.
- **Implementation:** In App.xaml.cs, register `navPage.Unloaded += OnPageUnloaded` in CreateWindow, then call `LoggingService.CloseAndFlush()` in the handler. This ensures all buffered logs are flushed before the app process exits.
- **Windows platform app:** The platform-specific `Platforms/Windows/App.xaml.cs` doesn't need modification; the MAUI-level handler is sufficient for cross-platform consistency.

### 2026-05-02 — Diagnostic logging implementation

- **Serilog setup:** Added Serilog (v3.*), Serilog.Sinks.File (v5.*), Serilog.Sinks.Debug (v2.*), and Serilog.Extensions.Logging (v7.*) to enable comprehensive file-based diagnostics.
- **LoggingService pattern:** Created a static LoggingService class that wraps Serilog initialization. Call `ConfigureLogging()` early in CreateMauiApp before any other initialization. If no log path is provided, defaults to debug sink only.
- **Command-line parsing:** CommandLineParser handles `--log <path>` arguments. Must parse `Environment.GetCommandLineArgs()` before creating services to apply log path to initialization logs.
- **Key logging points:** Instrumented MauiProgram.CreateMauiApp (app start, db context, migrations), App constructor (component init), App.CreateWindow (window creation, splash timing), App.NavigateFromSplashAsync (view resolution, routing decisions), and AppShell (initialization).
- **Log output template:** Uses ISO 8601 timestamp with timezone, level code (u3), and exception stack traces. Rolling daily, retains 10 files.

### 2026-04-27 — Initial data layer scaffold

- `dotnet new sln` on the installed SDK creates `.slnx` format (not `.sln`). All `dotnet sln` commands must target `dam-you.slnx`.
- `dotnet new maui --framework net9.0-windows10.0.19041.0` is rejected by the template; the MAUI template only accepts `net9.0` or `net10.0`. The Windows TFM (`net9.0-windows10.0.19041.0`) is baked into the generated `.csproj` automatically — no need to pass it on the command line.
- `ToHashSetAsync` does not exist in EF Core 8 on `IQueryable<T>`. Use `ToListAsync` followed by `.ToHashSet()` instead.
- `DamYou.Data` builds clean (0 errors, 0 warnings) with EF Core 8.* on net9.0.

