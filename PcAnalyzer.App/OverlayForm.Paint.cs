using System.Drawing.Drawing2D;
using PcAnalyzer.Core.Models;

namespace PcAnalyzer.App;

public sealed partial class OverlayForm
{
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
}
