using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace FinderCast.Services;

/// <summary>
/// Comandos de consola desde el lanzador: lo que se escribe tras "&gt;" (por ejemplo
/// "&gt; winget upgrade --all" o "&gt; ipconfig /all") se ejecuta en PowerShell, dentro de
/// Windows Terminal si está instalado, en una ventana que se queda abierta con el resultado.
/// </summary>
internal static class ConsoleCommand
{
    public const char Prefix = '>';

    /// <summary>El comando escrito tras el prefijo, o null si el texto no empieza por él.</summary>
    public static string? Parse(string text)
    {
        var trimmed = text.TrimStart();
        return trimmed.StartsWith(Prefix) ? trimmed[1..].Trim() : null;
    }

    /// <summary>PowerShell 7 si está instalado; si no, Windows PowerShell, que viene con Windows.</summary>
    public static string ShellPath { get; } = FindInPath("pwsh.exe")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe");

    private static string? TerminalPath { get; } = FindTerminal();

    public static LaunchItem Item(string command) => new(command, "console:" + command, LaunchKind.Console) { Path = ShellPath };

    /// <summary>Abre la consola con el comando; como administrador, pasa por el aviso de UAC.</summary>
    public static void Run(string command, bool asAdmin)
    {
        // Codificado en Base64 (UTF-16), PowerShell lo recibe tal cual: sin problemas de comillas,
        // ni del ";" que Windows Terminal tomaría como separador de sus propios subcomandos.
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
        var shellArguments = $"-NoLogo -NoExit -EncodedCommand {encoded}";

        var start = TerminalPath is { } terminal
            ? new ProcessStartInfo(terminal, $"\"{ShellPath}\" {shellArguments}")
            : new ProcessStartInfo(ShellPath, shellArguments);
        start.UseShellExecute = true;
        start.WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (asAdmin) start.Verb = "runas";

        try
        {
            Process.Start(start);
        }
        // También si se cancela el aviso de Control de cuentas de usuario.
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
        }
    }

    private static string? FindTerminal()
    {
        var alias = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WindowsApps\wt.exe");
        return File.Exists(alias) ? alias : FindInPath("wt.exe");
    }

    private static string? FindInPath(string file)
    {
        foreach (var folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(folder.Trim(), file);
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException)
            {
                // Una entrada del PATH con caracteres no válidos: se salta.
            }
        }
        return null;
    }
}
