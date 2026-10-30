using System.Management;
using PcAnalyzer.Core.Native;

namespace PcAnalyzer.Core.Samplers;

/// <summary>
/// Actividad de disco.
///
/// Camino rápido: contadores PDH en inglés (\PhysicalDisk(_Total)\...), que
/// funcionan en cualquier idioma de Windows y cuestan microsegundos.
///
/// Camino de respaldo: si PDH no puede registrar el contador (raro, pero pasa en
/// algunas máquinas virtuales), se usa WMI. Es más lento, así que solo se usa
/// cuando PDH falla de verdad.
/// </summary>
public sealed class DiskSampler : IDisposable
{
    private const string BusyPath = @"\PhysicalDisk(_Total)\% Disk Time";
    private const string ReadPath = @"\PhysicalDisk(_Total)\Disk Read Bytes/sec";
    private const string WritePath = @"\PhysicalDisk(_Total)\Disk Write Bytes/sec";

    private IntPtr _query;
    private IntPtr _busy;
    private IntPtr _read;
    private IntPtr _write;
    private readonly bool _pdhReady;

    public DiskSampler()
    {
        if (!Pdh.TryOpen(out _query))
            return;

        if (!Pdh.TryAdd(_query, BusyPath, out _busy) ||
            !Pdh.TryAdd(_query, ReadPath, out _read) ||
            !Pdh.TryAdd(_query, WritePath, out _write))
        {
            Dispose();
            return;
        }

        // Primera toma de datos: los contadores de tasa necesitan dos muestras.
        Pdh.TryCollect(_query);
        _pdhReady = true;
    }

    public (double Percent, double ReadMbPerSec, double WriteMbPerSec) Next()
    {
        if (!_pdhReady)
            return ReadViaWmi();

        if (!Pdh.TryCollect(_query))
            return (0, 0, 0);

        Pdh.TryRead(_busy, out double busy);
        Pdh.TryRead(_read, out double read);
        Pdh.TryRead(_write, out double write);

        return (
            Math.Clamp(busy, 0, 100),
            read / 1_048_576.0,
            write / 1_048_576.0);
    }

    private static (double Percent, double ReadMbPerSec, double WriteMbPerSec) ReadViaWmi()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, PercentDiskTime, DiskReadBytesPersec, DiskWriteBytesPersec " +
                "FROM Win32_PerfFormattedData_PerfDisk_PhysicalDisk")
            {
                Options = { ReturnImmediately = true, Timeout = TimeSpan.FromSeconds(2) }
            };

            using var results = searcher.Get();

            foreach (ManagementObject disk in results)
            {
                using (disk)
                {
                    if (!string.Equals(disk["Name"]?.ToString(), "_Total", StringComparison.OrdinalIgnoreCase))
                        continue;

                    return (
                        Math.Clamp(Convert.ToDouble(disk["PercentDiskTime"] ?? 0), 0, 100),
                        Convert.ToDouble(disk["DiskReadBytesPersec"] ?? 0) / 1_048_576.0,
                        Convert.ToDouble(disk["DiskWriteBytesPersec"] ?? 0) / 1_048_576.0);
                }
            }
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or FormatException)
        {
        }

        return (0, 0, 0);
    }

    public void Dispose()
    {
        Pdh.Close(_query);
        _query = IntPtr.Zero;
    }
}
