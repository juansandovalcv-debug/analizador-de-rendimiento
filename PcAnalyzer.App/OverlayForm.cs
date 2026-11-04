using PcAnalyzer.Core.Models;

namespace PcAnalyzer.App;

/// <summary>
/// Ventana flotante mínima para vigilar el equipo mientras se juega.
///
/// Es un formulario sin borde, siempre visible y semitransparente: cuatro barras
/// (CPU, RAM, GPU, temperatura) y nada más. No se activa al mostrarse ni al pulsarla,
/// así que no le roba el foco al juego, y se puede arrastrar a cualquier esquina.
///
/// Partida en varios archivos parciales: este (estado y ciclo de vida), Paint
/// (dibujo), Input (arrastrar y ajustar a la esquina) y Menu (clic derecho).
///
/// Limitación conocida: en juegos a pantalla completa EXCLUSIVA (Direct3D en modo
/// exclusivo) Windows no dibuja ventanas encima. Casi todos los juegos actuales usan
/// "pantalla completa sin bordes", donde esto sí funciona.
/// </summary>
public sealed partial class OverlayForm : Form
{
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;

    private static readonly Color Background = Color.FromArgb(14, 16, 20);
    private static readonly Color TrackColor = Color.FromArgb(42, 255, 255, 255);
    private static readonly Color BorderColor = Color.FromArgb(36, 255, 255, 255);
    private static readonly Color LabelColor = Color.FromArgb(148, 156, 170);
    private static readonly Color ValueColor = Color.FromArgb(240, 243, 248);

    private static readonly Color CpuColor = Color.FromArgb(76, 141, 255);
    private static readonly Color RamColor = Color.FromArgb(124, 92, 255);
    private static readonly Color GpuColor = Color.FromArgb(46, 204, 113);
    private static readonly Color CoolColor = Color.FromArgb(46, 204, 113);
    private static readonly Color WarmColor = Color.FromArgb(255, 176, 32);
    private static readonly Color HotColor = Color.FromArgb(255, 92, 92);

    private const string DefaultHint =
        "Doble clic: panel completo   ·   Arrastra para mover   ·   Clic derecho: opciones";

    private readonly Font _labelFont = new("Segoe UI Semibold", 8f);
    private readonly Font _valueFont = new("Segoe UI Semibold", 9f);
    private readonly ToolTip _hint = new() { InitialDelay = 700, ReshowDelay = 200 };
    private readonly ContextMenuStrip _menu = new() { ShowImageMargin = false };
    private readonly List<(ToolStripMenuItem Item, float Value)> _opacityItems = new();

    private Sample? _sample;
    private bool _dragging;
    private bool _moved;
    private Point _grabOffset;

    public event EventHandler? RestoreRequested;

    public OverlayForm()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer,
            true);

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Background;
        Opacity = 0.88;
        DoubleBuffered = true;
        Text = "Analizador de rendimiento";

        BuildMenu();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;

            // WS_EX_NOACTIVATE: pulsar el overlay no activa la ventana ni saca al juego
            // del primer plano. WS_EX_TOOLWINDOW: fuera del Alt+Tab y de la barra de tareas.
            parameters.ExStyle |= WsExNoActivate | WsExToolWindow;

            return parameters;
        }
    }

    private float Dpi => DeviceDpi / 96f;

    private int S(int value) => (int)Math.Round(value * Dpi);

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        ClientSize = new Size(S(280), S(104));
        UpdateRegion();
        PlaceAtCorner();
    }

    /// <summary>Coloca el overlay en la esquina inferior derecha de la pantalla indicada.</summary>
    public void PlaceAtCorner(Screen? screen = null)
    {
        screen ??= Screen.FromPoint(Cursor.Position);

        Rectangle area = screen.WorkingArea;
        int margin = S(16);

        Location = new Point(
            area.Right - Width - margin,
            area.Bottom - Height - margin);
    }

    /// <summary>Empuja una muestra nueva. Se llama desde el hilo de la UI.</summary>
    public void UpdateSample(Sample? sample, string hint = "")
    {
        _sample = sample;

        _hint.SetToolTip(this, hint.Length > 0 ? hint : DefaultHint);

        if (!IsDisposed)
            Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hint.Dispose();
            _menu.Dispose();
            _labelFont.Dispose();
            _valueFont.Dispose();
        }

        base.Dispose(disposing);
    }
}
