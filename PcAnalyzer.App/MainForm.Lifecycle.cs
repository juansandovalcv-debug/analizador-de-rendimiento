using System.Diagnostics;
using System.Security.Principal;
using PcAnalyzer.App.Native;

namespace PcAnalyzer.App;

public partial class MainForm
{
    /// <summary>
    /// Vuelve al hilo de la UI desde el hilo de muestreo. BeginInvoke no bloquea, así
    /// que el bucle de 1 segundo nunca se queda esperando a que se pinte.
    /// </summary>
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
        _closing = true;

        if (_hotKeyRegistered)
        {
            HotKey.Unregister(Handle, ToggleHotKeyId);
            _hotKeyRegistered = false;
        }

        if (_overlay is { IsDisposed: false })
        {
            _overlay.Close();
            _overlay = null;
        }

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
