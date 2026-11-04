namespace PcAnalyzer.App;

public sealed partial class OverlayForm
{
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
}
