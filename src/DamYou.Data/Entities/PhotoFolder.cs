namespace DamYou.Data.Entities;

public sealed class PhotoFolder
{
    public int Id { get; set; }
    public required string FolderPath { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
