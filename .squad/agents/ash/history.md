# Project Context

- **Owner:** Michael R. Schmidt
- **Project:** dam-you — Digital asset management desktop app (MAUI) for a single user to manage their photos.
- **Stack:** .NET MAUI, C#, Windows desktop, xUnit/NUnit, MVVM ViewModels, file system edge cases
- **Created:** 2026-04-27

## Learnings

### 2026-04-27 — Test infrastructure sprint

- Parker scaffolded `DamYou.Data` as a class library project but the initial commit contains no source files (empty project). Tests reference `DamYouDbContext` and `FolderRepository` from `DamYou.Data.Repositories` — these must exist before `dotnet build` on the test project succeeds.
- The test `.csproj` was already scaffolded by Parker via `dotnet new xunit`; needed to add `Moq`, `Microsoft.EntityFrameworkCore.InMemory`, `IsTestProject`, and the `ProjectReference` to `DamYou.Data`.
- `SyntheticPhotoFixture` uses a real minimal JPEG byte array (not a fake extension rename), so it's safe to pass to image libraries like `MetadataExtractor`.
- In-memory EF Core databases must use a unique `Guid` name per test class to prevent state leakage between parallel test runs.
- `DeactivateFolderAsync` should be a silent no-op on missing IDs — tests encode that contract explicitly so Parker implements it correctly.

### 2026-05-02 — Gallery feature test design

**Designed and implemented 50+ unit tests for responsive gallery feature.**

- **Pagination state machine is non-trivial:** Tests reveal the importance of preventing double-loads during concurrent operations (resize + load, search + pagination, refresh + load). Implemented explicit race condition tests (`LoadMorePhotosCommand_DoesNotExecute_WhenAlreadyLoading`, `ResizeWhileLoading_DoesNotCauseDoubleLoad`).
- **Responsive formula not yet implemented:** Current code has fixed `PageSize=10` constant. Tests document *intended* behavior (formula: `(viewportWidth / cellSize) * (viewportHeight / cellSize) + 1 buffer row`). Parker needs to add responsive calculation before feature is production-ready.
- **Edge cases are foundational:** Empty library, single photo, and large library (5000+) tests are blocker-level. Skipping these leads to crashes on user startup or OOM on large libraries. All three covered in tests.
- **Search + pagination interaction is subtle:** When search is active, pagination must reset and filter within results. Tests validate both the clearing of previous results and the correct offset calculation when loading more search results.
- **Missing metadata is common:** ~30% of real photo libraries have null `DateTaken` or missing dimensions. Tests validate graceful degradation (null checks on UI binding).
- **Modal behavior needs team decision:** Click-outside behavior (close modal or no-op) is not yet defined. Created placeholder decision file (`gallery-modal-behavior.md`) for team consensus before implementation.
- **Throttle/debounce resize events is necessary:** Without this, resizing window rapidly triggers load storm. Tests use `IsLoadingMore` guard to prevent concurrent loads, but should add explicit debounce in production code (300ms throttle suggested).

