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

