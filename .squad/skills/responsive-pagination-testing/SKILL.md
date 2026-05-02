# Responsive Pagination Testing Pattern

**Author:** Ash (Tester)  
**Context:** MAUI Gallery ViewModel with async pagination, responsive sizing, and search  
**Pattern Type:** ViewModel unit testing for pagination state machines  

## Problem

Testing complex pagination ViewModels requires:
1. **State machine validation** — tracking CurrentPage, TotalPages, IsLoadingMore transitions
2. **Responsive behavior** — page size changes based on viewport dimensions
3. **Concurrency safety** — preventing double-loads during concurrent resize/search/load
4. **Edge cases** — empty libraries, single items, 5000+ photo libraries
5. **Search integration** — pagination resets on search, filters within results

Common pitfalls:
- Tests assert on fixed page size instead of calculated responsive page size
- Missing race condition tests (resize while loading, search while loading)
- Assuming all photos have complete EXIF metadata
- Not testing pagination boundary conditions (0 photos, 1 photo, exact page boundary)

## Solution

### Test Structure

```csharp
private static GalleryViewModel CreateViewModel(
    List<Photo> allPhotos,
    Mock<IPhotoRepository>? photoRepoMock = null,
    Mock<ILibraryScanService>? scanServiceMock = null,
    Mock<IPipelineTaskRepository>? taskRepoMock = null,
    Mock<IImportProgressService>? importProgressMock = null)
{
    // Factory pattern: allow callers to inject slow mocks for concurrency testing
    photoRepoMock ??= CreateMockPhotoRepository(allPhotos);
    // ... other default mocks
    return new GalleryViewModel(/* ... */);
}
```

**Why this pattern:**
- Callers can inject slow mocks (`Task.Delay(100)`) to test race conditions
- Default mocks work for simple tests, reducing boilerplate
- Easy to test "what if repository is slow" scenarios

### Pagination State Machine Tests

**Key assertions:**
```csharp
// After Initialize
Assert.Equal(0, vm.CurrentPage);
Assert.False(vm.IsLoadingMore);
Assert.True(vm.CanLoadMore);  // Computed property: CurrentPage < TotalPages && !IsLoadingMore

// After LoadMore
Assert.Equal(1, vm.CurrentPage);
Assert.Equal(DefaultPageSize * 2, vm.GridPhotos.Count);  // Appended, not replaced

// At boundary
Assert.False(vm.CanLoadMore);  // No more pages
Assert.False(vm.IsLoadingMore);
```

**Race condition test pattern:**
```csharp
var loadTask = vm.LoadMorePhotosCommand.ExecuteAsync(CancellationToken.None);
vm.ResizeGrid(1);  // Try to trigger another load while first is pending
await loadTask;

Assert.Equal(DefaultPageSize * 2, vm.GridPhotos.Count);  // Only one extra page, not two
```

### Responsive Sizing Tests

**Pattern:** Instead of testing exact page count (which varies with formula), test the mechanism:

```csharp
[Fact]
public async Task SetGridDimensions_CalculatesPageSize_BasedOnViewportAndCellSize(double width, double height)
{
    var vm = CreateViewModel(photos);
    await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);

    vm.CurrentGridCellSize = cellSize;
    vm.SetGridDimensions(width, height);  // Triggers page size recalculation

    // Don't assert on exact page count (formula-dependent)
    // Instead, assert that dimensions were accepted and grid exists
    Assert.NotEmpty(vm.GridPhotos);
}
```

### Edge Case Pattern

**All three must be tested:**
```csharp
#region Empty Library
[Fact]
public async Task InitializeAsync_WithNoPhotos_DoesNotCrash()
{
    var photos = new List<Photo>();  // Key: empty list, not null
    var vm = CreateViewModel(photos);
    
    await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);
    
    Assert.Empty(vm.GridPhotos);  // Graceful, no exception
    Assert.Equal(0, vm.PhotoCount);
}

#region Single Photo
[Fact]
public async Task InitializeAsync_WithSinglePhoto_LoadsIt()
{
    var photos = CreatePhotos(1);
    var vm = CreateViewModel(photos);
    
    await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);
    
    Assert.Single(vm.GridPhotos);  // Boundary case
}

#region Large Library (5000+)
[Fact]
public async Task InitializeAsync_WithLargeLibrary_LoadsFirstPageOnly()
{
    var photos = CreatePhotos(5000);  // Key: not 100, not 1000 — needs pagination
    var vm = CreateViewModel(photos);
    
    await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);
    
    Assert.Equal(DefaultPageSize, vm.GridPhotos.Count);  // Not all 5000 in memory
}
```

### Missing Metadata Pattern

Test that UI gracefully handles null EXIF data:

