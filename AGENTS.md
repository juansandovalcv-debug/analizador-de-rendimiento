# Reglas del proyecto

Monitor de rendimiento en **C# / .NET 10 / WinForms**. Contexto técnico: `CONTEXT.md`.
Reglas generales de trabajo: globales (`~/.config/opencode/AGENTS.md`); aquí solo lo del proyecto.

## Reglas duras (no cambiar sin motivo)

- TFM `net10.0-windows10.0.19041.0` en la app. Con `net10.0-windows` NuGet cae a SkiaSharp
  `net462` y la app revienta al arrancar.
- **PDH con `PdhAddEnglishCounter`**, nunca `PerformanceCounter` (nombres localizados).
- WMI **solo** para datos estáticos al arrancar. En el bucle cuesta 300-700 ms.
- `PlatformTarget=x64` (nativos de LibreHardwareMonitor).
- **`PcAnalyzer.Core` no referencia WinForms.** Es la separación que permite testear.
- Un sampler que falle no puede tumbar el panel: cada uno captura sus excepciones.

## Ramas

Trabajar en `versiones-de-prueba`, **nunca commitear directo a `main`**. `main` solo recibe
merges cuando el usuario confirma que la versión funciona. Push a la rama tras cada cambio.

## Comandos

```powershell
.\build.ps1              # Debug, solo errores
.\build.ps1 -Release     # Release
.\build.ps1 -Publish     # los dos .exe de publish\ (secuencial: en paralelo chocan en obj\)
```

## Estilo

- Español en UI, comentarios y mensajes. Identificadores en inglés.
- Comentar el **porqué**, no el qué.
- Colores y fuentes centralizados en `MainForm` y `MetricCard`.
- Archivos pequeños por responsabilidad: `MainForm.*.cs`, `OverlayForm.*.cs` son parciales.
