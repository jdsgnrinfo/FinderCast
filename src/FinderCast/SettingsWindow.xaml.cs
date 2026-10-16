using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using FinderCast.Controls;
using FinderCast.Interop;
using FinderCast.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;
using Windows.System;
using Windows.UI;
using WinRT.Interop;
using DispatcherQueueTimer = Microsoft.UI.Dispatching.DispatcherQueueTimer;

namespace FinderCast;

/// <summary>
/// Ventana de ajustes construida como la de ShutterGrid: una barra de título de Windows 11 solo
/// con sus botones, el logo y la versión, y los ajustes separados por líneas finas. El
/// interruptor, las teclas y la información emergente siguen a shadcn/ui; el interruptor con su
/// propio muelle; el menú de idioma es el desplegable de Arc UI. Ancho fijo, Mica en toda la
/// ventana, tema claro u oscuro e idioma elegido. Cerrarla solo la oculta.
/// </summary>
public sealed partial class SettingsWindow : Window
{
    private const double WidthDip = 360;
    private const double RowsTop = 170; // Bajo la barra de título.
    private const double HeightDip = RowsTop + 48 * 4 + 16;

    /// <summary>Página de donación (la misma que en ShutterGrid y SwiftLook).</summary>
    private const string DonationLink = "https://www.paypal.com/paypalme/jotica96";

    private static readonly TimeSpan TooltipDelay = TimeSpan.FromMilliseconds(300);

    /// <summary>Tiempo que se ve el aviso de atajo ocupado antes de volver a mostrar el actual.</summary>
    private static readonly TimeSpan ErrorDuration = TimeSpan.FromSeconds(2.5);

    private readonly AppSettings _settings;
    private readonly SettingsStore _store;
    private readonly Func<HotKey, bool> _changeHotKey;
    private readonly HotKeyRecorder _recorder = new();
    private readonly DropdownMenu _languageMenu = new();
    private readonly TooltipPopup _tooltip = new();
    private readonly DispatcherQueueTimer _tooltipTimer;
    private readonly DispatcherQueueTimer _errorTimer;
    private readonly IntPtr _hwnd;
    private bool _allowClose;

    /// <summary>Se lanza cuando cambia el ajuste del icono de la bandeja.</summary>
    internal event Action? TrayIconSettingChanged;

    internal SettingsWindow(AppSettings settings, SettingsStore store, Func<HotKey, bool> changeHotKey)
    {
        _settings = settings;
        _store = store;
        _changeHotKey = changeHotKey;
        InitializeComponent();

        _hwnd = WindowNative.GetWindowHandle(this);
        AppWindow.Title = App.ProductName; // No se dibuja en la barra, pero sale en la barra de tareas y en Alt+Tab.
        AppWindow.SetIcon(App.IconPath);
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
        }
        WindowChrome.PreventMaximize(this);

        // Ventana nativa, como la de ShutterGrid: Windows dibuja la barra de título (sin nombre ni
        // icono, solo sus botones), el borde, las esquinas y la sombra; Mica cubre toda la ventana.
        SystemBackdrop = new MicaBackdrop();
        WindowChrome.UseMica(_hwnd);
        WindowChrome.HideCaption(_hwnd);
        AppWindow.Closing += (_, args) =>
        {
            if (_allowClose) return;
            args.Cancel = true;
            HideSettings();
        };
        AppWindow.Changed += (_, args) =>
        {
            if (!args.DidPositionChange) return;
            HideTooltip();
            _languageMenu.Dismiss(animate: false);
        };

        Logo.Source = new BitmapImage(new Uri(App.LogoPath));
        RefreshValues();

        StartupSwitch.Toggled += SetStartWithWindows;
        TraySwitch.Toggled += on =>
        {
            _settings.ShowTrayIcon = on;
            _store.Save(_settings);
            TrayIconSettingChanged?.Invoke();
        };

