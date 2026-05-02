namespace DamYou.Services;

/// <summary>
/// Service for notifying subscribers about import progress.
/// Decouples the import operation from the UI layer.
/// </summary>
public interface IImportProgressService
{
    /// <summary>
    /// Invoked when import starts. Args: total files to import.
    /// </summary>
    event Action<int>? ImportStarted;

    /// <summary>
    /// Invoked when import completes.
    /// </summary>
    event Action? ImportCompleted;

    /// <summary>
    /// Invoked when progress is reported during import.
    /// Args: (totalDiscovered, processed, currentFile)
    /// </summary>
    event Action<int, int, string?>? ImportProgressReported;

    /// <summary>
    /// Notifies that import has started.
    /// </summary>
    void NotifyImportStarted(int totalCount);

    /// <summary>
    /// Notifies that import has completed.
    /// </summary>
    void NotifyImportCompleted();

    /// <summary>
    /// Notifies of progress during import.
    /// </summary>
    void NotifyImportProgress(int totalDiscovered, int processed, string? currentFile);
}
