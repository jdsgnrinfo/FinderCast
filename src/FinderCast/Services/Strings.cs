using System.Globalization;

namespace FinderCast.Services;

/// <summary>
/// Todos los textos que ve el usuario, en español e inglés. <see cref="Current"/> es el idioma
/// elegido en los ajustes (por defecto, el de Windows). Al cambiarlo se lanza <see cref="Changed"/>
/// y cada ventana vuelve a poner sus textos, sin reiniciar la app.
/// </summary>
internal sealed record Strings
{
    public required string Code { get; init; }

    /// <summary>Nombre del idioma en sí mismo, como lo muestra el desplegable.</summary>
    public required string Name { get; init; }

    // Ventana de ajustes.
    public required string Version { get; init; }
    public required string StartWithWindows { get; init; }
    public required string StartupNotChanged { get; init; }
    public required string ShowTrayIcon { get; init; }
    public required string ShowRecentApps { get; init; }
    public required string Language { get; init; }
    public required string Shortcut { get; init; }
    public required string AdminShortcut { get; init; }
    public required string PressShortcut { get; init; }
    public required string ShortcutInUse { get; init; }
    public required string ShortcutFailed { get; init; }
    public required string EndWithEnter { get; init; }
    public required string BuyMeACoffee { get; init; }

    // Nombres de teclas que cambian con el idioma.
    public required string SpaceKey { get; init; }

    // Lanzador.
    public required string SearchPlaceholder { get; init; }
    public required string Application { get; init; }
    public required string Command { get; init; }
    public required string NoResults { get; init; }
    public required string ConsoleKind { get; init; }
    public required string ConsoleHint { get; init; }
    public required string OpenAction { get; init; }
    public required string CopyAction { get; init; }
    public required string RunAsAdminAction { get; init; }
    public required string PinApp { get; init; }
    public required string UnpinApp { get; init; }
    public required string SettingsCommand { get; init; }
    public required string QuitCommand { get; init; }

    // Bandeja del sistema.
    public required string Open { get; init; }
    public required string Quit { get; init; }

    public static readonly Strings Spanish = new()
    {
        Code = "es",
        Name = "Español",
        Version = "Versión {0}",
        StartWithWindows = "Abrir al iniciar Windows",
        StartupNotChanged = "No se pudo cambiar el inicio con Windows.",
        ShowTrayIcon = "Mostrar icono en la bandeja",
        ShowRecentApps = "Mostrar las 5 últimas apps abiertas",
        Language = "Idioma",
        Shortcut = "Atajo para abrir",
        AdminShortcut = "Abrir como administrador",
        PressShortcut = "Pulsa el atajo…",
        ShortcutInUse = "Ya está en uso",
        EndWithEnter = "Termina con Enter",
        ShortcutFailed = "No se pudo activar el atajo {0}: lo usa otra app. Elige otro en los ajustes.",
        BuyMeACoffee = "Invítame un café",
        SpaceKey = "Espacio",
        SearchPlaceholder = "Buscar aplicaciones y comandos…",
        Application = "Aplicación",
        Command = "Comando",
        NoResults = "Sin resultados",
        ConsoleKind = "Ejecutar en terminal",
        ConsoleHint = "Escribe un comando de consola tras >",
        OpenAction = "Abrir",
        CopyAction = "Copiar",
        RunAsAdminAction = "Abrir como administrador",
        PinApp = "Anclar arriba",
        UnpinApp = "Quitar de arriba",
        SettingsCommand = "Ajustes de FinderCast",
        QuitCommand = "Salir de FinderCast",
        Open = "Abrir {0}",
        Quit = "Salir",
    };

    public static readonly Strings English = new()
    {
        Code = "en",
        Name = "English",
        Version = "Version {0}",
        StartWithWindows = "Open at Windows startup",
        StartupNotChanged = "Couldn't change starting with Windows.",
        ShowTrayIcon = "Show icon in the notification area",
        ShowRecentApps = "Show the last 5 apps opened",
        Language = "Language",
        Shortcut = "Shortcut to open",
        AdminShortcut = "Run as administrator",
        PressShortcut = "Press the shortcut…",
        ShortcutInUse = "Already in use",
        EndWithEnter = "End with Enter",
        ShortcutFailed = "Couldn't enable the {0} shortcut: another app uses it. Pick another one in the settings.",
        BuyMeACoffee = "Buy me a coffee",
        SpaceKey = "Space",
        SearchPlaceholder = "Search apps and commands…",
        Application = "Application",
        Command = "Command",
        NoResults = "No results",
        ConsoleKind = "Run in terminal",
        ConsoleHint = "Type a console command after >",
        OpenAction = "Open",
        CopyAction = "Copy",
        RunAsAdminAction = "Run as administrator",
        PinApp = "Pin to top",
        UnpinApp = "Unpin from top",
        SettingsCommand = "FinderCast Settings",
        QuitCommand = "Quit FinderCast",
        Open = "Open {0}",
        Quit = "Quit",
    };

    public static IReadOnlyList<Strings> All { get; } = [Spanish, English];

    private static Strings _current = For(null);

    public static Strings Current
    {
        get => _current;
        set
        {
            if (value == _current) return;
            _current = value;
            Changed?.Invoke();
        }
    }

    public static event Action? Changed;

    /// <summary>El idioma con ese código; el de Windows si no hay ninguno guardado.</summary>
    public static Strings For(string? code) => code switch
    {
        "es" => Spanish,
        "en" => English,
        _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es" ? Spanish : English,
    };
}
