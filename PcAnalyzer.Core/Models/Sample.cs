namespace PcAnalyzer.Core.Models;

/// <summary>
/// Una "foto" del sistema en un instante concreto. Es inmutable y no depende de WinForms,
/// así que se puede testear y serializar sin abrir ninguna ventana.
/// </summary>
public readonly record struct Sample(
    DateTime Timestamp,
    double CpuPercent,
    uint CpuClockMhz,
    double RamPercent,
    double RamUsedGb,
    double RamTotalGb,
    double DiskPercent,
    double DiskReadMbPerSec,
    double DiskWriteMbPerSec,
    double NetDownMbps,
    double NetUpMbps,
    double? GpuPercent,
    double? GpuMemMb,
    double? CpuTempC,
    double? GpuTempC);
