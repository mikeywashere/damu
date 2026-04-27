# Project Context

- **Owner:** Michael R. Schmidt
- **Project:** dam-you — Digital asset management desktop app (MAUI) for a single user to manage their photos.
- **Stack:** .NET MAUI, C#, Windows desktop, file I/O, EXIF metadata, SQLite, async patterns
- **Created:** 2026-04-27

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-04-27 — Initial data layer scaffold

- `dotnet new sln` on the installed SDK creates `.slnx` format (not `.sln`). All `dotnet sln` commands must target `dam-you.slnx`.
- `dotnet new maui --framework net9.0-windows10.0.19041.0` is rejected by the template; the MAUI template only accepts `net9.0` or `net10.0`. The Windows TFM (`net9.0-windows10.0.19041.0`) is baked into the generated `.csproj` automatically — no need to pass it on the command line.
- `ToHashSetAsync` does not exist in EF Core 8 on `IQueryable<T>`. Use `ToListAsync` followed by `.ToHashSet()` instead.
- `DamYou.Data` builds clean (0 errors, 0 warnings) with EF Core 8.* on net9.0.
