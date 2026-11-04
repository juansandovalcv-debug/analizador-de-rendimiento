using PcAnalyzer.App.Controls;
using PcAnalyzer.Core;
using PcAnalyzer.Core.Models;

namespace PcAnalyzer.App;

/// <summary>
/// Ventana principal. Está partida en varios archivos parciales por responsabilidad:
/// este (estado y arranque), Render (pintado de tarjetas), Overlay (modo compacto)
/// y Lifecycle (cierre, elevación y marshalling al hilo de la UI).
/// </summary>
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

    private OverlayForm? _overlay;
    private Sample? _lastSample;
    private string _tempDetail = string.Empty;
    private bool _closing;
    private bool _hotKeyRegistered;
    private string _hotKeyLabel = string.Empty;

    private const int ToggleHotKeyId = 0x4D43;

    public MainForm()
    {
        InitializeComponent();
        BuildCards();

        _monitor.SampleReady += OnSampleReady;
        _monitor.Faulted += message => PostToUi(() => lblStatus.Text = message);

        btnElevate.Visible = !_isElevated;
        btnElevate.Click += OnElevateClick;
        btnOverlay.Click += (_, _) => ToggleOverlay();
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
}
