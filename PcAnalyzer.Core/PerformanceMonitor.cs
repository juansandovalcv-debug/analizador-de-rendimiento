using System.Diagnostics;
using PcAnalyzer.Core.Models;
using PcAnalyzer.Core.Samplers;

namespace PcAnalyzer.Core;

/// <summary>
/// Orquestador: corre un bucle en un hilo de fondo, lee todos los samplers
/// y publica un <see cref="Sample"/> por intervalo.
///
/// El evento se dispara en un hilo de fondo. La UI es responsable de hacer
/// el marshalling (BeginInvoke). Así este proyecto no depende de WinForms.
/// </summary>
public sealed class PerformanceMonitor : IDisposable
{
    private readonly CpuSampler _cpu = new();
    private readonly MemorySampler _memory = new();
    private readonly DiskSampler _disk = new();
    private readonly NetworkSampler _network = new();
    private readonly GpuSampler _gpu = new();

    private TemperatureSampler? _temperature;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public event Action<Sample>? SampleReady;
    public event Action<string>? Faulted;

    public bool IsRunning => _loop is { IsCompleted: false };

    public bool TemperaturesAvailable => _temperature?.Available ?? false;
    public string? TemperatureError => _temperature?.UnavailableReason;

    public void Start(TimeSpan interval)
    {
        if (IsRunning)
            return;

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _loop = Task.Run(() => RunAsync(interval, token), token);
    }

    public async Task StopAsync()
    {
        if (_cts is null || _loop is null)
            return;

        await _cts.CancelAsync();

        try
        {
            await _loop;
        }
        catch (OperationCanceledException)
        {
        }

        _cts.Dispose();
        _cts = null;
        _loop = null;
    }

    private async Task RunAsync(TimeSpan interval, CancellationToken token)
    {
        try
        {
            // Las temperaturas necesitan driver + permisos: si falla, seguimos sin ellas.
            _temperature = new TemperatureSampler();

            // Primera lectura de calentamiento: los contadores por diferencia
            // necesitan dos muestras separadas en el tiempo para dar un valor real.
            _cpu.NextPercent();
            _network.Next();

            using var timer = new PeriodicTimer(interval);

            do
            {
                token.ThrowIfCancellationRequested();
                SampleReady?.Invoke(Read());
            }
            while (await timer.WaitForNextTickAsync(token));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Faulted?.Invoke($"Error en el muestreo: {ex.Message}");
        }
    }

    private Sample Read()
    {
        double cpuPercent = 0;
        uint clock = 0;
        (double Percent, double UsedGb, double TotalGb) memory = (0, 0, 0);
        (double Percent, double ReadMbPerSec, double WriteMbPerSec) disk = (0, 0, 0);
        (double DownMbps, double UpMbps) net = (0, 0);
        (double? Percent, double? DedicatedMb) gpu = (null, null);
        (double? CpuTemp, double? GpuTemp) temps = (null, null);

        // Cada sampler va aislado: si uno falla (WMI roto, driver ausente),
        // el resto del panel sigue funcionando.
        try { cpuPercent = _cpu.NextPercent(); } catch { }
        try { clock = _cpu.CurrentClockMhz; } catch { }
        try { memory = _memory.Next(); } catch { }
        try { disk = _disk.Next(); } catch { }
        try { net = _network.Next(); } catch { }
        try { gpu = _gpu.Next(); } catch { }
        try { temps = _temperature?.Next() ?? (null, null); } catch { }

        return new Sample(
            DateTime.Now,
            cpuPercent,
            clock,
            memory.Percent,
            memory.UsedGb,
            memory.TotalGb,
            disk.Percent,
            disk.ReadMbPerSec,
            disk.WriteMbPerSec,
            net.DownMbps,
            net.UpMbps,
            gpu.Percent,
            gpu.DedicatedMb,
            temps.CpuTemp,
            temps.GpuTemp);
    }

    public void Dispose()
    {
        try
        {
            _cts?.Cancel();
            _loop?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
        }

        _cts?.Dispose();
        _temperature?.Dispose();
        _cpu.Dispose();
        _disk.Dispose();
        _gpu.Dispose();
    }
}
