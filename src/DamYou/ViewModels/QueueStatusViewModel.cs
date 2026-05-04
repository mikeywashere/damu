using CommunityToolkit.Mvvm.ComponentModel;
using DamYou.Services;

namespace DamYou.ViewModels;

/// <summary>
/// Observable ViewModel for queue status display (folder queue + file queue counts).
/// Decoupled from ProcessingStateViewModel to allow lightweight binding in status bars or widgets.
/// </summary>
public sealed partial class QueueStatusViewModel : ObservableObject
{
    private readonly IFolderQueueService _folderQueue;
    private readonly IFileQueueService _fileQueue;

    [ObservableProperty]
    private int folderQueueCount;

    [ObservableProperty]
    private int fileQueueCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentItemShort))]
    private string? currentItemFull;

    /// <summary>
    /// Center-ellipsis truncated display name of the item currently being processed.
    /// Preserves start and end of path so the file name and drive root remain visible.
    /// Returns an empty string when <see cref="CurrentItemFull"/> is null or empty.
    /// </summary>
    public string CurrentItemShort =>
        string.IsNullOrEmpty(CurrentItemFull)
            ? string.Empty
            : TruncateCenter(CurrentItemFull, 40);

    public QueueStatusViewModel(IFolderQueueService folderQueue, IFileQueueService fileQueue)
    {
        _folderQueue = folderQueue;
        _fileQueue = fileQueue;
    }

    /// <summary>Refreshes queue counts from the backing stores.</summary>
    public async Task RefreshQueueCountsAsync(CancellationToken ct = default)
    {
        FolderQueueCount = await _folderQueue.GetCountAsync(ct);
        FileQueueCount = await _fileQueue.GetCountAsync(ct);
    }

    /// <summary>
    /// Truncates a long path with a center ellipsis so both ends remain visible.
    /// Short paths are returned unchanged.
    /// </summary>
    internal static string TruncateCenter(string value, int maxLength)
    {
        if (value.Length <= maxLength)
            return value;

        int half = (maxLength - 3) / 2;
        return string.Concat(value.AsSpan(0, half), "...", value.AsSpan(value.Length - half));
    }
}
