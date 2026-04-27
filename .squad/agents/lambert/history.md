# Project Context

- **Owner:** Michael R. Schmidt
- **Project:** dam-you — Digital asset management desktop app (MAUI) for a single user to manage their photos.
- **Stack:** .NET MAUI, C#, Windows desktop, XAML, data binding, MVVM pattern
- **Created:** 2026-04-27

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-04-27 — First-Run UI Sprint

- **Converters belong in App.xaml MergedDictionaries.** IntToBoolConverter and InvertBoolConverter must be registered in App.xaml (not just Styles.xaml) so they resolve from the StaticResource extension inside ContentPage XAML.
- **FolderPickerService is Windows-only.** It uses `MauiWinUIWindow` and `InitializeWithWindow` from WinRT interop. Do not reference it from shared/cross-platform code paths.
- **Parker scaffolds before Lambert wires.** App.xaml.cs and MauiProgram.cs exist as boilerplate from `dotnet new maui` — always replace in full, not patch.
- **CanComplete must notify via CollectionChanged.** Because `SelectedFolders` is an ObservableCollection<string>, `CanComplete` doesn't auto-notify unless we explicitly call `OnPropertyChanged(nameof(CanComplete))` in the CollectionChanged handler.
- **NavigationPage wraps the first view.** `App.CreateWindow` returns `new Window(new NavigationPage(...))` so the shell can `PushAsync` on setup completion without a separate Shell route.