        LanguageButton.Click += ToggleLanguageMenu;
        _languageMenu.Dismissing += () => LanguageButton.IsOpen = false;

        // El logo es un enlace a la donación: mano al pasar, invitación tras un momento y clic.
        LogoLink.ShowsHand = true;
        _tooltipTimer = DispatcherQueue.CreateTimer();
        _tooltipTimer.Interval = TooltipDelay;
        _tooltipTimer.IsRepeating = false;
        _tooltipTimer.Tick += (_, _) =>
        {
            if (AppWindow.IsVisible && LogoLink.IsHovered && !LogoLink.Pressed) _tooltip.ShowAbove(LogoLink, _hwnd, Strings.Current.BuyMeACoffee);
        };
        LogoLink.StateChanged += () =>
        {
            if (LogoLink.IsHovered && !LogoLink.Pressed)
            {
                if (!_tooltip.IsOpen) _tooltipTimer.Start();
            }
            else
            {
                HideTooltip();
            }
        };
        LogoLink.Click += OpenDonationPage;

        ShortcutPicker.Click += StartRecording;
        _recorder.Recorded += hotKey => DispatcherQueue.TryEnqueue(() => FinishRecording(hotKey));
        _recorder.Cancelled += () => DispatcherQueue.TryEnqueue(ShowShortcut);
        _errorTimer = DispatcherQueue.CreateTimer();
        _errorTimer.Interval = ErrorDuration;
        _errorTimer.IsRepeating = false;
        _errorTimer.Tick += (_, _) => ShowShortcut();

        Activated += (_, args) =>
        {
            if (args.WindowActivationState != WindowActivationState.Deactivated) return;

            // Un clic en cualquier otro sitio, incluso en otra app, cierra el menú y la información.
            HideTooltip();
            _languageMenu.Dismiss();

            // Si el usuario se va a otra ventana mientras graba, el atajo se queda como estaba.
            if (_recorder.IsRecording)
            {
                _recorder.Stop();
                ShowShortcut();
            }
        };

