using System.Net.NetworkInformation;

namespace PcAnalyzer.Core.Samplers;

/// <summary>
/// Tráfico de red por diferencia de bytes acumulados.
/// Usa System.Net.NetworkInformation, que no está localizado y no requiere administrador.
/// </summary>
public sealed class NetworkSampler
{
    private long _prevRx;
    private long _prevTx;
    private long _prevTicks;
    private bool _hasPrevious;

    public (double DownMbps, double UpMbps) Next()
    {
        long rx = 0;
        long tx = 0;

        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up)
                    continue;

                var type = nic.NetworkInterfaceType;
                if (type is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                    continue;

                try
                {
                    var stats = nic.GetIPStatistics();
                    rx += stats.BytesReceived;
                    tx += stats.BytesSent;
                }
                catch (NetworkInformationException)
                {
                    // La interfaz desapareció entre la enumeración y la lectura: se ignora.
                }
            }
        }
        catch (NetworkInformationException)
        {
        }

        long now = Environment.TickCount64;

        if (!_hasPrevious)
        {
            _prevRx = rx;
            _prevTx = tx;
            _prevTicks = now;
            _hasPrevious = true;
            return (0, 0);
        }

        double seconds = Math.Max((now - _prevTicks) / 1000.0, 0.001);
        double downMbps = (rx - _prevRx) * 8.0 / 1_000_000.0 / seconds;
        double upMbps = (tx - _prevTx) * 8.0 / 1_000_000.0 / seconds;

        _prevRx = rx;
        _prevTx = tx;
        _prevTicks = now;

        return (Math.Max(downMbps, 0), Math.Max(upMbps, 0));
    }
}
