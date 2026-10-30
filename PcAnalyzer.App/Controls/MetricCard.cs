using ScottPlot.WinForms;

namespace PcAnalyzer.App.Controls;

/// <summary>
/// Tarjeta reutilizable: título, valor grande, detalle y una sparkline del histórico.
/// Cada tarjeta guarda su propio buffer circular, así que no hay estado compartido
/// entre métricas.
/// </summary>
public sealed class MetricCard : UserControl
{
    private readonly Label _title = new();
    private readonly Label _value = new();
    private readonly Label _detail = new();
    private readonly Panel _header = new();
    private readonly FormsPlot _plot = new();
    private readonly List<double> _history = new();

    private readonly int _capacity;
    private readonly double _fixedMax;
    private readonly bool _autoScale;
    private readonly ScottPlot.Color _accent;

    private static readonly Color CardBack = Color.FromArgb(27, 30, 36);

    /// <param name="title">Título de la tarjeta.</param>
    /// <param name="maxValue">Tope del eje Y cuando autoScale es false.</param>
    /// <param name="accent">Color del valor y de la línea.</param>
    /// <param name="autoScale">Si es true, el eje Y se adapta al pico (útil para red).</param>
    /// <param name="capacity">Puntos de histórico que se conservan.</param>
    public MetricCard(string title, double maxValue, Color accent, bool autoScale = false, int capacity = 120)
    {
        _capacity = capacity;
        _fixedMax = maxValue;
        _autoScale = autoScale;
        _accent = ScottPlot.Color.FromHex($"#{accent.R:X2}{accent.G:X2}{accent.B:X2}");

        BackColor = CardBack;
        Padding = new Padding(14, 10, 14, 12);
        Margin = new Padding(6);
        DoubleBuffered = true;

        BuildHeader(title, accent);
        BuildPlot();

        Controls.Add(_plot);
        Controls.Add(_header);

        _plot.Dock = DockStyle.Fill;
        _header.Dock = DockStyle.Top;
    }

    private void BuildHeader(string title, Color accent)
    {
        _header.Height = 96;
        _header.BackColor = CardBack;

        _title.Text = title.ToUpperInvariant();
        _title.Font = new Font("Segoe UI Semibold", 9f);
        _title.ForeColor = Color.FromArgb(138, 146, 160);
        _title.AutoSize = true;
        _title.Location = new Point(2, 0);

        _value.Text = "--";
        _value.Font = new Font("Segoe UI", 24f, FontStyle.Bold);
        _value.ForeColor = accent;
        _value.AutoSize = true;
        _value.Location = new Point(0, 16);

        // El detalle va DEBAJO del valor, no flotando arriba a la derecha: así se lee
        // como parte del dato y no como un elemento suelto del diseño.
        _detail.Text = string.Empty;
        _detail.Font = new Font("Segoe UI", 9f);
        _detail.ForeColor = Color.FromArgb(122, 130, 144);
        _detail.Dock = DockStyle.Bottom;
        _detail.Height = 22;
        _detail.TextAlign = ContentAlignment.MiddleLeft;
        _detail.AutoEllipsis = true;
        _detail.Padding = new Padding(2, 0, 0, 0);

        _header.Controls.Add(_detail);
        _header.Controls.Add(_title);
        _header.Controls.Add(_value);
    }

    private void BuildPlot()
    {
        _plot.BackColor = CardBack;

        var plot = _plot.Plot;
        plot.FigureBackground.Color = ScottPlot.Color.FromHex("#1B1E24");
        plot.DataBackground.Color = ScottPlot.Color.FromHex("#1B1E24");

        plot.Axes.Bottom.IsVisible = false;
        plot.Axes.Top.IsVisible = false;
        plot.Axes.Left.IsVisible = false;
        plot.Axes.Right.IsVisible = false;

        plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#242933");
        plot.Grid.MinorLineColor = ScottPlot.Color.FromHex("#20242C");

        plot.Axes.SetLimits(0, _capacity, 0, _fixedMax);

        _plot.Refresh();
    }

    /// <summary>Empuja una nueva muestra al histórico y repinta la tarjeta.</summary>
    public void Push(double value, string valueText, string detail)
    {
        _history.Add(Math.Clamp(value, 0, 1_000_000));

        while (_history.Count > _capacity)
            _history.RemoveAt(0);

        _value.Text = valueText;
        _detail.Text = detail;

        double top = _autoScale
            ? Math.Max(10, Math.Ceiling(_history.Max() * 1.3))
            : _fixedMax;

        try
        {
            var plot = _plot.Plot;
            plot.Clear();

            var signal = plot.Add.Signal(_history.ToArray());
            signal.Color = _accent;
            signal.LineWidth = 1.8f;

            plot.Axes.SetLimits(0, _capacity, 0, top);
            _plot.Refresh();
        }
        catch
        {
            // Si el control aún no tiene un tamaño válido, se reintenta en el siguiente tick.
        }
    }

    /// <summary>Texto sin valor numérico (por ejemplo cuando el sensor no está disponible).</summary>
    public void SetUnavailable(string detail)
    {
        _value.Text = "n/d";
        _detail.Text = detail;
    }
}
