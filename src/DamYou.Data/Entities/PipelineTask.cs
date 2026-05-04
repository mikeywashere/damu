using DamYou.Data.Pipeline;

namespace DamYou.Data.Entities;

public sealed class PipelineTask
{
    public int Id { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public PipelineTaskStatus Status { get; set; } = PipelineTaskStatus.Queued;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? PhotoId { get; set; }
    public Photo? Photo { get; set; }
    public string? ErrorMessage { get; set; }

    // Progress tracking
    public string? CurrentItemName { get; set; }

    public int CurrentItemIndex { get; set; }
    public int TotalItems { get; set; }
}