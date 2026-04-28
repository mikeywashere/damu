namespace DamYou.Data.Entities;

public sealed class PhotoOcrText
{
    public int Id { get; set; }
    public int PhotoId { get; set; }
    public Photo Photo { get; set; } = null!;
    public string FullText { get; set; } = string.Empty;
    public byte[]? TextEmbedding { get; set; }   // DistilBERT float32[] as bytes, null if no text found
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
