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
/// El lanzador: dos tarjetas flotantes sobre Acrylic, la de ancladas y búsqueda (esta ventana) y
/// la de resultados (<see cref="ResultsWindow"/>). Aparece con el atajo global en la pantalla del puntero, busca mientras se escribe
/// y se oculta al abrir algo, con Esc o al perder el foco. Se maneja entera con el teclado.
/// </summary>
public sealed partial class MainWindow : Window
{
    private const double WidthDip = 600;
    private const double SearchHeightDip = 56;
    // Fila de ancladas: 16 de margen superior + 44 de botón.
    private const double PinnedHeightDip = 16 + 44;
    private const double GapDip = 12;
    // Justo cinco filas, sin cortar la sexta: 5 × (44 de fila + 2 de margen) + 16 de relleno de la
    // lista. Si cambian esas medidas en ResultsWindow.xaml, aquí también.
    private const double ResultsHeightDip = 5 * (44 + 2) + 16;
    private const int MaxPinned = 6;

    private readonly AppIndex _index;
    private readonly AppSettings _settings;
    private readonly SettingsStore _store;
    private readonly ObservableCollection<ResultItem> _results = [];
    private readonly ObservableCollection<ResultItem> _pinned = [];
    private readonly IntPtr _hwnd;
    private readonly ResultsWindow _panel;
    private RectInt32 _panelBounds;
    private bool _menuOpen;
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

        if (Content is FrameworkElement content)
        {
            WindowChrome.UseFixedBorder(_hwnd, content.ActualTheme == ElementTheme.Dark);
            content.ActualThemeChanged += (_, _) => WindowChrome.UseFixedBorder(_hwnd, content.ActualTheme == ElementTheme.Dark);
        }

        PinnedList.ItemsSource = _pinned;
        _panel = new ResultsWindow(_hwnd);
        _panel.ItemRightTapped += OnItemRightTapped;
        _panel.UseGrid(false, _results);
        // La lista de apps y la cuadrícula de emojis se manejan igual; solo se ve una.
        foreach (var list in new[] { _panel.List, _panel.Grid })
        {
            list.ItemClick += OnResultClick;
            list.SelectionChanged += (_, e) =>
            {
                foreach (var item in e.RemovedItems.OfType<ResultItem>()) item.IsSelected = false;
                foreach (var item in e.AddedItems.OfType<ResultItem>()) item.IsSelected = true;
            };
        }

