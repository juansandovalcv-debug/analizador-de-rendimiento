using System.Management;
using System.Net.NetworkInformation;
using PcAnalyzer.Core.Models;

namespace PcAnalyzer.Core;

/// <summary>
/// Lee los datos fijos del equipo con WMI. Es lento (100-500 ms), así que se llama
/// UNA sola vez al arrancar y siempre desde un hilo de fondo.
/// </summary>
public static class HardwareInfoProvider
{
    public static HardwareInfo Get()
    {
        var (cpuName, cores, threads, maxClock) = ReadCpu();
        double ramGb = ReadTotalRam();
        string gpuName = ReadGpu();

        return new HardwareInfo(
            Environment.MachineName,
            cpuName,
            cores,
            threads,
            maxClock,
            ramGb,
            gpuName,
            ReadDisk(),
            ReadNetworkAdapter(),
            ReadMotherboard(),
            ReadOs());
    }

    private static (string Name, int Cores, int Threads, uint MaxClockMhz) ReadCpu()
    {
        string name = "CPU desconocida";
        int cores = 0;
        int threads = 0;
        uint maxClock = 0;

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor");

            foreach (ManagementObject cpu in searcher.Get())
            {
                using (cpu)
                {
                    name = (cpu["Name"]?.ToString() ?? name).Trim();
                    cores = ToInt(cpu["NumberOfCores"]);
                    threads = ToInt(cpu["NumberOfLogicalProcessors"]);
                    maxClock = (uint)ToInt(cpu["MaxClockSpeed"]);
                    break;
                }
            }
        }
        catch (ManagementException)
        {
        }

        return (name, cores, threads, maxClock);
    }

    private static double ReadTotalRam()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");

            foreach (ManagementObject system in searcher.Get())
            {
                using (system)
                {
                    return ToDouble(system["TotalPhysicalMemory"]) / (1024d * 1024d * 1024d);
                }
            }
        }
        catch (ManagementException)
        {
        }

        return 0;
    }

    /// <summary>
    /// Devuelve la GPU dedicada si existe. Un portátil híbrido expone dos adaptadores
    /// y WMI los lista en orden arbitrario: quedarse con el primero da la integrada,
    /// que es justo la que nadie quiere ver en el panel.
    /// </summary>
    private static string ReadGpu()
    {
        var candidates = new List<string>();

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");

            foreach (ManagementObject gpu in searcher.Get())
            {
                using (gpu)
                {
                    string name = (gpu["Name"]?.ToString() ?? string.Empty).Trim();

                    if (name.Length == 0 || name.Contains("Basic Display", StringComparison.OrdinalIgnoreCase))
                        continue;

                    candidates.Add(name);
                }
            }
        }
        catch (ManagementException)
        {
        }

        if (candidates.Count == 0)
            return "GPU desconocida";

        string? discrete = candidates.FirstOrDefault(n =>
            n.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("Radeon", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
            n.Contains("Arc", StringComparison.OrdinalIgnoreCase));

        return discrete ?? candidates[0];
    }

    private static string ReadDisk()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Model FROM Win32_DiskDrive");

            foreach (ManagementObject disk in searcher.Get())
            {
                using (disk)
                {
                    string model = (disk["Model"]?.ToString() ?? string.Empty).Trim();

                    if (model.Length > 0)
                        return model;
                }
            }
        }
        catch (ManagementException)
        {
        }

        return "Disco";
    }

    private static string ReadNetworkAdapter()
    {
        // Los adaptadores virtuales (VPN, Hyper-V, Bluetooth) se anuncian como Ethernet
        // y suelen ser los primeros de la lista. Si no se filtran, el panel acaba
        // mostrando "Radmin VPN" en lugar de la Wi-Fi real.
        string[] virtualKeywords =
        [
            "VPN", "Virtual", "TAP", "TUN", "Wintun", "Hyper-V", "VMware",
            "VirtualBox", "Bluetooth", "Loopback", "Pseudo", "Radmin",
            "ZeroTier", "Tailscale", "WireGuard", "Npcap"
        ];

        try
        {
            NetworkInterface? best = null;

            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up)
                    continue;

                if (nic.NetworkInterfaceType is not (NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211))
                    continue;

                string label = $"{nic.Name} {nic.Description}";
                if (virtualKeywords.Any(k => label.Contains(k, StringComparison.OrdinalIgnoreCase)))
                    continue;

                if (best is null || nic.Speed > best.Speed)
                    best = nic;
            }

            if (best is not null)
                return best.Name;
        }
        catch (NetworkInformationException)
        {
        }

        return "Red";
    }

    private static string ReadMotherboard()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Manufacturer, Product FROM Win32_BaseBoard");

            foreach (ManagementObject board in searcher.Get())
            {
                using (board)
                {
                    string maker = (board["Manufacturer"]?.ToString() ?? string.Empty).Trim();
                    string product = (board["Product"]?.ToString() ?? string.Empty).Trim();
                    return $"{maker} {product}".Trim();
                }
            }
        }
        catch (ManagementException)
        {
        }

        return "Placa desconocida";
    }

    private static string ReadOs()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem");

            foreach (ManagementObject system in searcher.Get())
            {
                using (system)
                {
                    return (system["Caption"]?.ToString() ?? "Windows").Replace("Microsoft ", string.Empty).Trim();
                }
            }
        }
        catch (ManagementException)
        {
        }

        return "Windows";
    }

    private static int ToInt(object? value)
    {
        try
        {
            return value is null ? 0 : Convert.ToInt32(value);
        }
        catch
        {
            return 0;
        }
    }

    private static double ToDouble(object? value)
    {
        try
        {
            return value is null ? 0 : Convert.ToDouble(value);
        }
        catch
        {
            return 0;
        }
    }
}