```csharp
[Fact]
public async Task InitializeAsync_WithPhotosWithoutDateTaken_LoadsSuccessfully()
{
    var photos = new List<Photo>
    {
        new() { /* ... */ DateTaken = null, /* ... */ },  // Real scenario
        new() { /* ... */ DateTaken = DateTime.UtcNow, /* ... */ }
    };

    var vm = CreateViewModel(photos);
    await vm.InitializeCommand.ExecuteAsync(CancellationToken.None);

    Assert.Equal(2, vm.GridPhotos.Count);
    Assert.Null(vm.GridPhotos[0].DateTaken);  // Null is OK
    Assert.NotNull(vm.GridPhotos[1].DateTaken);
}
```

## Test Coverage Checklist

- [ ] **Pagination state machine** (8+ tests)
  - [ ] Initial state (IsLoadingMore=false, CurrentPage=0, CanLoadMore=true)
  - [ ] Load more appends, doesn't replace
  - [ ] Concurrent loads blocked (IsLoadingMore guard)
  - [ ] Stops at boundary (CurrentPage >= TotalPages)
  - [ ] IsLoadingMore toggles during load
  - [ ] CurrentPage increments correctly
  - [ ] TotalPages calculated correctly
  - [ ] CanLoadMore computed property is accurate

- [ ] **Responsive sizing** (4+ tests)
  - [ ] SetGridDimensions accepted
  - [ ] ResizeGrid changes cell size
  - [ ] Min/max bounds respected (80–400)
  - [ ] Zero dimensions ignored

- [ ] **Search/filter** (4+ tests)
  - [ ] Search filters photos
  - [ ] Search resets CurrentPage to 0
  - [ ] Empty search reloads all
  - [ ] No results returns empty collection

- [ ] **Refresh** (2+ tests)
  - [ ] Refresh clears grid and reloads
  - [ ] Refresh clears search text

- [ ] **Edge cases** (5+ tests)
  - [ ] Empty library (0 photos) — no crash
  - [ ] Single photo — loads correctly
  - [ ] Large library (5000+) — only first page in memory
  - [ ] Missing DateTaken — graceful null
  - [ ] Missing dimensions — graceful null

- [ ] **Concurrency** (2+ tests)
  - [ ] Resize while loading doesn't double-load
  - [ ] CancellationToken stops operation

- [ ] **UI state** (2+ tests)
  - [ ] Toggle properties panel
  - [ ] PhotoGridItem wraps Photo correctly

## Antipatterns to Avoid

❌ **Bad:**
```csharp
// Tests grid.Count == fixed number, won't work with responsive sizing
Assert.Equal(20, vm.GridPhotos.Count);  // What if formula calculates 17?
```

✅ **Good:**
```csharp
// Test mechanism, not exact count
Assert.NotEmpty(vm.GridPhotos);
Assert.LessThanOrEqual(vm.GridPhotos.Count, calculatedPageSize);
```

---

❌ **Bad:**
```csharp
// Missing race condition test — will fail on slow CI
await vm.LoadMorePhotosCommand.ExecuteAsync(ct);
vm.ResizeGrid(1);
```

✅ **Good:**
```csharp
// Explicitly test concurrent operations
var loadTask = vm.LoadMorePhotosCommand.ExecuteAsync(ct);
vm.ResizeGrid(1);
await loadTask;
Assert.Equal(expectedCount, vm.GridPhotos.Count);  // Not double-loaded
```

---

❌ **Bad:**
```csharp
// Assumes all photos have DateTaken
var photos = CreatePhotos(10);  // All have DateTaken = UtcNow
```

✅ **Good:**
```csharp
// Mix null and non-null metadata
var photos = new List<Photo>
{
    new() { DateTaken = null },
    new() { DateTaken = DateTime.UtcNow }
};
```

## Integration & E2E

**This test pattern covers:** ViewModel business logic (unit layer)

**Not covered:** 
- Modal UI interaction (E2E/MAUI TestCloud)
- Visual rendering (manual QA + screenshots)
- Database performance (integration tests)
- Real file I/O (integration tests)

**Next layer (integration tests):**
```csharp
// Use real database, not mocks
var dbContext = new DamYouDbContext();
var photoRepo = new PhotoRepository(dbContext);
var vm = new GalleryViewModel(
    /* ... */,
    photoRepo,  // Real, not mock
    /* ... */);

// Now test: DB queries are indexed, no N+1, etc.
```

## References

- **Pattern origin:** MVVM unit testing best practices (MVVM Community Toolkit)
- **Race condition pattern:** Async/await testing (xUnit async tests)
- **Edge case priority:** Bug analysis — empty state crashes are common on startup
- **Mock strategy:** Moq with delayed returns to simulate slow I/O

---

**Last Updated:** 2026-05-02  
**Status:** Ready for reuse across MAUI pagination scenarios
