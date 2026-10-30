using System.Diagnostics;
using System.Security.Principal;
using PcAnalyzer.App.Controls;
using PcAnalyzer.Core;
using PcAnalyzer.Core.Models;

namespace PcAnalyzer.App;

public partial class MainForm : Form
{
    private static readonly Color CpuAccent = Color.FromArgb(76, 141, 255);
    private static readonly Color RamAccent = Color.FromArgb(124, 92, 255);
    private static readonly Color GpuAccent = Color.FromArgb(46, 204, 113);
    private static readonly Color DiskAccent = Color.FromArgb(255, 176, 32);
    private static readonly Color NetAccent = Color.FromArgb(0, 200, 190);
    private static readonly Color TempAccent = Color.FromArgb(255, 92, 92);

    private readonly PerformanceMonitor _monitor = new();
    private readonly DateTime _startedAt = DateTime.Now;
    private readonly bool _isElevated = IsElevated();

    private MetricCard _cpuCard = null!;
    private MetricCard _ramCard = null!;
    private MetricCard _gpuCard = null!;
    private MetricCard _diskCard = null!;
    private MetricCard _netCard = null!;
    private MetricCard _tempCard = null!;

    private HardwareInfo? _hardware;
    private bool? _memoryIntegrity;

    public MainForm()
    {
        InitializeComponent();
        BuildCards();

        _monitor.SampleReady += OnSampleReady;
        _monitor.Faulted += message => PostToUi(() => lblStatus.Text = message);

        btnElevate.Visible = !_isElevated;
        btnElevate.Click += OnElevateClick;
    }

    private void BuildCards()
    {
        _cpuCard = new MetricCard("CPU", 100, CpuAccent);
        _ramCard = new MetricCard("Memoria RAM", 100, RamAccent);
        _gpuCard = new MetricCard("GPU", 100, GpuAccent);
        _diskCard = new MetricCard("Disco", 100, DiskAccent);
        _netCard = new MetricCard("Red", 100, NetAccent, autoScale: true);
        _tempCard = new MetricCard("Temperatura", 110, TempAccent);

        var cards = new[] { _cpuCard, _ramCard, _gpuCard, _diskCard, _netCard, _tempCard };

        foreach (var card in cards)
        {
            card.Dock = DockStyle.Fill;
            gridCards.Controls.Add(card);
        }
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        try
        {
            lblStatus.Text = "Detectando hardware (WMI)...";

            // WMI tarda cientos de milisegundos: nunca en el hilo de la UI.
            _hardware = await Task.Run(() =>
            {
                var info = HardwareInfoProvider.Get();
                _memoryIntegrity = MemoryIntegrity.IsEnabled();
                return info;
            });

            lblHardware.Text = _hardware.Summary;
            lblStatus.Text = "Muestreando cada 1 segundo";
        }
        catch (Exception ex)
        {
            lblHardware.Text = "No se pudo leer el hardware.";
            lblStatus.Text = $"Aviso: {ex.Message}";
        }

        _monitor.Start(TimeSpan.FromSeconds(1));
    }

    private void OnSampleReady(Sample sample)
    {
        // El evento llega desde un hilo de fondo: hay que volver al hilo de la UI.
        PostToUi(() => Render(sample));
    }

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
        double? mainTemp = sample.CpuTempC ?? sample.GpuTempC;

        if (mainTemp is double temperature)
        {
            string detail = (sample.CpuTempC, sample.GpuTempC) switch
            {
                (double cpuTemp, double gpuTemp) => $"CPU {cpuTemp:F0} °C  ·  GPU {gpuTemp:F0} °C",
                (double cpuTemp, null) => $"CPU {cpuTemp:F0} °C",
                (null, double gpuTemp) => $"GPU {gpuTemp:F0} °C  ·  CPU {CpuTempBlocker()}",
                _ => string.Empty
            };

            _tempCard.Push(temperature, $"{temperature:F0} °C", detail);
        }
        else
        {
            _tempCard.SetUnavailable($"sin sensores  ·  CPU {CpuTempBlocker()}");
        }

        var uptime = DateTime.Now - _startedAt;
        lblUptime.Text = $"activo {uptime:hh\\:mm\\:ss}  ·  {sample.Timestamp:HH:mm:ss}";
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

    private void PostToUi(Action action)
    {
        if (!IsHandleCreated || IsDisposed)
            return;

        try
        {
            BeginInvoke(action);
        }
        catch (InvalidOperationException)
        {
            // La ventana se cerró entre la comprobación y el Invoke.
        }
    }

    private void OnElevateClick(object? sender, EventArgs e)
    {
        try
        {
            var startInfo = new ProcessStartInfo(Application.ExecutablePath)
            {
                UseShellExecute = true,
                Verb = "runas"
            };

            Process.Start(startInfo);
            Close();
        }
        catch (Exception)
        {
            // El usuario canceló el diálogo de UAC.
            lblStatus.Text = "Elevación cancelada.";
        }
    }

    private static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        _monitor.SampleReady -= OnSampleReady;

        try
        {
            await _monitor.StopAsync();
        }
        catch
        {
        }

        _monitor.Dispose();
        base.OnFormClosing(e);
    }
}
