using System.Drawing.Drawing2D;
using PcAnalyzer.Core.Models;

namespace PcAnalyzer.App;

/// <summary>
/// Ventana flotante mínima para vigilar el equipo mientras se juega.
///
/// Es un formulario sin borde, siempre visible y semitransparente: cuatro barras
/// (CPU, RAM, GPU, temperatura) y nada más. No se activa al mostrarse ni al pulsarla,
/// así que no le roba el foco al juego, y se puede arrastrar a cualquier esquina.
///
/// Limitación conocida: en juegos a pantalla completa EXCLUSIVA (Direct3D en modo
/// exclusivo) Windows no dibuja ventanas encima. Casi todos los juegos actuales usan
/// "pantalla completa sin bordes", donde esto sí funciona.
/// </summary>
public sealed class OverlayForm : Form
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

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var back = new SolidBrush(Background))
            g.FillRectangle(back, ClientRectangle);

        int padX = S(12);
        int top = S(9);
        int rowH = S(21);
        int labelW = S(42);
        int valueW = S(74);
        int barX = padX + labelW;
        int barW = Math.Max(S(40), ClientSize.Width - padX * 2 - labelW - valueW);

        void Row(int index, string label, double? percent, Color accent, string value)
        {
            int y = top + index * rowH;

            TextRenderer.DrawText(
                g,
                label,
                _labelFont,
                new Rectangle(padX, y, labelW, rowH),
                LabelColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            var track = new Rectangle(barX, y + rowH / 2 - S(2), barW, S(4));

            using (var brush = new SolidBrush(TrackColor))
                FillRounded(g, brush, track, S(2));

            if (percent is double value0To100)
            {
                int fill = (int)Math.Round(barW * Math.Clamp(value0To100 / 100.0, 0, 1));

                if (fill > 0)
                {
                    var bar = new Rectangle(barX, track.Y, Math.Max(fill, S(4)), track.Height);

                    using var brush = new SolidBrush(accent);
                    FillRounded(g, brush, bar, S(2));
                }
            }

            TextRenderer.DrawText(
                g,
                value,
                _valueFont,
                new Rectangle(ClientSize.Width - padX - valueW, y, valueW, rowH),
                ValueColor,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        Sample? sample = _sample;

        Row(0, "CPU", sample?.CpuPercent, CpuColor, sample?.CpuPercent is double cpu ? $"{cpu:F0} %" : "--");
        Row(1, "RAM", sample?.RamPercent, RamColor, sample?.RamPercent is double ram ? $"{ram:F0} %" : "--");

        Row(
            2,
            "GPU",
            sample?.GpuPercent,
            GpuColor,
            sample?.GpuPercent is double gpu ? $"{gpu:F0} %" : "--");

        // La temperatura no es un porcentaje: se mapea 30-100 °C sobre la barra y el
        // color avisa solo (verde → ámbar → rojo).
        double? temperature = sample?.CpuTempC ?? sample?.GpuTempC;

        Row(
            3,
            "TEMP",
            temperature is double t ? (t - 30) / 70 * 100 : null,
            temperature switch
            {
                < 60 => CoolColor,
                < 80 => WarmColor,
                null => CoolColor,
                _ => HotColor
            },
            temperature is double temp ? $"{temp:F0} °C" : "--");

        using (var path = RoundedPath(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), S(10)))
        using (var pen = new Pen(BorderColor))
            g.DrawPath(pen, path);
    }

    private static void FillRounded(Graphics g, Brush brush, Rectangle rect, int radius)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
            return;

        if (radius < 1)
        {
            g.FillRectangle(brush, rect);
            return;
        }

        using var path = RoundedPath(rect, radius);
        g.FillPath(brush, path);
    }

    private static GraphicsPath RoundedPath(RectangleF rect, int radius)
    {
        int r = Math.Min(radius, (int)Math.Min(rect.Width, rect.Height) / 2);
        var path = new GraphicsPath();

        if (r < 1)
        {
            path.AddRectangle(rect);
            return path;
        }

        float d = r * 2f;

        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();

        return path;
    }

    private void UpdateRegion()
    {
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
            return;

        using var path = RoundedPath(new RectangleF(0, 0, ClientSize.Width, ClientSize.Height), S(10));
        var previous = Region;

        Region = new Region(path);
        previous?.Dispose();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateRegion();
    }

    #region Arrastrar y ajustar a la esquina

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button != MouseButtons.Left)
            return;

        _dragging = true;
        _moved = false;
        _grabOffset = e.Location;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!_dragging)
            return;

        Point cursor = PointToScreen(e.Location);

        Location = new Point(cursor.X - _grabOffset.X, cursor.Y - _grabOffset.Y);
        _moved = true;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (!_dragging)
            return;

        _dragging = false;

        if (_moved)
            SnapToNearestCorner();
    }

    protected override void OnDoubleClick(EventArgs e)
    {
        base.OnDoubleClick(e);

        if (_moved)
            return;

        RestoreRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Al soltar, se pega a la esquina más cercana para que nunca quede torcido.</summary>
    private void SnapToNearestCorner()
    {
        Rectangle area = Screen.FromControl(this).WorkingArea;
        int margin = S(16);

        int left = area.Left + margin;
        int right = area.Right - Width - margin;
        int top = area.Top + margin;
        int bottom = area.Bottom - Height - margin;

        int x = Math.Abs(Left - left) <= Math.Abs(Left - right) ? left : right;
        int y = Math.Abs(Top - top) <= Math.Abs(Top - bottom) ? top : bottom;

        Location = new Point(x, y);
    }

    #endregion

    #region Menú contextual

    private void BuildMenu()
    {
        _menu.Items.Add("Abrir panel completo", null, (_, _) => RestoreRequested?.Invoke(this, EventArgs.Empty));
        _menu.Items.Add(new ToolStripSeparator());

        var opacity = new ToolStripMenuItem("Opacidad");

        foreach (var (label, value) in new (string Label, float Value)[]
                 {
                     ("95 %", 0.95f),
                     ("88 %", 0.88f),
                     ("70 %", 0.70f),
                     ("55 %", 0.55f)
                 })
        {
            var item = new ToolStripMenuItem(label);

            item.Click += (_, _) => SetOpacity(value);

            _opacityItems.Add((item, value));
            opacity.DropDownItems.Add(item);
        }

        _menu.Items.Add(opacity);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Cerrar overlay", null, (_, _) => Close());

        _menu.Opening += (_, _) =>
        {
            foreach (var (item, value) in _opacityItems)
                item.Checked = Math.Abs(Opacity - value) < 0.01;
        };

        ContextMenuStrip = _menu;
    }

    private void SetOpacity(float value)
    {
        Opacity = value;

        foreach (var (item, candidate) in _opacityItems)
            item.Checked = Math.Abs(value - candidate) < 0.01;
    }

    #endregion

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
