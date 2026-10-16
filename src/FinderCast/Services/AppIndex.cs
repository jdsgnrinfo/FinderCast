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
    public void Refresh() => Task.Run(() =>
    {
        _apps = Scan();
        Items = [.. _apps, .. Commands];
    });

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
        return [.. byName.Values.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)];
    }
}
