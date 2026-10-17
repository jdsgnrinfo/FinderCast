using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using FinderCast.Interop;
using FinderCast.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;
using Windows.System;
using WinRT.Interop;

namespace FinderCast;

/// <summary>
/// El lanzador: dos tarjetas flotantes sobre Acrylic, la de búsqueda (esta ventana) y la de
/// resultados (<see cref="ResultsWindow"/>), que aparecen con el atajo global en la pantalla del
/// puntero, buscan mientras se escribe y se ocultan al abrir algo, con Esc o al perder el foco.
/// Se maneja entera con el teclado (flechas, Enter, Esc).
/// </summary>
public sealed partial class MainWindow : Window
{
    private const double WidthDip = 600;
    private const double SearchHeightDip = 56;
    private const double GapDip = 12;
    // Justo cinco filas, sin cortar la sexta: 5 × (48 de fila + 2 de margen) + 16 de relleno de
    // la lista + 1 de la línea + 52 del pie. Si cambian esas medidas en ResultsWindow.xaml, aquí también.
    private const double ResultsHeightDip = 5 * (48 + 2) + 16 + 1 + 52;

    private readonly AppIndex _index;
    private readonly AppSettings _settings;
    private readonly SettingsStore _store;
    private readonly ObservableCollection<ResultItem> _results = [];
    private readonly IntPtr _hwnd;
    private readonly ResultsWindow _panel;
    private RectInt32 _panelBounds;
    private bool _allowClose;

    internal MainWindow(AppIndex index, AppSettings settings, SettingsStore store)
    {
        _index = index;
        _settings = settings;
        _store = store;
        InitializeComponent();

        _hwnd = WindowNative.GetWindowHandle(this);
        AppWindow.Title = App.ProductName;
        AppWindow.SetIcon(App.IconPath);
        // Ni en la barra de tareas ni en Alt+Tab: es una paleta, no una ventana de trabajo.
        AppWindow.IsShownInSwitchers = false;

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = true;
            presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
        }
        // No se expande nunca.
        WindowChrome.PreventMaximize(this);

        SystemBackdrop = new Controls.ActiveAcrylic();
        // Sin barra de título, con el borde, las esquinas y la sombra originales de Windows 11. No
        // se usa ExtendsContentIntoTitleBar: WinUI deja entonces una línea oscura en el borde superior.
        WindowChrome.UseNativeBorderWithoutTitleBar(this);

        // El mismo borde en las dos tarjetas, tenga el foco la que lo tenga.
        if (Content is FrameworkElement content)
        {
            WindowChrome.UseFixedBorder(_hwnd, content.ActualTheme == ElementTheme.Dark);
            content.ActualThemeChanged += (_, _) => WindowChrome.UseFixedBorder(_hwnd, content.ActualTheme == ElementTheme.Dark);
        }

        _panel = new ResultsWindow(_hwnd);
        ApplyAdminShortcut();
        Results.ItemsSource = _results;
        Results.ItemClick += OnResultClick;
        Results.SelectionChanged += (_, e) =>
        {
            foreach (var item in e.RemovedItems.OfType<ResultItem>()) item.IsSelected = false;
            foreach (var item in e.AddedItems.OfType<ResultItem>()) item.IsSelected = true;
        };

        AppWindow.Closing += (_, args) =>
        {
            if (_allowClose) return;
            args.Cancel = true; // Alt+F4 solo la oculta.
            HideLauncher();
        };
        Activated += (_, args) =>
        {
            // Un clic en los resultados no cuenta como salir: se mira qué ventana queda delante.
            if (args.WindowActivationState != WindowActivationState.Deactivated) return;
            DispatcherQueue.TryEnqueue(() =>
            {
                var foreground = Native.GetForegroundWindow();
                if (foreground == _panel.Hwnd)
                {
                    Native.SetForegroundWindow(_hwnd);
                    return;
                }
                if (foreground != _hwnd) HideLauncher();
            });
        };

