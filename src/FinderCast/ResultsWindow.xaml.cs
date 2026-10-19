using FinderCast.Interop;
using FinderCast.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using WinRT.Interop;

namespace FinderCast;

/// <summary>
/// La tarjeta de resultados del lanzador: una ventana propia sobre Acrylic, con sus esquinas y su
/// sombra, que va pegada bajo la de búsqueda. Pertenece a ella (se queda siempre encima y se
/// oculta a la vez) y no se activa al hacer clic, para que el teclado siga en la búsqueda.
/// </summary>
public sealed partial class ResultsWindow : Window
{
    private bool _allowClose;

    internal ResultsWindow(IntPtr owner)
    {
        InitializeComponent();

        Hwnd = WindowNative.GetWindowHandle(this);
        AppWindow.Title = App.ProductName;
        AppWindow.SetIcon(App.IconPath);
        AppWindow.IsShownInSwitchers = false;

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = true;
            presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
        }
        WindowChrome.PreventMaximize(this);
        SystemBackdrop = new Controls.ActiveAcrylic();
        WindowChrome.UseNativeBorderWithoutTitleBar(this);

        // El mismo borde en las dos tarjetas, tenga el foco la que lo tenga.
        if (Content is FrameworkElement content)
        {
            WindowChrome.UseFixedBorder(Hwnd, content.ActualTheme == ElementTheme.Dark);
            content.ActualThemeChanged += (_, _) => WindowChrome.UseFixedBorder(Hwnd, content.ActualTheme == ElementTheme.Dark);
        }

        Native.SetWindowLongPtr(Hwnd, Native.GWLP_HWNDPARENT, owner);
        var style = (long)Native.GetWindowLongPtr(Hwnd, Native.GWL_EXSTYLE);
        Native.SetWindowLongPtr(Hwnd, Native.GWL_EXSTYLE, (IntPtr)(style | Native.WS_EX_NOACTIVATE));

        AppWindow.Closing += (_, args) =>
        {
            if (_allowClose) return;
            args.Cancel = true;
            AppWindow.Hide();
        };

        ApplyStrings();
        Strings.Changed += ApplyStrings;
    }

    internal IntPtr Hwnd { get; }

    /// <summary>Dibuja en el pie las teclas del atajo de administrador.</summary>
    internal void ShowAdminShortcut(HotKeyModifiers modifiers)
    {
        AdminKeys.Children.Clear();
        foreach (var key in AdminShortcut.Keys(modifiers))
        {
            var cap = new Controls.Outline
            {
                Radius = 6,
                Height = 28,
                MinWidth = 28,
                Fill = (Brush)Application.Current.Resources["FcKeyFillBrush"],
                Stroke = (Brush)Application.Current.Resources["FcKeyBorderBrush"],
            };
            cap.Children.Add(new TextBlock
            {
                Text = key,
                Margin = new Thickness(8, 0, 8, 0),
                Style = (Style)Application.Current.Resources["FcKeyCapTextStyle"],
            });
            AdminKeys.Children.Add(cap);
        }
    }

    /// <summary>
    /// Con emojis el pie solo muestra «Copiar [Enter]»: no hay nada que abrir como administrador.
    /// </summary>
    internal void ShowCopyAction(bool copy)
    {
        var admin = copy ? Visibility.Collapsed : Visibility.Visible;
        AdminActionText.Visibility = admin;
        AdminKeys.Visibility = admin;
        FooterGap.Visibility = admin;
        SelectedNameText.Visibility = copy ? Visibility.Visible : Visibility.Collapsed;
        OpenActionText.Text = copy ? Strings.Current.CopyAction : Strings.Current.OpenAction;
    }

    /// <summary>Nombre del emoji elegido, en el pie.</summary>
    internal void ShowSelectedName(string name) => SelectedNameText.Text = name;

    /// <summary>La lista de apps o la cuadrícula de emojis: la que se ve recibe los resultados.</summary>
    internal void UseGrid(bool grid, object items)
    {
        if (grid == IsGrid && (grid ? EmojiGrid : (ListViewBase)Results).ItemsSource == items) return;
        IsGrid = grid;
        Results.ItemsSource = grid ? null : items;
        EmojiGrid.ItemsSource = grid ? items : null;
        Results.Visibility = grid ? Visibility.Collapsed : Visibility.Visible;
        EmojiGrid.Visibility = grid ? Visibility.Visible : Visibility.Collapsed;
    }

    internal bool IsGrid { get; private set; }

    /// <summary>Casillas por fila de la cuadrícula, para mover la selección con ↑ y ↓.</summary>
    internal int GridColumns => Math.Max(1, (int)((EmojiGrid.ActualWidth - EmojiGrid.Padding.Left - EmojiGrid.Padding.Right) / 64));

    internal ListViewBase ActiveList => IsGrid ? EmojiGrid : Results;

    internal ListViewBase List => Results;

    internal ListViewBase Grid => EmojiGrid;

    internal TextBlock Empty => EmptyText;

    /// <summary>Se muestra en esa posición (en píxeles físicos) sin quitarle la activación a nadie.</summary>
    internal void ShowAt(RectInt32 bounds)
    {
        AppWindow.MoveAndResize(bounds);
        AppWindow.Show(activateWindow: false);
    }

    internal void Hide()
    {
        if (AppWindow.IsVisible) AppWindow.Hide();
    }

    internal void CloseForExit()
    {
        _allowClose = true;
        Close();
    }

    private void ApplyStrings()
    {
        var text = Strings.Current;
        EmptyText.Text = text.NoResults;
        OpenActionText.Text = text.OpenAction;
        AdminActionText.Text = text.RunAsAdminAction;
    }
}
