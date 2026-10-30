# CONTEXT — Analizador de rendimiento (handoff)

Monitor de rendimiento de PC en tiempo real. Windows, **C# / .NET 10 / WinForms**.
Carpeta: `C:\Users\juans\Desktop\analizador de rendimiento`. NO es repo git.

## Estado

Compila 0 errores / 0 warnings. Funciona y verificado en la máquina del usuario.
Panel: CPU, RAM, GPU, disco, red, temperatura. 1 muestra/s, histórico 120 puntos (2 min).

## Proyectos

- `PcAnalyzer.slnx` — solución (formato .slnx; VS 2026 o 2022 17.10+).
- `PcAnalyzer.Core` — `net10.0-windows`. Lógica. **Nunca referencia WinForms** (regla dura).
- `PcAnalyzer.App` — `net10.0-windows10.0.19041.0`, WinForms, `PlatformTarget=x64`, `RuntimeIdentifier=win-x64`, `app.manifest` asInvoker.

## Decisiones que NO se deben deshacer

1. **TFM con `10.0.19041.0` obligatorio.** ScottPlot→SkiaSharp solo publica `net8.0-windows10.0.19041`. Con `net10.0-windows` (=windows7.0) NuGet cae a binarios **net462** → compila pero revienta al arrancar.
2. **PDH, no `PerformanceCounter`.** Los nombres de `PerformanceCounter` están traducidos (es `"Procesador"`, no `"Processor"`). `PdhAddEnglishCounter` acepta inglés siempre. Wrapper: `Core/Native/Pdh.cs`.
3. **WMI solo para datos estáticos** (1 vez al arrancar). WMI en el bucle cuesta 300-700 ms/muestra; PDH cuesta ~5 ms total.
4. **x64 obligatorio**: LibreHardwareMonitor carga nativos.
5. **asInvoker** + botón "Ejecutar como administrador" en el status bar (relanza con `runas` y cierra).

## Fuentes de datos (`Core/Samplers/`)

| Métrica | Fuente | Coste |
|---|---|---|
| CPU % | `GetSystemTimes` delta; `total=kernel+user`, `busy=total-idle` | ~0 ms |
| CPU MHz | PDH `\Processor Information(_Total)\Processor Frequency` × `% Processor Performance` / 100 | ~0 ms |
| RAM | `GlobalMemoryStatusEx` | ~0 ms |
| Disco | PDH `\PhysicalDisk(_Total)\% Disk Time`, `Disk Read Bytes/sec`, `Disk Write Bytes/sec` (fallback WMI) | ~0 ms |
| Red | `NetworkInterface.GetIPStatistics` delta bytes → Mbps | ~1 ms |
| GPU % | PDH `\GPU Engine(*engtype_3D)\Utilization Percentage`, sumar, clamp 100 (fallback WMI) | ~1 ms |
| VRAM | PDH `\GPU Adapter Memory(*)\Dedicated Usage` | ~0 ms |
| Temp GPU | LibreHardwareMonitor (NVML/ADL) — **funciona sin admin** | ~5 ms |
| Temp CPU | LibreHardwareMonitor (MSR) — **requiere driver kernel** | — |
| Estático | WMI: CPU, RAM total, GPU, modelo de disco, adaptador de red, placa, OS | 200-500 ms 1 vez |

Notas: `Processor Frequency` solo devuelve la base (2400 MHz fijos); el boost se obtiene multiplicando por `% Processor Performance` (118-165 %). `% Disk Time` puede pasar de 100 → clamp.

## Temperatura de CPU: BLOQUEADA por HVCI

Máquina del usuario tiene **Integridad de memoria (HVCI) activa** → Windows rechaza el driver `WinRing0` de LHM **aunque se ejecute como admin**. Verificado: los 55 sensores de CPU dan `null`; los de GPU dan valor.

- Plan B descartado: zona térmica ACPI (`Win32_PerfFormattedData_Counters_ThermalZoneInformation`) clavada en 27.9 °C con 16 hilos al 100 %. No mide la CPU.
- Única solución real: desactivar HVCI (Seguridad de Windows → Aislamiento del núcleo → Integridad de memoria: Off + reinicio).
- La app lo detecta: `Core/MemoryIntegrity.cs` → WMI `root\Microsoft\Windows\DeviceGuard`, `SecurityServicesRunning` contiene `2`. Se cachea.
- UI: si falta temp CPU pero hay GPU → muestra la de GPU y el motivo (`MainForm.CpuTempBlocker()`).

## UI (`App/Controls/MetricCard.cs`)

Tarjeta = título pequeño gris (arriba) → valor grande con color acento → **detalle debajo del valor** (no arriba a la derecha: el usuario lo reportó como mal separado) → sparkline ScottPlot.
Grid 2×3 en `MainForm.Designer.cs`. Detalle = nombre del componente + cifras. Colores por tarjeta en `MainForm.cs`.

Render: `PerformanceMonitor` dispara evento en hilo de fondo → `BeginInvoke` (`MainForm.PostToUi`). Cada tarjeta guarda su propio `List<double>`; cada tick hace `Plot.Clear()` + `Add.Signal` (API segura, no mutar arrays).

## Bugs ya corregidos (no reintroducir)

1. **GPU híbrida**: recorrer GPUs con `gpu = Max(hw)` hacía que la iGPU Intel (sin sensores) sobrescribiera con `null` la lectura de la RTX. Ahora `Higher(current, candidate)` ignora nulos.
2. **Temp GPU**: se mostraba el máximo (= Hot Spot). Ahora prefiere el sensor `"GPU Core"` (y CPU `"CPU Package"`/`"Core Max"`).
3. **Nombre de GPU**: `Win32_VideoController` devolvía la Intel UHD. Ahora prefiere NVIDIA/Radeon/AMD/Arc.
4. **Nombre de red**: salía "Radmin VPN" (los VPN se anuncian como Ethernet). Ahora filtra keywords virtuales y elige la interfaz más rápida.
5. **`dotnet run` no reinicia nada**; los cierres con `CloseReason=UserClosing` durante pruebas eran el usuario cerrando ventanas, no un bug.

## Compilar / publicar

```powershell
dotnet run --project PcAnalyzer.App
dotnet publish PcAnalyzer.App -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish\exe-ligero      # 34 MB + DLLs, requiere .NET 10
dotnet publish PcAnalyzer.App -c Release -r win-x64 --self-contained true  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish\exe-portable   # 159 MB, un .exe
```

`IncludeNativeLibrariesForSelfExtract` **no es opcional**: SkiaSharp y el driver de LHM son nativos; sin él el .exe arranca y falla al dibujar.

## Entorno del usuario

ASUS FX507VV, i7-13620H (10C/16T), RTX 4060 Laptop + Intel UHD (híbrida), 16 GB, 1920×1080 @125 % (PerMonitorV2).
Windows 11, HVCI ON, usuario en grupo Administradores. VS 2026 + SDK .NET 10.0.401.
Al probar la GUI: captura con `PrintWindow` + `SetProcessDPIAware` (sin eso el rect sale virtualizado y cortado).

## Pendiente (sugerido, no hecho)

Vista top procesos · alertas por umbral · persistencia SQLite · export CSV · tema claro · benchmark con puntuación.
Archivos de apoyo: `README.md` (decisiones + limitaciones), `docs/panel.png`.
