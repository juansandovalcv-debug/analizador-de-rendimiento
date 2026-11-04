# CONTEXT — Analizador de rendimiento

Monitor de rendimiento de PC en tiempo real. Windows, **C# / .NET 10 / WinForms**.
Repo: `github.com/juansandovalcv-debug/analizador-de-rendimiento`. Reglas de trabajo: `AGENTS.md`.

## Estado

Compila 0 errores. Seis métricas (CPU, RAM, GPU, disco, red, temperatura) a 1 muestra/s con
histórico de 120 puntos, más un overlay compacto para jugar. Verificado en el equipo del usuario.

## Archivos

- `PcAnalyzer.Core` (`net10.0-windows`) — lógica, **sin WinForms**.
  `Samplers/*.cs`, `PerformanceMonitor.cs`, `HardwareInfoProvider.cs` (WMI, una vez),
  `MemoryIntegrity.cs` (HVCI), `Native/Pdh.cs`, `Native/NativeMethods.cs`.
- `PcAnalyzer.App` (`net10.0-windows10.0.19041.0`, x64) — UI.
  `MainForm.{cs,Render,Overlay,Lifecycle}.cs`, `OverlayForm.{cs,Paint,Input,Menu}.cs`,
  `Controls/MetricCard.cs`, `Native/HotKey.cs`, `app.manifest` (asInvoker).

## Reglas duras

TFM `10.0.19041.0` (si no, SkiaSharp cae a net462 y revienta al arrancar) · PDH con
`PdhAddEnglishCounter`, nunca `PerformanceCounter` (nombres localizados) · WMI solo al arrancar
(en el bucle cuesta 300-700 ms) · x64 (nativos de LHM) · Core sin WinForms.

## Fuentes de datos

CPU % `GetSystemTimes` · CPU MHz PDH `Processor Frequency` × `% Processor Performance` ·
RAM `GlobalMemoryStatusEx` · Disco PDH `\PhysicalDisk(_Total)\% Disk Time` + `Read/Write Bytes/sec` ·
Red `GetIPStatistics` delta · GPU PDH `\GPU Engine(*engtype_3D)\Utilization Percentage` ·
Temp GPU por NVML (sin admin) · Temp CPU por MSR (driver de kernel).

## Temperatura de CPU: bloqueada por HVCI

Integridad de memoria activa en el equipo → Windows rechaza `WinRing0` **aunque se ejecute como
administrador**; los 55 sensores de CPU dan `null`. ACPI descartado (clavado en 27.9 °C con 16
hilos al 100 %). Única salida: desactivar HVCI y reiniciar. La app lo detecta y lo explica.

## Overlay (modo compacto)

280×104, TopMost, Opacity 0.88, `WS_EX_NOACTIVATE | TOOLWINDOW` (no roba el foco), arrastrable
con ajuste a la esquina, doble clic vuelve al panel, clic derecho menú. `MainForm` se **minimiza**,
no se oculta. Atajo global: prueba Ctrl+Alt+M → +O → +P → +K → Ctrl+Shift+M (**M está ocupado**).

## Bugs corregidos (no reintroducir)

GPU híbrida sobrescribía con `null` la lectura de la dGPU · la temperatura mostraba Hot Spot en vez
de GPU Core · el nombre de GPU salía la iGPU · la red salía "Radmin VPN" · el detalle de la tarjeta
estaba arriba a la derecha en vez de bajo el valor.

## Comandos

`.\build.ps1`, `.\build.ps1 -Release`, `.\build.ps1 -Publish` (los dos `.exe`, en serie).

## Entorno

ASUS FX507VV, i7-13620H 10C/16T, RTX 4060 Laptop + UHD (híbrida), 16 GB, 1920×1080 @125 %, HVCI ON.
Capturas del overlay con `CopyFromScreen`: `PrintWindow` sale negro (ventana por capas).

## Pendiente

Top procesos · alertas por umbral · SQLite · CSV · tema claro · benchmark.
