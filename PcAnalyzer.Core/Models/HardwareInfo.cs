namespace PcAnalyzer.Core.Models;

/// <summary>
/// Datos del equipo que no cambian mientras la app está abierta.
/// Se leen una sola vez al arrancar (WMI es lento, no se consulta cada segundo).
/// </summary>
public sealed record HardwareInfo(
    string MachineName,
    string CpuName,
    int Cores,
    int Threads,
    uint MaxClockMhz,
    double RamTotalGb,
    string GpuName,
    string DiskName,
    string NetworkName,
    string Motherboard,
    string OsName)
{
    public string Summary =>
        $"{CpuName}  ·  {Cores}C/{Threads}T  ·  {RamTotalGb:F0} GB RAM  ·  {GpuName}";
}
