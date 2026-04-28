namespace DamYou.Data.Analysis;

public sealed record HardwareCapabilities(
    bool HasDedicatedGpu,
    long VramBytes,
    bool IsIntelHardware,
    string AdapterName);

public interface IHardwareDetectionService
{
    HardwareCapabilities Detect();
    bool CanRunLargeClipModel();   // returns true if VRAM >= 4GB
    bool IsIntelHardware();
    string GetRecommendedClipModel(); // "clip-vit-b32" or "clip-vit-l14"
}