        ApplyStrings();
        Strings.Changed += ApplyStrings;
    }

    /// <summary>El engranaje de la búsqueda: abrir los ajustes.</summary>
    public event Action? SettingsRequested;

    public bool IsShown => AppWindow.IsVisible;

    /// <summary>El atajo global abre el lanzador o, si ya está abierto, lo cierra.</summary>
    public void Toggle()
    {
        if (IsShown) HideLauncher();
        else ShowLauncher();
    }

    /// <summary>Lo muestra vacío y centrado en la pantalla del puntero, un poco por encima del centro.</summary>
    public void ShowLauncher()
    {
        Native.GetCursorPos(out var cursor);
        var area = DisplayArea.GetFromPoint(new PointInt32(cursor.X, cursor.Y), DisplayAreaFallback.Nearest).WorkArea;
        var scale = Native.ScaleAt(cursor);
        var width = (int)Math.Round(WidthDip * scale);
        var searchHeight = (int)Math.Round(SearchHeightDip * scale);
        var gap = (int)Math.Round(GapDip * scale);
        var resultsHeight = (int)Math.Round(ResultsHeightDip * scale);
        var x = area.X + (area.Width - width) / 2;
        var y = area.Y + (int)((area.Height - searchHeight - gap - resultsHeight) * 0.3);
        AppWindow.MoveAndResize(new RectInt32(x, y, width, searchHeight));
        // Los resultados aparecen debajo en cuanto se escribe algo (UpdateResults).
        _panelBounds = new RectInt32(x, y + searchHeight + gap, width, resultsHeight);

        _index.RefreshIfStale();
        SearchBox.Text = "";
        UpdateResults();

        Activate();
        Native.SetForegroundWindow(_hwnd);
        SearchBox.Focus(FocusState.Programmatic);
    }

    public void HideLauncher()
    {
        _panel.Hide();
        if (IsShown) AppWindow.Hide();
    }

    public void CloseForExit()
    {
        _allowClose = true;
        _panel.CloseForExit();
        Close();
    }

    private ListView Results => _panel.List;

    private TextBlock EmptyText => _panel.Empty;

    private void ApplyStrings()
    {
        var text = Strings.Current;
        SearchBox.PlaceholderText = text.SearchPlaceholder;
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        HideLauncher();
        SettingsRequested?.Invoke();
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => UpdateResults();

    private void UpdateResults()
    {
        var ranked = Search.Rank(SearchBox.Text, _index.Items, _settings.Launches);
        _results.Clear();
        foreach (var item in ranked) _results.Add(new ResultItem(item));

        Results.SelectedIndex = _results.Count > 0 ? 0 : -1;
        if (_results.Count > 0) Results.ScrollIntoView(_results[0]);
        EmptyText.Visibility = _results.Count == 0 && SearchBox.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        // Sin nada escrito, solo la barra de búsqueda.
        if (SearchBox.Text.Trim().Length == 0) _panel.Hide();
        else _panel.ShowAt(_panelBounds);

        foreach (var result in _results) _ = result.LoadIconAsync();
    }

    /// <summary>Las flechas mueven la selección sin sacar el foco de la búsqueda.</summary>
    private void OnSearchKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Down:
                MoveSelection(+1);
                break;
            case VirtualKey.Up:
                MoveSelection(-1);
                break;
            case VirtualKey.Enter:
                // Sin texto la lista está oculta: Enter no abre nada que no se vea.
                if (SearchBox.Text.Trim().Length > 0 && Results.SelectedItem is ResultItem selected) Launch(selected.Item, LaunchModeFromKeys());
                break;
            case VirtualKey.Escape:
                HideLauncher();
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Escape) return;
        HideLauncher();
        e.Handled = true;
    }

    private void MoveSelection(int delta)
    {
        if (_results.Count == 0) return;
        var index = Math.Clamp(Results.SelectedIndex + delta, 0, _results.Count - 1);
        Results.SelectedIndex = index;
        Results.ScrollIntoView(_results[index]);
    }

    private void OnResultClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ResultItem result) Launch(result.Item);
    }

    /// <summary>Con el atajo de los ajustes (Ctrl+Enter por defecto), como administrador.</summary>
    private LaunchMode LaunchModeFromKeys() => AdminShortcut.IsHeld(_settings.AdminShortcut) ? LaunchMode.RunAsAdmin : LaunchMode.Open;

    /// <summary>Pone en el pie las teclas del atajo de administrador elegido en los ajustes.</summary>
    public void ApplyAdminShortcut() => _panel.ShowAdminShortcut(_settings.AdminShortcut);

    private void Launch(LaunchItem item, LaunchMode mode = LaunchMode.Open)
    {
        HideLauncher();

        // Las apps de Microsoft Store no se elevan así: se abren sin más.
        var packaged = item.Path?.StartsWith("shell:", StringComparison.OrdinalIgnoreCase) == true;

        if (item.Kind == LaunchKind.Application)
        {
            _settings.Launches[item.Key] = _settings.Launches.GetValueOrDefault(item.Key) + 1;
            // Las apps desinstaladas dejan de contar: el archivo de ajustes no crece sin fin.
            foreach (var key in _settings.Launches.Keys.Where(k => !_index.Contains(k)).ToList()) _settings.Launches.Remove(key);
            _store.Save(_settings);
        }

        if (item.Run is { } run)
        {
            run();
            return;
        }

        try
        {
            var start = new ProcessStartInfo(item.Path!) { UseShellExecute = true };
            if (mode == LaunchMode.RunAsAdmin && !packaged) start.Verb = "runas";
            Process.Start(start);
        }
        // También si se cancela el aviso de Control de cuentas de usuario.
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            // El acceso directo apunta a algo que ya no existe: el índice se pondrá al día solo.
        }
    }
}

