namespace DamYou.Services;

/// <summary>
/// Default implementation of IImportProgressService.
/// A simple event broadcaster that allows import operations to notify subscribers
/// of progress changes without holding direct references.
/// Thread-safe: callers are responsible for marshaling to the UI thread.
/// </summary>
public sealed class ImportProgressService : IImportProgressService
{
    public event Action<int>? ImportStarted;
    public event Action? ImportCompleted;
    public event Action<int, int, string?>? ImportProgressReported;

    public void NotifyImportStarted(int totalCount)
    {
        ImportStarted?.Invoke(totalCount);
    }

    public void NotifyImportCompleted()
    {
        ImportCompleted?.Invoke();
    }

    public void NotifyImportProgress(int totalDiscovered, int processed, string? currentFile)
    {
        ImportProgressReported?.Invoke(totalDiscovered, processed, currentFile);
    }
}
