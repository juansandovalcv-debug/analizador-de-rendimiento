using System.Management;
using System.Runtime.InteropServices;

namespace PcAnalyzer.Core;

/// <summary>
/// Detecta si "Integridad de memoria" (HVCI) está activa.
///
/// Importa porque HVCI bloquea los drivers de acceso directo al hardware que usan
/// LibreHardwareMonitor y HWiNFO para leer los MSR del procesador. Con HVCI activo,
/// la temperatura de CPU no aparece NI EJECUTANDO COMO ADMINISTRADOR: Windows rechaza
/// cargar el driver, punto. Es la causa más habitual de "no me sale la temperatura".
///
/// Se consulta una sola vez y se cachea: la consulta WMI cuesta ~50 ms.
/// </summary>
public static class MemoryIntegrity
{
    private const int HvciService = 2;
    private static bool? _cached;

    public static bool IsEnabled()
    {
        if (_cached is bool cached)
            return cached;

        try
        {
            var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\DeviceGuard");
            scope.Connect();

            using var searcher = new ManagementObjectSearcher(
                scope,
                new ObjectQuery("SELECT SecurityServicesRunning FROM Win32_DeviceGuard"));

            foreach (ManagementObject guard in searcher.Get())
            {
                using (guard)
                {
                    if (guard["SecurityServicesRunning"] is not Array services)
                        continue;

                    foreach (var service in services)
                    {
                        if (Convert.ToInt32(service) == HvciService)
                        {
                            _cached = true;
                            return true;
                        }
                    }
                }
            }
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or COMException)
        {
            // En equipos sin virtualización o sin el namespace disponible, se asume desactivado.
        }

        _cached = false;
        return false;
    }
}