internal enum LaunchMode
{
    Open,
    RunAsAdmin,
}

/// <summary>Un resultado de la lista, con su icono cargado en segundo plano.</summary>
public sealed partial class ResultItem : INotifyPropertyChanged
{
    // Los iconos se reutilizan entre búsquedas: buscar no vuelve a pedirlos al Explorador.
    private static readonly Dictionary<string, ImageSource> IconCache = new(StringComparer.OrdinalIgnoreCase);
    private static BitmapImage? _appLogo;

    internal ResultItem(LaunchItem item) => Item = item;

    public event PropertyChangedEventHandler? PropertyChanged;

    internal LaunchItem Item { get; }

    public string Name => Item.Name;

    public string KindLabel => Item.KindLabel;

    /// <summary>Versión del programa, solo en el resultado elegido ("v1.0.0").</summary>
    public string Detail
    {
        get;
        private set
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Detail)));
        }
    } = "";

    internal bool IsSelected
    {
        get;
        set
        {
            field = value;
            if (!value) Detail = "";
            else _ = LoadVersionAsync();
        }
    }

    private static readonly Dictionary<string, string> VersionCache = new(StringComparer.OrdinalIgnoreCase);

    private async Task LoadVersionAsync()
    {
        if (Item.Kind != LaunchKind.Application || Item.Path is not { } path) return;
        if (!VersionCache.TryGetValue(path, out var version))
        {
            version = await Task.Run(() => ReadVersion(path));
            VersionCache[path] = version;
        }
        if (IsSelected) Detail = version;
    }

    /// <summary>"v" y los tres primeros números de la versión del programa, o vacío si no tiene.</summary>
    private static string ReadVersion(string path)
    {
        try
        {
            if (ShellIcon.LinkTarget(path) is not { } target) return "";
            var info = FileVersionInfo.GetVersionInfo(target);
            if (info.FileMajorPart == 0 && info.FileMinorPart == 0 && info.FileBuildPart == 0) return "";
            return $"v{info.FileMajorPart}.{info.FileMinorPart}.{info.FileBuildPart}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "";
        }
    }

    public ImageSource? Icon
    {
        get;
        private set
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon)));
        }
    }

    internal async Task LoadIconAsync()
    {
        if (Item.Kind == LaunchKind.Command)
        {
            Icon = _appLogo ??= new BitmapImage(new Uri(App.LogoPath));
            return;
        }

        if (IconCache.TryGetValue(Item.Key, out var cached))
        {
            Icon = cached;
            return;
        }

        // Se pide al Shell fuera del hilo de la interfaz; 64 px se ven nítidos hasta el 225 % de escala.
        var path = Item.Path!;
        var icon = await Task.Run(() => ShellIcon.Load(path, 64));
        if (icon is not { } loaded) return;

        var image = new WriteableBitmap(loaded.Width, loaded.Height);
        using (var stream = image.PixelBuffer.AsStream()) stream.Write(loaded.Pixels);
        image.Invalidate();
        IconCache[Item.Key] = image;
        Icon = image;
    }
}
