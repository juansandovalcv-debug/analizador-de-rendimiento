using PcAnalyzer.App.Native;

namespace PcAnalyzer.App;

public partial class MainForm
{
    #region Modo compacto (overlay)

    private void ToggleOverlay()
    {
        if (_overlay is { IsDisposed: false, Visible: true })
            CloseOverlay();
        else
            OpenOverlay();
    }

    private void OpenOverlay()
    {
        if (_overlay is null || _overlay.IsDisposed)
        {
            _overlay = new OverlayForm();
            _overlay.RestoreRequested += (_, _) => CloseOverlay();
        }

        _overlay.UpdateSample(_lastSample, _tempDetail);
        _overlay.Show();
        _overlay.PlaceAtCorner();

        btnOverlay.Text = "Volver al panel";
        lblStatus.Text = _hotKeyRegistered
            ? $"Modo compacto  ·  {_hotKeyLabel} para volver"
            : "Modo compacto  ·  doble clic en el overlay para volver";

        // Se minimiza en vez de ocultarse: así sigue en la barra de tareas y se puede
        // recuperar con Alt+Tab aunque el overlay se quede en segundo plano por un juego
        // a pantalla completa exclusiva.
        WindowState = FormWindowState.Minimized;
    }

    private void CloseOverlay()
    {
        if (_overlay is { IsDisposed: false })
            _overlay.Hide();

        btnOverlay.Text = "Modo compacto";

        if (_closing)
            return;

        if (WindowState == FormWindowState.Minimized)
            WindowState = FormWindowState.Normal;

        Show();
        Activate();

        lblStatus.Text = "Muestreando cada 1 segundo";
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        // Si el usuario restaura la ventana desde la barra de tareas, el overlay sobra.
        if (!_closing && WindowState != FormWindowState.Minimized && _overlay is { IsDisposed: false, Visible: true })
            CloseOverlay();
    }

    #endregion

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        if (_hotKeyRegistered)
            return;

        // Ctrl+Alt+M suele estar ocupado por otra utilidad; se prueba una lista y se
        // muestra la combinación que realmente quedó registrada.
        _hotKeyRegistered = HotKey.TryRegister(Handle, ToggleHotKeyId, out string label);
        _hotKeyLabel = label;

        btnOverlay.ToolTipText = _hotKeyRegistered
            ? $"Ventana flotante mínima para jugar ({label})"
            : "Ventana flotante mínima para jugar (sin atajo: combinaciones ocupadas)";
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == HotKey.WmHotKey && m.WParam.ToInt32() == ToggleHotKeyId)
        {
            ToggleOverlay();
            return;
        }

        base.WndProc(ref m);
    }
}
