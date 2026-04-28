namespace DamYou.Data.Entities;

public sealed class PhotoEmbedding
{
    public int Id { get; set; }
    public int PhotoId { get; set; }
    public Photo Photo { get; set; } = null!;
    public string ModelName { get; set; } = string.Empty;  // "clip-vit-b32" or "clip-vit-l14"
    public int Dimensions { get; set; }                     // 512 or 768
    public byte[] Embedding { get; set; } = [];            // float32[] serialized as little-endian bytes
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
