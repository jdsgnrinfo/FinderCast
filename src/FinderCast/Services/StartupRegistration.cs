using Microsoft.Win32;

namespace FinderCast.Services;

/// <summary>
/// Inicio con Windows para el usuario actual (HKCU\...\Run, sin permisos de administrador).
/// El registro es la fuente de verdad: también se respeta si el usuario lo desactiva desde el
/// Administrador de tareas → Aplicaciones de arranque (clave StartupApproved).
/// </summary>
internal static class StartupRegistration
{
    /// <summary>Argumento con el que arranca al iniciar sesión: en segundo plano, sin abrir ventanas.</summary>
    public const string BackgroundArgument = "--background";

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    private static string ValueName => App.ProductName;

    private static string Command => $"\"{Environment.ProcessPath}\" {BackgroundArgument}";

    public static bool IsEnabled
    {
        get
        {
            using var run = Registry.CurrentUser.OpenSubKey(RunKey);
            if (run?.GetValue(ValueName) is not string) return false;

            // En StartupApproved, el primer byte impar (03, 07…) significa "deshabilitado".
            using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey);
            return approved?.GetValue(ValueName) is not byte[] { Length: > 0 } state || state[0] % 2 == 0;
        }
    }

    /// <exception cref="UnauthorizedAccessException">Si el registro no se puede escribir.</exception>
    public static void SetEnabled(bool enabled)
    {
        using var run = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            run.SetValue(ValueName, Command);
            // Quita una posible desactivación previa desde el Administrador de tareas.
            using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey, writable: true);
            approved?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        else
        {
            run.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    /// <summary>
    /// La primera vez que se ejecuta la app activa el inicio con Windows (opción predeterminada).
    /// Después, si sigue activo, actualiza la ruta por si el ejecutable cambió de sitio.
    /// </summary>
    /// <returns>True si es la primera configuración (hay que guardar los ajustes).</returns>
    public static bool EnsureDefault(bool alreadyConfigured)
    {
        try
        {
            if (!alreadyConfigured)
            {
                SetEnabled(true);
                return true;
            }

            using var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (run?.GetValue(ValueName) is string current && current != Command) run.SetValue(ValueName, Command);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            // Sin acceso al registro la app funciona igual; solo no arrancará sola.
        }
        return false;
    }
}