        ApplyStrings();
        Strings.Changed += ApplyStrings;
        WindowChrome.UseDarkFrame(_hwnd, Root.ActualTheme == ElementTheme.Dark);
        Root.ActualThemeChanged += (_, _) =>
        {
            WindowChrome.UseDarkFrame(_hwnd, Root.ActualTheme == ElementTheme.Dark);
            HideTooltip();
            _languageMenu.Dismiss(animate: false);
        };
    }

    /// <summary>Muestra la ventana centrada en la pantalla del puntero, a un tercio de la altura.</summary>
    public void ShowCentered()
    {
        if (!AppWindow.IsVisible)
        {
            Native.GetCursorPos(out var cursor);
            var area = DisplayArea.GetFromPoint(new PointInt32(cursor.X, cursor.Y), DisplayAreaFallback.Nearest).WorkArea;
            var scale = Native.ScaleAt(cursor);
            AppWindow.ResizeClient(new SizeInt32((int)Math.Round(WidthDip * scale), (int)Math.Round(HeightDip * scale)));
            var size = AppWindow.Size;
            AppWindow.Move(new PointInt32(area.X + (area.Width - size.Width) / 2, area.Y + (area.Height - size.Height) / 3));

            // El registro pudo cambiar mientras estaba oculta (p. ej. desde el Administrador de tareas).
            RefreshValues();
        }

        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter) presenter.Restore();
        Activate();
        Native.SetForegroundWindow(_hwnd);
    }

    /// <summary>El atajo guardado no se pudo activar al arrancar: lo dice en su fila.</summary>
    internal void ShowShortcutError(HotKey hotKey)
    {
        ShortcutPicker.ShowPrompt(Strings.Current.ShortcutInUse);
        _errorTimer.Start();
        Native.MessageBox(_hwnd, string.Format(Strings.Current.ShortcutFailed, hotKey), App.ProductName, Native.MB_ICONWARNING);
    }

    public void CloseForExit()
    {
        _allowClose = true;
        _recorder.Dispose();
        HideTooltip();
        _languageMenu.Dismiss(animate: false);
        Close();
    }

    private void HideSettings()
    {
        _recorder.Stop();
        HideTooltip();
        _languageMenu.Dismiss(animate: false);
        AppWindow.Hide();
    }

    private void RefreshValues()
    {
        StartupSwitch.Jump(StartupRegistration.IsEnabled);
        TraySwitch.Jump(_settings.ShowTrayIcon);
    }

    // Mientras el menú de idioma está abierto se queda las teclas: flechas, Enter, Esc.
    private void OnRootPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_languageMenu.HandleKey(e.Key))
        {
            e.Handled = true;
            return;
        }

        if (e.Key != VirtualKey.Escape || _recorder.IsRecording) return;
        HideSettings();
        e.Handled = true;
    }

    // Un clic en cualquier otro sitio de la ventana cierra el menú de idioma.
    private void OnRootPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!LanguageButton.IsHovered) _languageMenu.Dismiss();
    }

    private void ToggleLanguageMenu()
    {
        if (_languageMenu.IsOpen)
        {
            _languageMenu.Dismiss();
            return;
        }

        HideTooltip();
        var languages = Strings.All;
        var items = languages.Select(language => (language.Name, language == Strings.Current)).ToList();
        _languageMenu.Open(LanguageButton, _hwnd, items, index =>
        {
            // Todo cambia a la vez: esta ventana, el lanzador y el menú de la bandeja.
            _settings.Language = languages[index].Code;
            _store.Save(_settings);
            Strings.Current = languages[index];
        });
        LanguageButton.IsOpen = true;
    }

    private void HideTooltip()
    {
        _tooltipTimer.Stop();
        _tooltip.HideNow();
    }

    /// <summary>Abre la página de PayPal en el navegador.</summary>
    private static void OpenDonationPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo(DonationLink) { UseShellExecute = true });
        }
        catch (Win32Exception)
        {
            // No hay navegador con el que abrirla; no se puede hacer nada más.
        }
    }

    private void StartRecording()
    {
        if (_recorder.IsRecording) return;
        _errorTimer.Stop();
        _languageMenu.Dismiss();
        ShortcutPicker.ShowPrompt(Strings.Current.PressShortcut);
        _recorder.Start();
    }

    private void FinishRecording(HotKey hotKey)
    {
        if (_changeHotKey(hotKey))
        {
            ShowShortcut();
            return;
        }

        // Otra app ya lo usa: se avisa un momento y se vuelve a mostrar el que sigue activo.
        ShortcutPicker.ShowPrompt(Strings.Current.ShortcutInUse);
        _errorTimer.Start();
    }

    /// <summary>Dibuja el atajo actual como teclas sueltas: [Alt] [Espacio].</summary>
    private void ShowShortcut()
    {
        _errorTimer.Stop();
        ShortcutPicker.ShowKeys(_settings.HotKey.Parts);
    }

    private void SetStartWithWindows(bool enabled)
    {
        try
        {
            StartupRegistration.SetEnabled(enabled);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            StartupSwitch.Set(StartupRegistration.IsEnabled);
            Native.MessageBox(_hwnd, $"{Strings.Current.StartupNotChanged}\n\n{ex.Message}", App.ProductName, Native.MB_ICONWARNING);
        }
    }

    private void ApplyStrings()
    {
        var text = Strings.Current;
        VersionText.Text = string.Format(text.Version, Version);
        StartupLabel.Text = text.StartWithWindows;
        TrayLabel.Text = text.ShowTrayIcon;
        LanguageLabel.Text = text.Language;
        LanguageButton.Text = text.Name;
        ShortcutLabel.Text = text.Shortcut;
        if (!_recorder.IsRecording) ShowShortcut(); // "Espacio" / "Space" cambia con el idioma.
    }

    private static string Version
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 1, 0);
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }
}
