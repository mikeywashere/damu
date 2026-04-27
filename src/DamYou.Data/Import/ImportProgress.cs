namespace DamYou.Data.Import;

public sealed record ImportProgress(int TotalDiscovered, int Processed, string? CurrentFile);
