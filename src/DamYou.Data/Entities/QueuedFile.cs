namespace DamYou.Data.Entities;

public sealed class QueuedFile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FilePath { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public QueueStatus Status { get; set; } = QueueStatus.Pending;
}
