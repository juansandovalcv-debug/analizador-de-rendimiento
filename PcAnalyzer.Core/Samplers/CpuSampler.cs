using PcAnalyzer.Core.Native;

namespace PcAnalyzer.Core.Samplers;

/// <summary>
/// Uso de CPU calculado por diferencia de tiempos del kernel.
/// El kernel incluye el tiempo idle, por eso: total = kernel + user, ocupado = total - idle.
///
/// La frecuencia real se calcula como nominal × "% Processor Performance" / 100, porque
/// el contador "Processor Frequency" devuelve siempre la frecuencia base (2400 MHz en un
/// i7-13620H aunque esté a 4 GHz por boost). Si PDH no está disponible, se cae a
/// CallNtPowerInformation.
/// </summary>
public sealed class CpuSampler : IDisposable
{
    private ulong _prevIdle;
    private ulong _prevKernel;
    private ulong _prevUser;
    private bool _hasPrevious;

    private IntPtr _query;
    private IntPtr _frequency;
    private IntPtr _performance;
    private readonly bool _pdhFrequency;

    public CpuSampler()
    {
        Capture();

        if (Pdh.TryOpen(out _query) &&
            Pdh.TryAdd(_query, @"\Processor Information(_Total)\Processor Frequency", out _frequency) &&
            Pdh.TryAdd(_query, @"\Processor Information(_Total)\% Processor Performance", out _performance))
        {
            Pdh.TryCollect(_query);
            _pdhFrequency = true;
        }
    }

    /// <summary>Devuelve el % de uso desde la llamada anterior (0-100).</summary>
    public double NextPercent()
    {
        if (!NativeMethods.GetSystemTimes(out var idle, out var kernel, out var user))
            return 0;

        ulong idleNow = idle.ToUInt64();
        ulong kernelNow = kernel.ToUInt64();
        ulong userNow = user.ToUInt64();

        if (!_hasPrevious)
        {
            _prevIdle = idleNow;
            _prevKernel = kernelNow;
            _prevUser = userNow;
            _hasPrevious = true;
            return 0;
        }

        ulong idleDelta = idleNow - _prevIdle;
        ulong totalDelta = (kernelNow - _prevKernel) + (userNow - _prevUser);
        ulong busyDelta = totalDelta > idleDelta ? totalDelta - idleDelta : 0;

        _prevIdle = idleNow;
        _prevKernel = kernelNow;
        _prevUser = userNow;

        return totalDelta == 0 ? 0 : Math.Clamp(busyDelta * 100.0 / totalDelta, 0, 100);
    }

    /// <summary>Frecuencia real del núcleo más rápido, en MHz (incluye el boost).</summary>
    public uint CurrentClockMhz
    {
        get
        {
            if (_pdhFrequency && Pdh.TryCollect(_query))
            {
                bool okFrequency = Pdh.TryRead(_frequency, out double nominal);
                bool okPerformance = Pdh.TryRead(_performance, out double performance);

                // "% Processor Performance" es un porcentaje sobre la frecuencia nominal:
                // 118 % sobre 2400 MHz = 2832 MHz reales.
                if (okFrequency && okPerformance && nominal > 0 && performance > 0)
                    return (uint)Math.Round(nominal * performance / 100.0);

                if (okFrequency && nominal > 0)
                    return (uint)Math.Round(nominal);
            }

            return NativeMethods.GetCurrentClockMhz();
        }
    }

    private void Capture()
    {
        if (!NativeMethods.GetSystemTimes(out var idle, out var kernel, out var user))
            return;

        _prevIdle = idle.ToUInt64();
        _prevKernel = kernel.ToUInt64();
        _prevUser = user.ToUInt64();
        _hasPrevious = true;
    }

    public void Dispose()
    {
        Pdh.Close(_query);
        _query = IntPtr.Zero;
    }
}
