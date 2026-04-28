using System.Management;
using System.Runtime.Versioning;

namespace DamYou.Data.Analysis;

#if WINDOWS

[SupportedOSPlatform("windows")]
public sealed class HardwareDetectionService : IHardwareDetectionService
{
    private readonly Lazy<HardwareCapabilities> _capabilities;

    public HardwareDetectionService()
    {
        _capabilities = new Lazy<HardwareCapabilities>(DetectInternal, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    private static HardwareCapabilities DetectInternal()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController");
            long maxVram = 0;
            string adapterName = "Unknown";
            bool hasDedicated = false;

            foreach (ManagementObject obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString() ?? string.Empty;
                // AdapterRAM is uint32; 0xFFFFFFFF means >= 4GB (caps at 32-bit max)
                var ramRaw = obj["AdapterRAM"];
                long vram = ramRaw is null ? 0 : Convert.ToInt64(ramRaw);
                if (vram == 0xFFFFFFFF) vram = 4L * 1024 * 1024 * 1024; // treat as 4GB+
                if (vram > maxVram)
                {
                    maxVram = vram;
                    adapterName = name;
                    // Simple heuristic: if not integrated (Intel UHD/Iris), treat as dedicated
                    hasDedicated = !name.Contains("UHD", StringComparison.OrdinalIgnoreCase)
                                && !name.Contains("Iris", StringComparison.OrdinalIgnoreCase);
                }
            }
            return new HardwareCapabilities(hasDedicated, maxVram,
                adapterName.Contains("Intel", StringComparison.OrdinalIgnoreCase), adapterName);
        }
        catch
        {
            return new HardwareCapabilities(false, 0, false, "Unknown");
        }
    }

    public HardwareCapabilities Detect() => _capabilities.Value;
    public bool CanRunLargeClipModel() => _capabilities.Value.VramBytes >= 4L * 1024 * 1024 * 1024;
    public bool IsIntelHardware() => _capabilities.Value.IsIntelHardware;
    public string GetRecommendedClipModel() => CanRunLargeClipModel() ? "clip-vit-l14" : "clip-vit-b32";
}

#endif
