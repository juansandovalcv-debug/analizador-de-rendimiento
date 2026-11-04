using PcAnalyzer.Core.Models;

namespace PcAnalyzer.App;

public partial class MainForm
{
    /// <summary>Traduce una muestra a las seis tarjetas. Siempre en el hilo de la UI.</summary>
    private void Render(Sample sample)
    {
        if (IsDisposed)
            return;

        string cpuName = _hardware?.CpuName ?? "CPU";
        string gpuName = _hardware?.GpuName ?? "GPU";
        string diskName = _hardware?.DiskName ?? "Disco";
        string netName = _hardware?.NetworkName ?? "Red";

        _cpuCard.Push(
            sample.CpuPercent,
            $"{sample.CpuPercent:F0} %",
            sample.CpuClockMhz > 0
                ? $"{cpuName}  ·  {sample.CpuClockMhz / 1000.0:F2} GHz"
                : cpuName);

        _ramCard.Push(
            sample.RamPercent,
            $"{sample.RamPercent:F0} %",
            $"{sample.RamTotalGb:F0} GB  ·  {sample.RamUsedGb:F1} GB en uso");

        if (sample.GpuPercent is double gpu)
        {
            _gpuCard.Push(
                gpu,
                $"{gpu:F0} %",
                sample.GpuMemMb is double vram
                    ? $"{gpuName}  ·  {vram / 1024.0:F1} GB VRAM"
                    : gpuName);
        }
        else
        {
            _gpuCard.SetUnavailable($"{gpuName}  ·  contadores no disponibles");
        }

        _diskCard.Push(
            sample.DiskPercent,
            $"{sample.DiskPercent:F0} %",
            $"{diskName}  ·  lectura {sample.DiskReadMbPerSec:F1} · escritura {sample.DiskWriteMbPerSec:F1} MB/s");

        _netCard.Push(
            sample.NetDownMbps,
            $"{sample.NetDownMbps:F1} Mbps",
            $"{netName}  ·  ↓ {sample.NetDownMbps:F1}  ·  ↑ {sample.NetUpMbps:F1} Mbps");

        // La temperatura de CPU necesita leer los MSR del procesador, y eso exige un
        // driver de kernel. La de GPU se lee por NVML/ADL y funciona sin permisos.
        string tempDetail = (sample.CpuTempC, sample.GpuTempC) switch
        {
            (double cpuTemp, double gpuTemp) => $"CPU {cpuTemp:F0} °C  ·  GPU {gpuTemp:F0} °C",
            (double cpuTemp, null) => $"CPU {cpuTemp:F0} °C",
            (null, double gpuTemp) => $"GPU {gpuTemp:F0} °C  ·  CPU {CpuTempBlocker()}",
            _ => $"sin sensores  ·  CPU {CpuTempBlocker()}"
        };

        double? mainTemp = sample.CpuTempC ?? sample.GpuTempC;

        if (mainTemp is double temperature)
            _tempCard.Push(temperature, $"{temperature:F0} °C", tempDetail);
        else
            _tempCard.SetUnavailable(tempDetail);

        var uptime = DateTime.Now - _startedAt;
        lblUptime.Text = $"activo {uptime:hh\\:mm\\:ss}  ·  {sample.Timestamp:HH:mm:ss}";

        _lastSample = sample;
        _tempDetail = tempDetail;

        if (_overlay is { IsDisposed: false })
            _overlay.UpdateSample(sample, tempDetail);
    }

    /// <summary>
    /// Explica por qué no hay temperatura de CPU. El orden importa: con HVCI activo,
    /// ejecutar como administrador NO sirve de nada, así que ese es el mensaje prioritario.
    /// </summary>
    private string CpuTempBlocker()
    {
        if (_memoryIntegrity == true)
            return "bloqueada por Integridad de memoria";

        return _isElevated ? "sin sensor en este equipo" : "requiere admin";
    }
}
