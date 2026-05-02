using CommunityToolkit.Mvvm.ComponentModel;
using DamYou.Data.Entities;

namespace DamYou.Models;

/// <summary>
/// Wraps a Photo entity with UI-specific properties for grid display.
/// </summary>
public sealed partial class PhotoGridItem : ObservableObject
{
    private readonly Photo _photo;

    [ObservableProperty]
    private string? _processingStatusIcon;

    [ObservableProperty]
    private Color _processingStatusColor = Colors.Transparent;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private List<string> _allFilePaths = [];

    public Photo Photo => _photo;
    public int Id => _photo.Id;
    public string FileName => _photo.FileName;
    public string FilePath => _photo.FilePath;
    public long FileSizeBytes => _photo.FileSizeBytes;
    public DateTime DateIndexed => _photo.DateIndexed;
    public DateTime? DateTaken => _photo.DateTaken;
    public int? Width => _photo.Width;
    public int? Height => _photo.Height;

    /// <summary>
    /// Gets a displayable string of all file paths (for hover tooltip).
    /// </summary>
    public string FilePathsDisplay =>
        AllFilePaths.Count == 1
            ? AllFilePaths[0]
            : $"Stored in {AllFilePaths.Count} locations:\n" + string.Join("\n", AllFilePaths);

    public PhotoGridItem(Photo photo)
    {
        _photo = photo;
        UpdateStatusDisplay();
    }

    /// <summary>
    /// Updates the status icon and color based on the photo's processing state.
    /// </summary>
    public void UpdateStatusDisplay()
    {
        switch (_photo.Status)
        {
            case ProcessingStatus.Processed:
                ProcessingStatusIcon = "✓";
                ProcessingStatusColor = Colors.Green;
                IsProcessing = false;
                break;
            case ProcessingStatus.Processing:
                ProcessingStatusIcon = "⟳";
                ProcessingStatusColor = Colors.Orange;
                IsProcessing = true;
                break;
            case ProcessingStatus.Unprocessed:
            default:
                ProcessingStatusIcon = null;
                ProcessingStatusColor = Colors.Transparent;
                IsProcessing = false;
                break;
        }
    }
}
