using System.Runtime.InteropServices;

namespace PcAnalyzer.Core.Native;

/// <summary>
/// Llamadas nativas a Windows.
///
/// ¿Por qué no usar PerformanceCounter? Porque los nombres de categorías y contadores
/// están TRADUCIDOS al idioma de Windows: "Processor" en inglés es "Procesador" en
/// español. Si hardcodeas los nombres en inglés la app funciona en tu PC y falla en la
/// del cliente. Estas APIs nativas devuelven números, no texto, y son inmunes al idioma.
/// </summary>
internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct FILETIME
    {
        public uint Low;
        public uint High;

        public readonly ulong ToUInt64() => ((ulong)High << 32) | Low;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetSystemTimes(out FILETIME idleTime, out FILETIME kernelTime, out FILETIME userTime);

    [StructLayout(LayoutKind.Sequential)]
    internal struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    internal struct PROCESSOR_POWER_INFORMATION
    {
        public uint Number;
        public uint MaxMhz;
        public uint CurrentMhz;
        public uint MhzLimit;
        public uint MaxIdleState;
        public uint CurrentIdleState;
    }

    private const int ProcessorInformation = 11;

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern uint CallNtPowerInformation(
        int informationLevel,
        IntPtr inputBuffer,
        uint inputBufferSize,
        IntPtr outputBuffer,
        uint outputBufferSize);

    /// <summary>Frecuencia real actual del núcleo más rápido, en MHz.</summary>
    internal static uint GetCurrentClockMhz()
    {
        int count = Environment.ProcessorCount;
        int size = Marshal.SizeOf<PROCESSOR_POWER_INFORMATION>();
        IntPtr buffer = Marshal.AllocHGlobal(size * count);

        try
        {
            uint status = CallNtPowerInformation(
                ProcessorInformation, IntPtr.Zero, 0, buffer, (uint)(size * count));

            if (status != 0)
                return 0;

            uint max = 0;
            for (int i = 0; i < count; i++)
            {
                var info = Marshal.PtrToStructure<PROCESSOR_POWER_INFORMATION>(buffer + (i * size));
                if (info.CurrentMhz > max)
                    max = info.CurrentMhz;
            }

            return max;
        }
        catch
        {
            return 0;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