        AppWindow.Closing += (_, args) =>
        {
            if (_allowClose) return;
            args.Cancel = true; // Alt+F4 solo la oculta.
            HideLauncher();
        };
        Activated += (_, args) =>
        {
            // El menú de anclar puede abrir su propia ventana: mientras está abierto no cuenta como salir.
            if (args.WindowActivationState != WindowActivationState.Deactivated || _menuOpen) return;
            DispatcherQueue.TryEnqueue(() =>
            {
                // Un clic en los resultados no cuenta como salir: se mira qué ventana queda delante.
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
        int Px(double dip) => (int)Math.Round(dip * scale);
        var width = Px(WidthDip);
        var topHeight = Px(PinnedHeightDip + SearchHeightDip);
        var gap = Px(GapDip);
        var resultsHeight = Px(ResultsHeightDip);
        var x = area.X + (area.Width - width) / 2;
        var y = area.Y + (int)((area.Height - topHeight - gap - resultsHeight) * 0.3);
        // Ninguna de las dos cambia de tamaño a la vista: Windows pintaría de negro lo nuevo.
        AppWindow.MoveAndResize(new RectInt32(x, y, width, topHeight));
        _panelBounds = new RectInt32(x, y + topHeight + gap, width, resultsHeight);

        _index.RefreshIfStale();
        UpdatePinned();
        SearchBox.Text = "";
        UpdateResults();

        Activate();
        Native.SetForegroundWindow(_hwnd);
        SearchBox.Focus(FocusState.Programmatic);
    }

    /// <summary>
    /// Prepara la cuadrícula de emojis al arrancar: lee la tabla en segundo plano y la dibuja una vez
    /// fuera de la pantalla. Si no, el primer ":" tarda y la tarjeta se ve negra mientras se dibuja.
    /// </summary>
    public async void WarmUp()
    {
        var emojis = await Task.Run(() => EmojiCatalog.Find(""));
        if (IsShown) return;
        _panel.UseGrid(true, new ObservableCollection<ResultItem>(emojis.Select(e => new ResultItem(e))));
        _panel.ShowAt(new RectInt32(-32000, -32000, 600, 300));
        await Task.Delay(1000);
        if (IsShown) return;
        _panel.Hide();
        _panel.UseGrid(false, _results);
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

    private const int RecentCount = 5;

    /// <summary>Las últimas apps abiertas que siguen instaladas, o ninguna si está desactivado.</summary>
    private IReadOnlyList<LaunchItem> RecentItems() => _settings.ShowRecentApps ? [.. ByKeys(_settings.RecentApps).Take(RecentCount)] : [];

    /// <summary>Las apps del índice con esas claves, en ese orden; las desinstaladas se saltan.</summary>
    private IEnumerable<LaunchItem> ByKeys(IEnumerable<string> keys)
    {
        var byKey = _index.Items.ToDictionary(i => i.Key, StringComparer.OrdinalIgnoreCase);
        return keys.Select(k => byKey.GetValueOrDefault(k)).OfType<LaunchItem>();
    }

    private ListViewBase ActiveList => _panel.ActiveList;

    private bool IsGrid => _panel.IsGrid;

    private int GridColumns => _panel.GridColumns;

    private void ApplyStrings()
    {
        var text = Strings.Current;
        SearchBox.PlaceholderText = text.SearchPlaceholder;
        ToolTipService.SetToolTip(PinButton, text.PinApp);
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        HideLauncher();
        SettingsRequested?.Invoke();
    }

    private void UpdatePinned()
    {
        _pinned.Clear();
        foreach (var item in ByKeys(_settings.PinnedApps)) _pinned.Add(new ResultItem(item));
        foreach (var item in _pinned) _ = item.LoadIconAsync();
        PinButton.Visibility = _pinned.Count < MaxPinned ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SetPinned(LaunchItem item, bool pinned)
    {
        _settings.PinnedApps.RemoveAll(k => k.Equals(item.Key, StringComparison.OrdinalIgnoreCase));
        // Las desinstaladas dejan su hueco libre.
        _settings.PinnedApps.RemoveAll(k => !_index.Contains(k));
        if (pinned && _settings.PinnedApps.Count < MaxPinned) _settings.PinnedApps.Add(item.Key);
        _store.Save(_settings);
        UpdatePinned();
    }

    /// <summary>El "+": ancla la app elegida en los resultados.</summary>
    private void OnPinClick(object sender, RoutedEventArgs e)
    {
        if (ActiveList.SelectedItem is ResultItem { Item.Kind: LaunchKind.Application } selected) SetPinned(selected.Item, true);
        SearchBox.Focus(FocusState.Programmatic);
    }

    private void OnPinnedClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ResultItem item) Launch(item.Item);
    }

    /// <summary>Clic derecho en una app (resultado o anclada): anclarla arriba o quitarla.</summary>
    private void OnItemRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if ((e.OriginalSource as FrameworkElement)?.DataContext is not ResultItem { Item.Kind: LaunchKind.Application } result) return;
        var pinned = _settings.PinnedApps.Contains(result.Item.Key, StringComparer.OrdinalIgnoreCase);
        if (!pinned && _settings.PinnedApps.Count >= MaxPinned) return;

        var option = new MenuFlyoutItem { Text = pinned ? Strings.Current.UnpinApp : Strings.Current.PinApp };
        option.Click += (_, _) => SetPinned(result.Item, !pinned);
        var menu = new MenuFlyout { Items = { option } };
        menu.Opened += (_, _) => _menuOpen = true;
        menu.Closed += (_, _) =>
        {
            _menuOpen = false;
            Native.SetForegroundWindow(_hwnd);
            SearchBox.Focus(FocusState.Programmatic);
        };
        menu.ShowAt((UIElement)sender, e.GetPosition((UIElement)sender));
        e.Handled = true;
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => UpdateResults();

    private void UpdateResults()
    {
        var empty = SearchBox.Text.Trim().Length == 0;
        // "> comando": un único resultado que lo ejecuta en la consola.
        var command = ConsoleCommand.Parse(SearchBox.Text);
        // ":nombre": emojis y símbolos para copiar.
        var emoji = command is null ? EmojiCatalog.Parse(SearchBox.Text) : null;
        // Con la búsqueda vacía, las últimas apps abiertas (si está activado en los ajustes).
        IReadOnlyList<LaunchItem> ranked = command is not null
            ? command.Length > 0 ? [ConsoleCommand.Item(command)] : []
            : emoji is not null ? EmojiCatalog.Find(emoji)
            : empty ? RecentItems() : Search.Rank(SearchBox.Text, _index.Items, _settings.Launches);
        _panel.UseGrid(emoji is not null, _results);
        _results.Clear();
        foreach (var item in ranked) _results.Add(new ResultItem(item));

        ActiveList.SelectedIndex = _results.Count > 0 ? 0 : -1;
        if (_results.Count > 0) ActiveList.ScrollIntoView(_results[0]);
        _panel.Empty.Text = command is not null ? Strings.Current.ConsoleHint : Strings.Current.NoResults;
        _panel.Empty.Visibility = _results.Count == 0 && SearchBox.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        // Sin nada escrito, solo ancladas y búsqueda (o las recientes, si hay y están activadas).
        if (empty && _results.Count == 0) _panel.Hide();
        else _panel.ShowAt(_panelBounds);

        foreach (var result in _results) _ = result.LoadIconAsync();
    }

    /// <summary>Las flechas mueven la selección sin sacar el foco de la búsqueda.</summary>
    private void OnSearchKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            // En la cuadrícula de emojis, ↑ ↓ saltan una fila y ← → una casilla.
            case VirtualKey.Down:
                MoveSelection(IsGrid ? GridColumns : +1);
                break;
            case VirtualKey.Up:
                MoveSelection(IsGrid ? -GridColumns : -1);
                break;
            case VirtualKey.Right when IsGrid && _results.Count > 0:
                MoveSelection(+1);
                break;
            case VirtualKey.Left when IsGrid && _results.Count > 0:
                MoveSelection(-1);
                break;
            case VirtualKey.Enter:
                // Con la lista oculta, Enter no abre nada que no se vea.
                if (_results.Count > 0 && ActiveList.SelectedItem is ResultItem selected) Launch(selected.Item, LaunchModeFromKeys());
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
        var index = Math.Clamp(ActiveList.SelectedIndex + delta, 0, _results.Count - 1);
        ActiveList.SelectedIndex = index;
        ActiveList.ScrollIntoView(_results[index]);
    }

    private void OnResultClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ResultItem result) Launch(result.Item);
    }

    /// <summary>Con el atajo de los ajustes (Ctrl+Enter por defecto), como administrador.</summary>
    private LaunchMode LaunchModeFromKeys() => AdminShortcut.IsHeld(_settings.AdminShortcut) ? LaunchMode.RunAsAdmin : LaunchMode.Open;

    private void Launch(LaunchItem item, LaunchMode mode = LaunchMode.Open)
    {
        HideLauncher();

        // Las apps de Microsoft Store no se elevan así: se abren sin más.
        var packaged = item.Path?.StartsWith("shell:", StringComparison.OrdinalIgnoreCase) == true;

        if (item.Kind == LaunchKind.Application)
        {
            _settings.Launches[item.Key] = _settings.Launches.GetValueOrDefault(item.Key) + 1;
            _settings.RecentApps.RemoveAll(k => k.Equals(item.Key, StringComparison.OrdinalIgnoreCase));
            _settings.RecentApps.Insert(0, item.Key);
            if (_settings.RecentApps.Count > RecentCount) _settings.RecentApps.RemoveRange(RecentCount, _settings.RecentApps.Count - RecentCount);
            // Las apps desinstaladas dejan de contar: el archivo de ajustes no crece sin fin.
            foreach (var key in _settings.Launches.Keys.Where(k => !_index.Contains(k)).ToList()) _settings.Launches.Remove(key);
            _settings.RecentApps.RemoveAll(k => !_index.Contains(k));
            _store.Save(_settings);
        }

        if (item.Run is { } run)
        {
            run();
            return;
        }

        if (item.Kind == LaunchKind.Emoji)
        {
            EmojiCatalog.Copy(item.Glyph!);
            return;
        }

        if (item.Kind == LaunchKind.Console)
        {
            ConsoleCommand.Run(item.Name, mode == LaunchMode.RunAsAdmin);
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
    // Píxeles de los iconos leídos de antemano, fuera del hilo de la interfaz (ver Preload).
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (byte[] Pixels, int Width, int Height)?> Pixels = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Lee de antemano los iconos de las apps que aún no tiene, uno tras otro en segundo plano.</summary>
    internal static void Preload(IReadOnlyList<LaunchItem> apps)
    {
        foreach (var app in apps)
        {
            if (app.Path is { } path && !Pixels.ContainsKey(app.Key)) Pixels[app.Key] = ShellIcon.Load(path, 64);
        }
    }

    internal ResultItem(LaunchItem item) => Item = item;

    public event PropertyChangedEventHandler? PropertyChanged;

    internal LaunchItem Item { get; }

    public string Name => Item.Name;

    public string KindLabel => Item.KindLabel;

    public string Glyph => Item.Glyph ?? "";

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
        if (Item.Kind == LaunchKind.Emoji) return; // Se dibuja el carácter.

        if (Item.Kind == LaunchKind.Command)
        {
            Icon = _appLogo ??= new BitmapImage(new Uri(App.LogoPath));
            return;
        }

        // Todos los comandos de consola comparten el icono de la consola: se guarda por su ruta.
        var cacheKey = Item.Kind == LaunchKind.Console ? Item.Path! : Item.Key;
        if (IconCache.TryGetValue(cacheKey, out var cached))
        {
            Icon = cached;
            return;
        }

        // Se pide al Shell fuera del hilo de la interfaz; 64 px se ven nítidos hasta el 225 % de escala.
        var path = Item.Path!;
        var icon = Pixels.TryGetValue(cacheKey, out var ready) ? ready : await Task.Run(() => ShellIcon.Load(path, 64));
        if (icon is not { } loaded) return;

        var image = new WriteableBitmap(loaded.Width, loaded.Height);
        using (var stream = image.PixelBuffer.AsStream()) stream.Write(loaded.Pixels);
        image.Invalidate();
        IconCache[cacheKey] = image;
        Icon = image;
    }
}
