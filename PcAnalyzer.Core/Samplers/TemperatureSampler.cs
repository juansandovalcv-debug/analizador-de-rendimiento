using LibreHardwareMonitor.Hardware;

namespace PcAnalyzer.Core.Samplers;

/// <summary>
/// Temperaturas reales (CPU / GPU) mediante LibreHardwareMonitor.
///
/// IMPORTANTE: leer sensores requiere cargar un driver en modo kernel, así que
/// esta clase SOLO funciona si la app se ejecuta como administrador. Si no hay
/// permisos, se marca Available = false y la interfaz muestra "n/d" en lugar de
/// romperse. Todo va envuelto en try/catch a propósito: en máquinas virtuales o
/// con antivirus agresivos el driver puede no cargar.
/// </summary>
public sealed class TemperatureSampler : IDisposable
{
    private readonly UpdateVisitor _visitor = new();
    private Computer? _computer;

    public bool Available { get; private set; }
    public string? UnavailableReason { get; private set; }

    public TemperatureSampler()
    {
        try
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMotherboardEnabled = true,
                IsStorageEnabled = false,
                IsControllerEnabled = false,
                IsMemoryEnabled = false,
                IsNetworkEnabled = false
            };

            _computer.Open();
            _computer.Accept(_visitor);

            Available = true;
        }
        catch (Exception ex)
        {
            Available = false;
            UnavailableReason = ex.Message;
            _computer = null;
        }
    }

    public (double? CpuTemp, double? GpuTemp) Next()
    {
        if (_computer is null)
            return (null, null);

        try
        {
            _computer.Accept(_visitor);

            double? cpu = null;
            double? gpu = null;

            foreach (var hardware in _computer.Hardware)
            {
                switch (hardware.HardwareType)
                {
                    case HardwareType.Cpu:
                        cpu = Higher(cpu, Temperature(hardware, "CPU Package", "Core Max"));
                        break;

                    case HardwareType.GpuNvidia:
                    case HardwareType.GpuAmd:
                    case HardwareType.GpuIntel:
                        // Un portátil con gráfica híbrida expone DOS GPUs: la dedicada y la
                        // integrada. La integrada suele no tener sensores de temperatura, así
                        // que hay que quedarse con el mejor valor, no con el último leído.
                        gpu = Higher(gpu, Temperature(hardware, "GPU Core"));
                        break;
                }
            }

            return (cpu, gpu);
        }
        catch
        {
            return (null, null);
        }
    }

    /// <summary>
    /// Devuelve la temperatura del primer sensor preferido que exista y, si no hay
    /// ninguno con ese nombre, la más alta de todas. Se prefiere un nombre concreto
    /// porque "la más alta" en una GPU es el Hot Spot, que no es lo que el usuario
    /// compara con MSI Afterburner.
    /// </summary>
    private static double? Temperature(IHardware hardware, params string[] preferredNames)
    {
        var byName = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        double? hottest = null;

        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.SensorType != SensorType.Temperature || sensor.Value is not float value)
                continue;

            byName[sensor.Name] = value;

            if (hottest is null || value > hottest)
                hottest = value;
        }

        foreach (var name in preferredNames)
        {
            if (byName.TryGetValue(name, out double preferred))
                return preferred;
        }

        return hottest;
    }

    /// <summary>Devuelve el mayor de dos valores opcionales, ignorando los nulos.</summary>
    private static double? Higher(double? current, double? candidate)
    {
        if (candidate is null)
            return current;

        return current is null || candidate > current ? candidate : current;
    }

    public void Dispose()
    {
        try
        {
            _computer?.Close();
        }
        catch
        {
            // Si el driver ya se descargó, Close() puede fallar. No es un problema.
        }

        _computer = null;
    }

    /// <summary>Recorre el árbol de hardware y refresca todos los sensores.</summary>
    private sealed class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer) => computer.Traverse(this);

        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();

            foreach (var sub in hardware.SubHardware)
                sub.Accept(this);
        }

        public void VisitSensor(ISensor sensor) { }

        public void VisitParameter(IParameter parameter) { }
    }
}
