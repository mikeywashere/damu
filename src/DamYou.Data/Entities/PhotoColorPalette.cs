namespace DamYou.Data.Entities;

public sealed class PhotoColorPalette
{
    public int Id { get; set; }
    public int PhotoId { get; set; }
    public Photo Photo { get; set; } = null!;
    public string ColorsJson { get; set; } = "[]";  // JSON array of hex strings e.g. ["#FF5733","#C70039"]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
