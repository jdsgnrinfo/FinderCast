using System.Text.Json;

namespace FinderCast.Services;

internal sealed class AppSettings
{
    public int Version { get; set; } = 1;

    /// <summary>Si se muestra el icono en el área de notificación.</summary>
    public bool ShowTrayIcon { get; set; } = true;

    /// <summary>Código del idioma ("es", "en"), o null para usar el de Windows.</summary>
    public string? Language { get; set; }

    /// <summary>
    /// True una vez aplicado el valor predeterminado del inicio con Windows (activado). El estado
    /// real se guarda en el registro, no aquí: así se respeta si el usuario lo cambia desde Windows.
    /// </summary>
    public bool StartupConfigured { get; set; }

    /// <summary>Atajo global que abre el lanzador.</summary>
    public HotKey HotKey { get; set; } = HotKey.Default;

    /// <summary>Modificadores que, con Enter, abren el resultado como administrador.</summary>
    public HotKeyModifiers AdminShortcut { get; set; } = Services.AdminShortcut.Default;

    /// <summary>Veces que se ha abierto cada resultado (por su ruta): los más usados suben.</summary>
    public Dictionary<string, int> Launches { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Persistencia de <see cref="AppSettings"/> en %APPDATA%\FinderCast\settings.json.</summary>
internal sealed class SettingsStore(string filePath)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string FilePath { get; } = filePath;

    public static SettingsStore CreateDefault()
        => new(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FinderCast",
            "settings.json"));

    /// <summary>Devuelve los ajustes guardados, o los predeterminados si no existen o están dañados.</summary>
    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new AppSettings();
            using var stream = File.OpenRead(FilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(stream, JsonOptions) ?? new AppSettings();
            settings.Launches = new Dictionary<string, int>(settings.Launches, StringComparer.OrdinalIgnoreCase);
            // Sin modificadoras, Enter solo abriría todo como administrador: se vuelve al predeterminado.
            if (!AdminShortcut.IsValid(new HotKey(settings.AdminShortcut, Interop.Native.VK_RETURN))) settings.AdminShortcut = AdminShortcut.Default;
            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }
    }

    /// <summary>Escritura atómica: se escribe a un temporal y se reemplaza, para no dejar un JSON a medias.</summary>
    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Perder una preferencia no justifica interrumpir al usuario.
        }
    }
}
