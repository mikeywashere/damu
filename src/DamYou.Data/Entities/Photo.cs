namespace DamYou.Data.Entities;

public sealed class Photo
{
    public int Id { get; set; }
    public int WatchedFolderId { get; set; }
    public WatchedFolder WatchedFolder { get; set; } = null!;
    public required string FileName { get; set; }
    public required string FilePath { get; set; }
    public long FileSizeBytes { get; set; }
    public string? FileHash { get; set; }
    public DateTime? DateTaken { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public DateTime DateIndexed { get; set; }
    public bool IsDeleted { get; set; } = false;
    public bool IsProcessed { get; set; } = false;
}
