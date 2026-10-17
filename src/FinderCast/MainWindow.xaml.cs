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
/// El lanzador: ventana flotante sin bordes sobre Acrylic que aparece con el atajo global en la
/// pantalla del puntero, busca mientras se escribe y se oculta al abrir algo, con Esc o al
/// perder el foco. Se maneja entera con el teclado (flechas, Enter, Esc).
/// </summary>
public sealed partial class MainWindow : Window
{
    private const double WidthDip = 640;
    private const double HeightDip = 420;

    private readonly AppIndex _index;
    private readonly AppSettings _settings;
    private readonly SettingsStore _store;
    private readonly ObservableCollection<ResultItem> _results = [];
    private readonly IntPtr _hwnd;
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
        // No se expande nunca, tampoco con doble clic en el pie.
        WindowChrome.PreventMaximize(this);

        SystemBackdrop = new DesktopAcrylicBackdrop();
        // Sin barra de título, con el borde, las esquinas y la sombra originales de Windows 11. No
        // se usa ExtendsContentIntoTitleBar: WinUI deja entonces una línea oscura en el borde superior.
        WindowChrome.UseNativeBorderWithoutTitleBar(this);
        // El pie arrastra la ventana.
        Footer.SizeChanged += (_, _) => UpdateRegions();

        AppWindow.Closing += (_, args) =>
        {
            if (_allowClose) return;
            args.Cancel = true; // Alt+F4 solo la oculta.
            HideLauncher();
        };
        Activated += (_, args) =>
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated) HideLauncher();
        };

        ApplyStrings();
        Strings.Changed += ApplyStrings;
    }

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
        var height = (int)Math.Round(HeightDip * scale);
        AppWindow.MoveAndResize(new RectInt32(
            area.X + (area.Width - width) / 2,
            area.Y + (int)((area.Height - height) * 0.3),
            width, height));

        _index.RefreshIfStale();
        SearchBox.Text = "";
        UpdateResults();

        Activate();
        Native.SetForegroundWindow(_hwnd);
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, UpdateRegions);
        SearchBox.Focus(FocusState.Programmatic);
    }

    public void HideLauncher()
    {
        if (IsShown) AppWindow.Hide();
    }

    public void CloseForExit()
    {
        _allowClose = true;
        Close();
    }

    private void UpdateRegions() => WindowChrome.SetRegions(this, Footer);

    private void ApplyStrings()
    {
        var text = Strings.Current;
        SearchBox.PlaceholderText = text.SearchPlaceholder;
        EmptyText.Text = text.NoResults;
        OpenActionText.Text = text.OpenAction;
        AdminActionText.Text = text.RunAsAdminAction;
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
                if (Results.SelectedItem is ResultItem selected) Launch(selected.Item, LaunchModeFromKeys());
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

    /// <summary>Ctrl+Enter abre la app como administrador.</summary>
    private static LaunchMode LaunchModeFromKeys()
        => Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down) ? LaunchMode.RunAsAdmin : LaunchMode.Open;

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
