namespace DamYou.Data.Entities;

public sealed class PhotoDetectedObject
{
    public int Id { get; set; }
    public int PhotoId { get; set; }
    public Photo Photo { get; set; } = null!;
    public string Label { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public float BoundingBoxX { get; set; }      // normalized 0-1
    public float BoundingBoxY { get; set; }
    public float BoundingBoxWidth { get; set; }
    public float BoundingBoxHeight { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
