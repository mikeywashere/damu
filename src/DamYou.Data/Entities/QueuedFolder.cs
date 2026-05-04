namespace DamYou.Data.Entities;

public sealed class QueuedFolder
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FolderPath { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public int Priority { get; set; } = 0;
    public QueueStatus Status { get; set; } = QueueStatus.Pending;
}
