using FinderCast.Interop;
using FinderCast.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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

    /// <summary>Clic derecho en un resultado: el lanzador ofrece anclarlo.</summary>
    internal event RightTappedEventHandler? ItemRightTapped;

    private void OnRightTapped(object sender, RightTappedRoutedEventArgs e) => ItemRightTapped?.Invoke(sender, e);

    /// <summary>Casillas por fila de la cuadrícula, para mover la selección con ↑ y ↓.</summary>
    internal int GridColumns => EmojiColumns;

    private const int EmojiColumns = 9;
    private ItemsWrapGrid? _emojiPanel;

    private void OnEmojiPanelLoaded(object sender, RoutedEventArgs e)
    {
        _emojiPanel = (ItemsWrapGrid)sender;
        _emojiPanel.SizeChanged += (_, _) => FitEmojiGrid();
        FitEmojiGrid();
    }

    /// <summary>Las nueve casillas de cada fila se reparten todo el ancho de la tarjeta.</summary>
    private void FitEmojiGrid()
    {
        // El ancho del propio panel: la plantilla del GridView le quita algo más que su relleno.
        var width = _emojiPanel?.ActualWidth ?? 0;
        // Un poco menos del reparto exacto: con el redondeo de la escala, 9 × 64 = 576 ya no cabían.
        if (width > 0) _emojiPanel!.ItemWidth = (width - 1) / EmojiColumns;
    }

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
    }
}
