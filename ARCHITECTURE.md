# dam-you — Architecture

> Digital Asset Manager (photo library) — .NET MAUI desktop app, Windows, single user.  
> **Last updated:** 2026-04-27 by Dallas

---

## Solution Structure

```
dam-you/
├── dam-you.sln
├── ARCHITECTURE.md               ← this file
├── src/
│   ├── DamYou/                   ← MAUI app project
│   │   ├── DamYou.csproj
│   │   ├── App.xaml / App.xaml.cs
│   │   ├── MauiProgram.cs
│   │   ├── Views/
│   │   │   ├── LibrarySetupView.xaml
│   │   │   └── LibraryView.xaml
│   │   ├── ViewModels/
│   │   │   ├── LibrarySetupViewModel.cs
│   │   │   └── LibraryViewModel.cs
│   │   └── Services/
│   │       └── FolderPickerService.cs  ← platform abstraction for OS folder picker
│   └── DamYou.Data/              ← class library: data layer
│       ├── DamYou.Data.csproj
│       ├── DamYouDbContext.cs
│       ├── Entities/
│       │   ├── WatchedFolder.cs
│       │   └── Photo.cs
│       ├── Repositories/
│       │   ├── IFolderRepository.cs
│       │   └── FolderRepository.cs
│       └── Migrations/           ← EF Core generated migrations
├── tests/
│   └── DamYou.Tests/
│       ├── DamYou.Tests.csproj
│       └── Fixtures/
│           └── SyntheticPhotoFixture.cs
└── tools/
    └── seed-photos.ps1           ← downloads sample CC photos for dev
```

---

## SQLite Approach

**EF Core 8 + Microsoft.EntityFrameworkCore.Sqlite**

Code-first, with migrations. No raw SQL strings.

**Packages in `DamYou.Data`:**
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.*" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.*" PrivateAssets="all" />
```

**DB file location:**  
`%LOCALAPPDATA%\DamYou\dam-you.db`  
Resolved at runtime via `Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)`.

**Startup sequence:**  
`MauiProgram.cs` registers `DamYouDbContext` with DI, then on app start calls `await db.Database.MigrateAsync()` — creates the DB and applies all pending migrations before any UI is shown.

---

## Database Schema

### `WatchedFolders`

| Column | Type | Constraints |
|--------|------|-------------|
| `Id` | `INTEGER` | PK, autoincrement |
| `Path` | `TEXT` | NOT NULL, UNIQUE |
| `DateAdded` | `TEXT` | NOT NULL (ISO-8601 UTC) |
| `IsActive` | `INTEGER` | NOT NULL, default 1 |

### `Photos`

| Column | Type | Constraints |
|--------|------|-------------|
| `Id` | `INTEGER` | PK, autoincrement |
| `WatchedFolderId` | `INTEGER` | NOT NULL, FK → WatchedFolders.Id |
| `FileName` | `TEXT` | NOT NULL |
| `FilePath` | `TEXT` | NOT NULL, UNIQUE |
| `FileSizeBytes` | `INTEGER` | NOT NULL |
| `FileHash` | `TEXT` | NULL (SHA-256; populated by indexer) |
| `DateTaken` | `TEXT` | NULL (EXIF DateTimeOriginal, ISO-8601 UTC) |
| `Width` | `INTEGER` | NULL |
| `Height` | `INTEGER` | NULL |
| `DateIndexed` | `TEXT` | NOT NULL |
| `IsDeleted` | `INTEGER` | NOT NULL, default 0 |

**Indexes:**
- `IX_Photos_WatchedFolderId`
- `IX_Photos_DateTaken`

---

## First-Run Detection

The app is "first run" when `WatchedFolders` has no active rows.

```
App start
  → MigrateAsync()          (creates DB if missing)
  → count = WatchedFolders.Count(f => f.IsActive)
  → count == 0  →  show LibrarySetupView (modal, blocks LibraryView)
  → count  > 0  →  show LibraryView
