namespace DamYou.Data.Analysis;

public sealed record DetectedObject(
    string Label,
    float Confidence,
    float X, float Y, float Width, float Height);
