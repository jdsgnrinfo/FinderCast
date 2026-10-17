using FinderCast.Interop;
using FinderCast.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace FinderCast;

/// <summary>
/// La app vive en segundo plano: un atajo global abre el lanzador y el icono de la bandeja da
/// acceso a los ajustes. Las ventanas nunca se cierran, solo se ocultan.
/// </summary>
public partial class App : Application
{
    /// <summary>Nombre del producto, centralizado en un solo sitio.</summary>
    public const string ProductName = "FinderCast";

    public static string IconPath { get; } = Path.Combine(AppContext.BaseDirectory, "Assets", "FinderCast.ico");

    public static string LogoPath { get; } = Path.Combine(AppContext.BaseDirectory, "Assets", "FinderCast.png");

    private readonly SingleInstance _instance;
    private readonly string[] _args;
    private SettingsStore? _store;
    private AppSettings? _settings;
    private AppIndex? _index;
    private MainWindow? _launcher;
    private SettingsWindow? _settingsWindow;
    private MessageWindow? _messages;
    private GlobalHotKey? _hotKey;
    private TrayIcon? _tray;

    internal App(SingleInstance instance, string[] args)
    {
        _instance = instance;
        _args = args;
        InitializeComponent();
        // Sigue en marcha con las ventanas ocultas: solo "Salir" la cierra.
        DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs e)
    {
        _store = SettingsStore.CreateDefault();
        _settings = _store.Load();
        Strings.Current = Strings.For(_settings.Language);

        _index = new AppIndex(CreateCommands());
        _index.Refresh();
        Strings.Changed += () => _index.Commands = CreateCommands();

        _launcher = new MainWindow(_index, _settings, _store);
        _settingsWindow = new SettingsWindow(_settings, _store, ChangeHotKey);
        _settingsWindow.TrayIconSettingChanged += () => _tray!.Visible = _settings.ShowTrayIcon;

        _messages = new MessageWindow();
        _hotKey = new GlobalHotKey(_messages);
        _hotKey.Pressed += _launcher.Toggle;
        _tray = new TrayIcon(ShowSettings, Quit)
        {
            Visible = _settings.ShowTrayIcon,
        };

        // Inicio con Windows activado por defecto la primera vez que se ejecuta la app.
        if (StartupRegistration.EnsureDefault(_settings.StartupConfigured))
        {
            _settings.StartupConfigured = true;
            _store.Save(_settings);
        }

        var dispatcher = DispatcherQueue.GetForCurrentThread();
        _instance.Listen(() => dispatcher.TryEnqueue(ShowSettings));

        if (!_hotKey.Register(_settings.HotKey))
        {
            // Otra app ya usa el atajo: los ajustes lo explican y dejan elegir otro.
            ShowSettings();
            _settingsWindow.ShowShortcutError(_settings.HotKey);
            return;
        }

        // Sin icono en la bandeja, abrir la app muestra los ajustes: si no, no habría forma de
        // llegar a ellos. Al iniciar sesión (--background) no se abre nada.
        var atLogin = _args.Contains(StartupRegistration.BackgroundArgument);
        if (!_settings.ShowTrayIcon && !atLogin) ShowSettings();
    }

    private IReadOnlyList<LaunchItem> CreateCommands() =>
    [
        new(Strings.Current.SettingsCommand, "command:settings", LaunchKind.Command) { Run = ShowSettings },
        new(Strings.Current.QuitCommand, "command:quit", LaunchKind.Command) { Run = Quit },
    ];

    private void ShowSettings() => _settingsWindow?.ShowCentered();

    /// <summary>Cambia el atajo global. Si el nuevo está ocupado, se queda el anterior.</summary>
    private bool ChangeHotKey(HotKey hotKey)
    {
        var previous = _hotKey!.Current ?? _settings!.HotKey;
        if (_hotKey.Register(hotKey))
        {
            _settings!.HotKey = hotKey;
            _store!.Save(_settings);
            return true;
        }

        _hotKey.Register(previous);
        return false;
    }

    private void Quit()
    {
        _hotKey?.Dispose();
        _tray?.Dispose();
        _messages?.Dispose();
        _index?.Dispose();
        _launcher?.CloseForExit();
        _settingsWindow?.CloseForExit();
        Exit();
    }
}
