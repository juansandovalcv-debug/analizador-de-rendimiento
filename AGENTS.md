# Reglas del proyecto

Monitor de rendimiento en **C# / .NET 10 / WinForms**. Contexto técnico: `CONTEXT.md`.

## Cómo trabajar aquí (ahorra contexto)

1. **`grep` antes de `read`.** Nunca leer un archivo entero si basta una búsqueda.
2. **Leer como máximo ~60 líneas** alrededor del cambio (`offset` + `limit`).
3. **`edit` siempre; `write` solo para archivos nuevos.** Nunca reescribir un archivo entero
   para cambiar tres líneas.
4. **Compilar en silencio:** `.\build.ps1`. No pegar la salida de MSBuild.
5. **Una sola captura de pantalla** al final y recortada. No verificar visualmente cada cambio.
6. **Responder telegráfico:** sin recaps, sin resumen de lo ya hecho, sin repetir el código.
7. **No explorar** el proyecto si la tarea nombra el archivo. Preguntar antes de tocar algo no pedido.

## Reglas duras (no cambiar sin motivo)

- TFM `net10.0-windows10.0.19041.0` en la app. Con `net10.0-windows` NuGet cae a SkiaSharp
  `net462` y la app revienta al arrancar.
- **PDH con `PdhAddEnglishCounter`**, nunca `PerformanceCounter` (nombres localizados).
- WMI **solo** para datos estáticos al arrancar. En el bucle cuesta 300-700 ms.
- `PlatformTarget=x64` (nativos de LibreHardwareMonitor).
- **`PcAnalyzer.Core` no referencia WinForms.** Es la separación que permite testear.
- Un sampler que falle no puede tumbar el panel: cada uno captura sus excepciones.

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
