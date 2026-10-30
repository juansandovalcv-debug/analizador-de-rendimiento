using System.Management;
using PcAnalyzer.Core.Native;

namespace PcAnalyzer.Core.Samplers;

/// <summary>
/// Uso de GPU y VRAM.
///
/// Los contadores "\GPU Engine(*engtype_3D)\Utilization Percentage" desglosan la
/// carga por proceso y por motor (3D, Copy, VideoDecode...). "engtype_3D" es el
/// que refleja juegos y render. Se suman todas las instancias y se limita a 100.
///
/// Se usa PDH; si no está disponible, se cae a WMI (mismos datos, 300 ms más lento).
/// </summary>
public sealed class GpuSampler : IDisposable
{
    private const string EnginePath = @"\GPU Engine(*engtype_3D)\Utilization Percentage";
    private const string MemoryPath = @"\GPU Adapter Memory(*)\Dedicated Usage";

    private IntPtr _query;
    private IntPtr _engine;
    private IntPtr _memory;
    private readonly bool _pdhReady;

    public GpuSampler()
    {
        if (!Pdh.TryOpen(out _query))
            return;

        // El contador de VRAM es opcional: en GPUs integradas o Windows antiguos
        // puede no existir y no debe impedir medir el uso de la GPU.
        Pdh.TryAdd(_query, MemoryPath, out _memory);

        if (!Pdh.TryAdd(_query, EnginePath, out _engine))
        {
            Dispose();
            return;
        }

        Pdh.TryCollect(_query);
        _pdhReady = true;
    }

    public (double? Percent, double? DedicatedMb) Next()
    {
        if (!_pdhReady)
            return ReadViaWmi();

        if (!Pdh.TryCollect(_query))
            return (null, null);

        double? percent = null;
        double? memory = null;

        var engines = Pdh.ReadArray(_engine);
        if (engines.Length > 0)
            percent = Math.Clamp(engines.Sum(), 0, 100);

        if (_memory != IntPtr.Zero)
        {
            var memoryValues = Pdh.ReadArray(_memory);
            if (memoryValues.Length > 0)
                memory = memoryValues.Sum() / 1_048_576.0;
        }

        return (percent, memory);
    }

    private static (double? Percent, double? DedicatedMb) ReadViaWmi()
    {
        double? percent = null;
        double? memory = null;

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, UtilizationPercentage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine")
            {
                Options = { ReturnImmediately = true, Timeout = TimeSpan.FromSeconds(5) }
            };

            using var results = searcher.Get();

            double sum = 0;
            bool found = false;

            foreach (ManagementObject engine in results)
            {
                using (engine)
                {
                    string name = engine["Name"]?.ToString() ?? string.Empty;

                    if (!name.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase))
                        continue;

                    sum += Convert.ToDouble(engine["UtilizationPercentage"] ?? 0);
                    found = true;
                }
            }

            if (found)
                percent = Math.Clamp(sum, 0, 100);
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or FormatException)
        {
        }

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT DedicatedUsage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUAdapterMemory")
            {
                Options = { ReturnImmediately = true, Timeout = TimeSpan.FromSeconds(5) }
            };

            using var results = searcher.Get();

            double bytes = 0;
            bool found = false;

            foreach (ManagementObject adapter in results)
            {
                using (adapter)
                {
                    bytes += Convert.ToDouble(adapter["DedicatedUsage"] ?? 0);
                    found = true;
                }
            }

            if (found)
                memory = bytes / 1_048_576.0;
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or FormatException)
        {
        }

        return (percent, memory);
    }

    public void Dispose()
    {
        Pdh.Close(_query);
        _query = IntPtr.Zero;
    }
}
