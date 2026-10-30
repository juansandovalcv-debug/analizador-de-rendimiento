using System.Runtime.InteropServices;
using PcAnalyzer.Core.Native;

namespace PcAnalyzer.Core.Samplers;

/// <summary>Memoria física: % en uso, GB usados y GB totales.</summary>
public sealed class MemorySampler
{
    public (double Percent, double UsedGb, double TotalGb) Next()
    {
        var status = new NativeMethods.MEMORYSTATUSEX
        {
            dwLength = (uint)Marshal.SizeOf<NativeMethods.MEMORYSTATUSEX>()
        };

        if (!NativeMethods.GlobalMemoryStatusEx(ref status))
            return (0, 0, 0);

        const double bytesPerGb = 1024d * 1024d * 1024d;

        double total = status.ullTotalPhys / bytesPerGb;
        double used = (status.ullTotalPhys - status.ullAvailPhys) / bytesPerGb;

        return (Math.Clamp(status.dwMemoryLoad, 0, 100), used, total);
    }
}
