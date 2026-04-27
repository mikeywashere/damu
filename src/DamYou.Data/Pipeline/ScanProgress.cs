namespace DamYou.Data.Pipeline;

public sealed record ScanProgress(int TotalDiscovered, int Enqueued, string? CurrentFolder);
