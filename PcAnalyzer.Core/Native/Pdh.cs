using System.Runtime.InteropServices;

namespace PcAnalyzer.Core.Native;

/// <summary>
/// PDH (Performance Data Helper) es la API nativa de contadores de rendimiento.
///
/// La clave está en <c>PdhAddEnglishCounter</c>: acepta el nombre del contador
/// en INGLÉS aunque Windows esté en español, alemán o japonés. PDH lo traduce
/// internamente. Eso resuelve el problema de PerformanceCounter, que exige el
/// nombre traducido del sistema.
///
/// Además es rapidísimo: leer un contador cuesta microsegundos, mientras que una
/// consulta WMI equivalente cuesta 300-400 ms.
/// </summary>
internal static class Pdh
{
    private const uint FmtDouble = 0x00000200;
    private const uint MoreData = 0x800007D2;

    [StructLayout(LayoutKind.Explicit)]
    internal struct FmtCounterValue
    {
        [FieldOffset(0)] public uint Status;
        [FieldOffset(8)] public double Double;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct FmtCounterValueItem
    {
        public IntPtr Name;
        public FmtCounterValue Value;
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint PdhOpenQuery(string? dataSource, IntPtr userData, out IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint PdhAddEnglishCounter(IntPtr query, string fullCounterPath, IntPtr userData, out IntPtr counter);

    [DllImport("pdh.dll")]
    private static extern uint PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll")]
    private static extern uint PdhGetFormattedCounterValue(IntPtr counter, uint format, out uint type, out FmtCounterValue value);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhGetFormattedCounterArray(IntPtr counter, uint format, ref uint bufferSize, out uint itemCount, IntPtr buffer);

    [DllImport("pdh.dll")]
    private static extern uint PdhCloseQuery(IntPtr query);

    internal static bool TryOpen(out IntPtr query) => PdhOpenQuery(null, IntPtr.Zero, out query) == 0;

    internal static bool TryAdd(IntPtr query, string path, out IntPtr counter) =>
        PdhAddEnglishCounter(query, path, IntPtr.Zero, out counter) == 0;

    internal static bool TryCollect(IntPtr query) => PdhCollectQueryData(query) == 0;

    internal static bool TryRead(IntPtr counter, out double value)
    {
        value = 0;

        if (PdhGetFormattedCounterValue(counter, FmtDouble, out _, out var raw) != 0)
            return false;

        if (raw.Status != 0)
            return false;

        value = raw.Double;
        return true;
    }

    /// <summary>
    /// Lee un contador con instancias comodín (por ejemplo
    /// <c>\GPU Engine(*engtype_3D)\Utilization Percentage</c>) y devuelve el
    /// valor de cada instancia encontrada.
    /// </summary>
    internal static double[] ReadArray(IntPtr counter)
    {
        uint size = 0;
        uint status = PdhGetFormattedCounterArray(counter, FmtDouble, ref size, out _, IntPtr.Zero);

        if (status != MoreData || size == 0)
            return [];

        IntPtr buffer = Marshal.AllocHGlobal((int)size);

        try
        {
            status = PdhGetFormattedCounterArray(counter, FmtDouble, ref size, out uint count, buffer);

            if (status != 0 || count == 0)
                return [];

            var values = new double[count];
            int itemSize = Marshal.SizeOf<FmtCounterValueItem>();

            for (int i = 0; i < count; i++)
            {
                var item = Marshal.PtrToStructure<FmtCounterValueItem>(buffer + (i * itemSize));
                values[i] = item.Value.Status == 0 ? item.Value.Double : 0;
            }

            return values;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    internal static void Close(IntPtr query)
    {
        if (query != IntPtr.Zero)
            PdhCloseQuery(query);
    }
}
