# Parker — Pipeline Data Layer Complete

**Date:** 2026-04-27  
**Author:** Parker (Backend Dev)

## What Was Built

### Entities & Enum
- `Photo.IsProcessed` (bool, default `false`) — marks whether downstream processing has occurred; existing import flow unaffected.
- `PipelineTaskStatus` enum: Queued / Running / Completed / Failed
- `PipelineTask` entity: tracks named tasks with status, timestamps, optional `PhotoId` FK

### DbContext
- Added `DbSet<PipelineTask> PipelineTasks`
- Configured indexes: `IX_PipelineTasks_Status`, `IX_PipelineTasks_PhotoId`, `IX_PipelineTasks_CreatedAt`
- FK to `Photo` with `OnDelete(SetNull)` (nullable, so tasks survive photo deletion)

### Repository
- `IPipelineTaskRepository` / `PipelineTaskRepository` — enqueue, status transitions with automatic timestamp tracking (StartedAt / CompletedAt), active/queued queries, depth count

### Scan Service
- `ILibraryScanService` / `LibraryScanService` — creates a "Scan Library" running task, walks watched folders, discovers images, creates `Photo` records with `IsProcessed = false`, enqueues "Process Photo" tasks, skips duplicates (existing unprocessed photos only get a new task if none queued), reports `ScanProgress`
- Error handling: failed scan task written with `CancellationToken.None` so failure is persisted even on cancellation

### DI Registration
- Both new services registered as `Scoped` in `MauiProgram.cs`

### Migration
- EF migration `AddPipelineQueue` generated and applied to snapshot

## Notable Decisions

1. **SHA-256 duplicated (not extracted)** — `LibraryScanService` has its own private `ComputeSha256Async`. Extraction to a shared helper was not done to avoid introducing a cross-cutting utility class without team discussion. Can be refactored later.

2. **Scan task written directly to DbContext** — The "Scan Library" task is managed directly via `_db` (not `_taskRepository`) so `StartedAt` and `Status = Running` are set atomically at creation, avoiding a two-step create-then-update pattern.

3. **Cancellation on error path** — `SaveChangesAsync(CancellationToken.None)` is used when persisting failure state so the error record is always written even if the token was cancelled.

4. **MAUI multi-target build** — `FolderPickerService.cs` had pre-existing build errors on iOS/Android/macOS targets (Windows-only namespaces). The Windows target (`net9.0-windows10.0.19041.0`) builds cleanly. This is pre-existing and not introduced by this change.