```

No separate settings flag. The folder count is the ground truth.

---

## First-Run Dialog Contract

### Input
None. The dialog is invoked when the folder count is zero.

### Output
```csharp
public sealed record LibrarySetupResult(IReadOnlyList<string> SelectedFolderPaths);
```

The dialog returns this record. The caller (App shell / navigation service) receives it and persists via `IFolderRepository.AddFoldersAsync(paths)`. The dialog does **not** write to the DB itself.

### Interfaces

```csharp
// Parker implements this
public interface IFolderRepository
{
    Task<IReadOnlyList<WatchedFolder>> GetActiveFoldersAsync();
    Task AddFoldersAsync(IEnumerable<string> paths);
    Task DeactivateFolderAsync(int id);
}

// Lambert implements this (platform-specific)
public interface IFolderPickerService
{
    /// <summary>Opens the OS folder picker. Returns null if user cancels.</summary>
    Task<string?> PickFolderAsync();
}
```

---

## ViewModel Contracts

### `LibrarySetupViewModel`

```csharp
public sealed class LibrarySetupViewModel : ObservableObject
{
    // Constructor
    public LibrarySetupViewModel(
        IFolderRepository folderRepository,
        IFolderPickerService folderPickerService);

    // State
    public ObservableCollection<string> SelectedFolders { get; }
    public bool CanComplete { get; }   // true when SelectedFolders.Count > 0
    public bool IsComplete { get; }    // flips to true after CompleteSetupCommand succeeds

    // Commands
    public IAsyncRelayCommand AddFolderCommand { get; }        // calls IFolderPickerService
    public IRelayCommand<string> RemoveFolderCommand { get; }  // removes path from collection
    public IAsyncRelayCommand CompleteSetupCommand { get; }    // persists, sets IsComplete=true
}
```

**Shell behavior:** The App shell observes `IsComplete`. When `true`, it navigates to `LibraryView`.

### `LibraryViewModel` *(stub — future sprint)*

```csharp
public sealed class LibraryViewModel : ObservableObject
{
    // Will hold the photo grid state, selected photo, filter/sort state
    // Defined here so Lambert knows the shape; Parker fills in the data layer later
    public ObservableCollection<PhotoSummary> Photos { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
}
```

---

## Sample Photo Strategy

### For humans (dev / UI work)
`tools/seed-photos.ps1` — downloads ~20 free Creative Commons JPEG photos from Wikimedia Commons into `C:\dev\dam-you-sample-photos\`. Run once per dev machine. Checked in to the repo.

```powershell
# Usage
.\tools\seed-photos.ps1
# → downloads to C:\dev\dam-you-sample-photos\
```

### For automated tests
`tests/DamYou.Tests/Fixtures/SyntheticPhotoFixture.cs` — generates valid JPEG files with known EXIF metadata (deterministic DateTaken, dimensions, file size) into a temp directory. Owned by Ash. Tests are self-contained and work on CI without network access or a pre-seeded folder.

---

## Dependency Injection Setup

In `MauiProgram.cs`:

```csharp
builder.Services.AddDbContext<DamYouDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddScoped<IFolderRepository, FolderRepository>();
builder.Services.AddSingleton<IFolderPickerService, FolderPickerService>();

builder.Services.AddTransient<LibrarySetupViewModel>();
builder.Services.AddTransient<LibraryViewModel>();
```

---

## NuGet Package Summary

| Project | Package | Purpose |
|---------|---------|---------|
| `DamYou.Data` | `Microsoft.EntityFrameworkCore.Sqlite 8.*` | ORM + SQLite driver |
| `DamYou.Data` | `Microsoft.EntityFrameworkCore.Design 8.*` | Migration tooling (dev only) |
| `DamYou` | `CommunityToolkit.Mvvm` | `ObservableObject`, `IRelayCommand` |
| `DamYou.Tests` | `xunit`, `Moq`, `Microsoft.EntityFrameworkCore.InMemory` | Testing |

---

## Key Constraints

- **Windows only** for v1. No Android/iOS targets in scope.
- **Single user.** No sync, no cloud, no multi-user concurrency.
- **Local DB only.** No server. DB lives in `%LOCALAPPDATA%\DamYou\`.
- **Photos are never moved or modified** by this app. Read-only access to the filesystem.
