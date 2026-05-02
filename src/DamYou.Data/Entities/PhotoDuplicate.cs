namespace DamYou.Data.Entities;

public sealed class PhotoDuplicate
{
    public int Id { get; set; }
    public int PhotoId { get; set; }
    public Photo Photo { get; set; } = null!;
    public required string FilePath { get; set; }
    public required string FileName { get; set; }
    public DateTime DateDiscovered { get; set; }
}
