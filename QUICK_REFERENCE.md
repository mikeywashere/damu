# Quick Reference: Status Bar & Background Processing

**TL;DR:** Photos now process automatically in the background. A status bar shows progress in real-time. ✅

---

## What Changed

### New Files
- `ProcessingStateViewModel.cs` — Observable state for UI
- `ProcessingHostedService.cs` — Background worker (IHostedService)
- `StatusBar.xaml` — Status bar UI component
- Unit tests (3 test files)

### Modified Files
- `MauiProgram.cs` — Register new services
- `App.xaml.cs` — Cache ViewModel for XAML binding
- `LibrarySetupViewModel.cs` — Trigger processing after import
- `LibraryView.xaml` — Embed status bar (Row 2)

---

## How to Use

### Running Tests
```bash
# Unit tests
dotnet test tests/DamYou.Tests/ProcessingStateViewModelTests.cs
dotnet test tests/DamYou.Tests/ProcessingHostedServiceTests.cs

# All tests
dotnet test tests/DamYou.Tests/
```

### Building
```bash
dotnet build
```

### Manual Testing
1. Start the app
2. Go through setup (select folders)
3. Watch the status bar show:
   - Spinner 🔄 (while importing)
   - Progress count (e.g., "5/42")
   - Status text (e.g., "Processing photos...")
4. When done: Checkmark ✓ + "Complete"

---

## Key Classes

### ProcessingStateViewModel
**Singleton** shared state for progress updates.

```csharp
var vm = App.ProcessingState;  // Access from anywhere
vm.IsProcessing    // bool — true if items being processed
vm.CurrentProgress // int — completed count
vm.TotalItems      // int — total queue size
vm.StatusText      // string — "Processing...", "Complete", etc.
vm.ProgressText    // string (computed) — "5/42"

// Worker calls this to report progress
vm.ReportProgress(new AnalysisProgress(...));
```

### ProcessingHostedService
**Singleton** IHostedService that manages background processing.

```csharp
// Starts automatically on app launch
// Runs a 2-second timer to check for pending work
// Can also be triggered manually:
await _processingWorker.TriggerProcessingAsync();
```

### StatusBar
**XAML component** — displays processing state.

```xaml
<views:StatusBar Grid.Row="2" 
                 BindingContext="{x:Static local:App.ProcessingState}" />
```

---

## Architecture Decision

**Pattern:** IHostedService + Timer Polling + Manual Trigger

**Why?**
- ✅ Integrated with MAUI app lifecycle
- ✅ Timer ensures processing continues even after app restart
- ✅ Manual trigger provides immediate feedback
- ✅ Thread-safe (MainThread marshaling for all UI updates)
- ✅ Scoped DbContext (no "disposed context" errors)

**What happens:**
1. App starts → ProcessingHostedService.StartAsync() → Timer begins
2. Every 2 seconds → Check for pending work
3. User triggers import → Immediately calls ProcessQueueAsync()
4. While processing → IProgress callbacks update ViewModel
5. ViewModel updates → XAML bindings update StatusBar
6. Processing complete → StatusBar shows "Complete"

---

## Common Issues & Fixes

### Status bar doesn't update
- ✅ Verify `App.ProcessingState` is not null (set in App.xaml.cs constructor)
- ✅ Verify status bar has `BindingContext="{x:Static local:App.ProcessingState}"`
- ✅ Verify ProcessingStateViewModel is registered as singleton in MauiProgram.cs

### "Disposed DbContext" error
- ✅ ProcessingHostedService uses IServiceScopeFactory — each call gets fresh scope
- ✅ No shared DbContext between calls

### Processing never starts
- ✅ Verify ProcessingHostedService is registered: `builder.Services.AddHostedService<ProcessingHostedService>()`
- ✅ Verify LibrarySetupViewModel injects ProcessingHostedService
- ✅ Verify CompleteSetupAsync calls `_processingWorker.TriggerProcessingAsync()`

### Tests fail
- ✅ Ensure you're in the right directory: `cd src/DamYou.Tests/`
- ✅ Verify DamYou project builds: `dotnet build`
- ✅ Run with verbose output: `dotnet test --verbosity detailed`

---

## Integration Checklist

- ✅ ProcessingStateViewModel created & registered as singleton
- ✅ ProcessingHostedService created & registered as IHostedService
- ✅ StatusBar UI created & embedded in LibraryView (Row 2)
- ✅ App.xaml.cs caches ProcessingState for XAML binding
- ✅ LibrarySetupViewModel injects ProcessingHostedService
- ✅ LibrarySetupViewModel calls TriggerProcessingAsync after import
- ✅ MauiProgram.cs registers all components
- ✅ Unit tests created (3 test files)
- ✅ Build succeeds (0 errors)

---

## Next Steps for Squad

1. **Review** — Dallas approves architecture (see ARCHITECTURE_DECISION.md)
2. **Test** — Run unit tests locally
3. **Manual QA** — Scan library, verify status bar updates in real-time
4. **Integration Tests** — Ash implements full scenario tests (template provided)
5. **Polish** — Add spinner animation, refine UI layout

---

## Questions?

See `IMPLEMENTATION_SUMMARY.md` for full architecture details and data flow diagrams.

See `.squad/decisions/inbox/architecture-background-processing.md` for architecture decision record.
