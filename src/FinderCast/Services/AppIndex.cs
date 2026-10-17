namespace FinderCast.Services;

internal enum LaunchKind
{
    Application,
    Command,
}

/// <summary>
/// Algo que el lanzador puede abrir: un acceso directo del menú Inicio o un comando propio
/// (ajustes, salir). <see cref="Key"/> identifica el resultado para contar sus usos.
/// </summary>
internal sealed record LaunchItem(string Name, string Key, LaunchKind Kind)
{
    /// <summary>Ruta del acceso directo (solo aplicaciones).</summary>
    public string? Path { get; init; }

    /// <summary>Acción de un comando propio.</summary>
    public Action? Run { get; init; }

    /// <summary>Tipo de resultado, en el idioma actual.</summary>
    public string KindLabel => Kind == LaunchKind.Application ? Strings.Current.Application : Strings.Current.Command;

    /// <summary>Nombre preparado para buscar (minúsculas y sin tildes), calculado una vez.</summary>
    public string SearchName { get; } = Search.Normalize(Name);
}

/// <summary>
/// Índice de los accesos directos del menú Inicio (del usuario y de todos los usuarios). Se
/// lee en segundo plano y se vuelve a leer solo cuando cambian las carpetas.
/// </summary>
internal sealed class AppIndex : IDisposable
{
    private static readonly string[] Extensions = [".lnk", ".url", ".appref-ms"];

    // Accesos que no son la aplicación en sí: desinstaladores, ayudas, léame…
    private static readonly string[] Noise = ["uninstall", "desinstal", "readme", "léame", "leame", "help", "ayuda", "release notes"];

    private readonly string[] _folders =
    [
        Environment.GetFolderPath(Environment.SpecialFolder.Programs),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
    ];

    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly Timer _debounce;
    private IReadOnlyList<LaunchItem> _apps = [];
    private DateTime _scannedAt;

    public AppIndex(IReadOnlyList<LaunchItem> commands)
    {
        Commands = commands;
        _debounce = new Timer(_ => Refresh());

        foreach (var folder in _folders.Where(Directory.Exists))
        {
            var watcher = new FileSystemWatcher(folder)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
            };
            FileSystemEventHandler changed = (_, _) => _debounce.Change(TimeSpan.FromSeconds(1), Timeout.InfiniteTimeSpan);
            watcher.Created += changed;
            watcher.Deleted += changed;
            watcher.Renamed += (s, e) => changed(s, e);
            watcher.EnableRaisingEvents = true;
            _watchers.Add(watcher);
        }
    }

    /// <summary>Comandos propios. Se sustituyen al cambiar de idioma, porque se buscan por su nombre.</summary>
    public IReadOnlyList<LaunchItem> Commands
    {
        get;
        set
        {
            field = value;
            Items = [.. _apps, .. value];
        }
    }

    /// <summary>Aplicaciones y comandos, en una sola lista.</summary>
    public IReadOnlyList<LaunchItem> Items { get; private set; } = [];

    /// <summary>Lee de nuevo las carpetas en un hilo de fondo.</summary>
    public void Refresh()
    {
        _scannedAt = DateTime.UtcNow;
        Task.Run(() =>
        {
            _apps = Scan();
            Items = [.. _apps, .. Commands];
        });
    }

    /// <summary>
    /// Las apps de Microsoft Store no dejan accesos en las carpetas vigiladas: se vuelven a leer al
    /// abrir el lanzador si la última lectura tiene más de unos minutos.
    /// </summary>
    public void RefreshIfStale()
    {
        if (DateTime.UtcNow - _scannedAt > TimeSpan.FromMinutes(2)) Refresh();
    }

    /// <summary>True si el resultado con esa clave sigue en el índice.</summary>
    public bool Contains(string key) => _apps.Any(i => i.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    public void Dispose()
    {
        foreach (var watcher in _watchers) watcher.Dispose();
        _debounce.Dispose();
    }

    private List<LaunchItem> Scan()
    {
        var byName = new Dictionary<string, LaunchItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in _folders.Where(Directory.Exists))
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(folder, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                if (!Extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)) continue;
                var name = Path.GetFileNameWithoutExtension(file);
                if (Noise.Any(n => name.Contains(n, StringComparison.OrdinalIgnoreCase))) continue;

                // La misma app suele estar en las dos carpetas: gana la del usuario (la primera).
                byName.TryAdd(name, new LaunchItem(name, file, LaunchKind.Application) { Path = file });
            }
        }
        // Apps de Microsoft Store (Calculadora, Fotos…): si ya hay un acceso con el mismo nombre, gana el acceso.
        foreach (var app in PackagedApps()) byName.TryAdd(app.Name, app);

        return [.. byName.Values.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <summary>Apps empaquetadas del usuario, que se abren por su id: shell:AppsFolder\&lt;AUMID&gt;.</summary>
    private static List<LaunchItem> PackagedApps()
    {
        var apps = new List<LaunchItem>();
        try
        {
            var manager = new Windows.Management.Deployment.PackageManager();
            foreach (var package in manager.FindPackagesForUser(""))
            {
                try
                {
                    if (package.IsFramework || package.IsResourcePackage || package.IsBundle) continue;
                    foreach (var entry in package.GetAppListEntries())
                    {
                        var name = entry.DisplayInfo.DisplayName?.Trim();
                        var id = entry.AppUserModelId;
                        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(id)) continue;
                        if (name.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase)) continue;
                        var path = @"shell:AppsFolder\" + id;
                        apps.Add(new LaunchItem(name, path, LaunchKind.Application) { Path = path });
                    }
                }
                catch (Exception)
                {
                    // Un paquete dañado o a medio instalar no impide leer los demás.
                }
            }
        }
        catch (Exception)
        {
            // Sin acceso al catálogo de paquetes quedan los accesos del menú Inicio.
        }
        return apps;
    }
}
