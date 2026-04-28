namespace DamYou.Data.Analysis;

public sealed record AnalysisProgress(int Total, int Completed, string? CurrentFile, string? CurrentPass);
public sealed record ModelDownloadProgress(string ModelId, long BytesReceived, long TotalBytes);
