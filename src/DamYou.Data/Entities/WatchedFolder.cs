namespace DamYou.Data.Entities;

public sealed class WatchedFolder
{
    public int Id { get; set; }
    public required string Path { get; set; }
    public DateTime DateAdded { get; set; }
    public bool IsActive { get; set; } = true;
}
