# Project Context

- **Owner:** Michael R. Schmidt
- **Project:** dam-you — Digital asset management desktop app (MAUI) for a single user to manage their photos.
- **Stack:** .NET MAUI, C#, Windows desktop, local file storage, SQLite (likely), MVVM pattern
- **Created:** 2026-04-27

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-05-02 — Gallery Enhancement Architecture (Feature Request)

- **Viewport-aware loading:** Implement as scroll-position monitoring in View codebehind, not SizeChanged. When ScrollView reaches 90% of content height, fire `LoadMorePhotosCommand`. Simpler than predicting FlexLayout wrapping.
- **Pagination state:** Current ViewModel design is sound — `_currentSkip` and `_totalPhotoCount` are correct abstractions. No refactoring needed.
- **Modal viewer:** Use standalone Shell page (PhotoViewerModal) for image inspection. Simpler lifecycle than overlays; matches MAUI idiomatic patterns.
- **Refresh strategy:** Hard refresh (clear collection + reload from DB) is correct for single-user app. No cache invalidation logic needed.
- **Hover/Tooltips on desktop:** Use CommunityToolkit ToolTip or custom overlay. This is a Windows-first app, so hover is a reasonable affordance.
- **File path patterns:** GalleryView is at `src/DamYou/Views/GalleryView.xaml`; ViewModels at `src/DamYou/ViewModels/`. PhotoGridItem model bridges Photo entity with UI state.

### 2026-04-27 — Initial Architecture (Design Review)

- **Solution split:** Two src projects (`DamYou` MAUI app + `DamYou.Data` class library) plus `tests/` and `tools/`. Minimum useful split for testability without over-engineering.
- **ORM:** EF Core 8 + SQLite provider. Code-first migrations. Rejected Dapper/raw SQL — LINQ is refactor-safe and migrations are version-controlled schema.
- **DB location:** `%LOCALAPPDATA%\DamYou\dam-you.db`. Standard Windows app data path.
- **First-run detection:** `WatchedFolders` active row count == 0. No separate settings flag — folder count is the ground truth.
- **Dialog contract:** `LibrarySetupViewModel` produces a `LibrarySetupResult` record; dialog does not persist — caller does.
- **MVVM toolkit:** CommunityToolkit.Mvvm (`ObservableObject`, `IRelayCommand`, `IAsyncRelayCommand`).
- **Sample photos:** Two-pronged — `tools/seed-photos.ps1` for humans, `SyntheticPhotoFixture` (owned by Ash) for automated tests.
- **Photos are read-only** — the app never moves or modifies files on disk.
- **Windows-only for v1.** No cross-platform targets in scope.
