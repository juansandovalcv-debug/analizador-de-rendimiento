namespace PcAnalyzer.App;

public sealed partial class OverlayForm
{
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
}
