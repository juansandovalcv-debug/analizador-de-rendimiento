namespace PcAnalyzer.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Inicializa DPI, estilos visuales y estilos de texto de .NET moderno.
        ApplicationConfiguration.Initialize();

        // Sin esto, cualquier excepción no controlada cierra la app sin dejar rastro.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Log("UI", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log("AppDomain", e.ExceptionObject as Exception);

        Application.Run(new MainForm());
    }

    private static void Log(string source, Exception? exception)
    {
        try
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PcAnalyzer");

            Directory.CreateDirectory(folder);

            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}: " +
                          (exception?.ToString() ?? "sin excepción") +
                          Environment.NewLine;

            File.AppendAllText(Path.Combine(folder, "app.log"), line);
        }
        catch
        {
            // Si ni siquiera se puede escribir el log, no hay nada más que hacer.
        }
    }
}
