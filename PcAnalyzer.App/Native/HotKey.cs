using System.Runtime.InteropServices;

namespace PcAnalyzer.App.Native;

/// <summary>
/// Atajo de teclado global: funciona con el juego en primer plano.
/// Sin esto habría que minimizar el juego para volver al panel, que es justo lo
/// que el modo compacto intenta evitar.
/// </summary>
internal static class HotKey
{
    public const int WmHotKey = 0x0312;

    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModNoRepeat = 0x4000;

    /// <summary>
    /// Combinaciones por orden de preferencia. Ctrl+Alt+M es la más natural, pero en
    /// muchos equipos ya la tiene cogida otro programa (utilidades del teclado, capturas,
    /// overlays de fabricante), así que se prueban varias hasta encontrar una libre.
    /// </summary>
    private static readonly (string Label, uint Modifiers, uint VirtualKey)[] Candidates =
    [
        ("Ctrl+Alt+M", ModControl | ModAlt, 0x4D),
        ("Ctrl+Alt+O", ModControl | ModAlt, 0x4F),
        ("Ctrl+Alt+P", ModControl | ModAlt, 0x50),
        ("Ctrl+Alt+K", ModControl | ModAlt, 0x4B),
        ("Ctrl+Shift+M", ModControl | ModShift, 0x4D)
    ];

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);

    /// <summary>
    /// Registra la primera combinación libre y devuelve su nombre para mostrarlo en la UI.
    /// Devuelve false solo si están ocupadas todas.
    /// </summary>
    public static bool TryRegister(IntPtr window, int id, out string label)
    {
        foreach (var (candidateLabel, modifiers, virtualKey) in Candidates)
        {
            if (RegisterHotKey(window, id, modifiers | ModNoRepeat, virtualKey))
            {
                label = candidateLabel;
                return true;
            }
        }

        label = string.Empty;
        return false;
    }

    public static void Unregister(IntPtr window, int id) => UnregisterHotKey(window, id);
}
