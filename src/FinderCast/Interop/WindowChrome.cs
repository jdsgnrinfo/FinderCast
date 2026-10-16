using System.Runtime.InteropServices;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Foundation;
using Windows.Graphics;

namespace FinderCast.Interop;

/// <summary>Ajustes de ventana que WinUI no resuelve por sí solo.</summary>
internal static class WindowChrome
{
    private const uint WM_SYSCOMMAND = 0x0112;
    private const uint WM_NCLBUTTONDBLCLK = 0x00A3;
    private const int SC_MAXIMIZE = 0xF030;

    // Los delegados se guardan para que el recolector no los libere mientras Windows los usa.
    private static readonly List<SubclassProc> Procedures = [];

    private delegate IntPtr SubclassProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr data);

    /// <summary>
    /// La ventana nunca se maximiza: ni con doble clic en su zona de arrastre, ni con Win+↑, ni
    /// desde el menú de la ventana. WinUI maximiza con doble clic aunque falte el botón.
    /// </summary>
    public static void PreventMaximize(Window window)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        SubclassProc procedure = (h, msg, wParam, lParam, _, _) =>
        {
            if (msg == WM_NCLBUTTONDBLCLK) return IntPtr.Zero;
            if (msg == WM_SYSCOMMAND && ((int)wParam & 0xFFF0) == SC_MAXIMIZE) return IntPtr.Zero;
            return DefSubclassProc(h, msg, wParam, lParam);
        };
        Procedures.Add(procedure);
        SetWindowSubclass(hwnd, procedure, (UIntPtr)1, UIntPtr.Zero);

        // Por si llega por otro camino: se devuelve a su tamaño en cuanto ocurre.
        window.AppWindow.Changed += (sender, args) =>
        {
            if (args.DidPresenterChange || args.DidSizeChange)
            {
                if (sender.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Maximized } presenter) presenter.Restore();
            }
        };
    }

    /// <summary>
    /// Fija las zonas de la barra de título: <paramref name="caption"/> arrastra la ventana y
    /// <paramref name="passthrough"/> (dentro de ella) recibe el ratón como el resto del contenido.
    /// En píxeles físicos, relativos a la ventana.
    /// </summary>
    public static void SetRegions(Window window, FrameworkElement caption, FrameworkElement? passthrough = null)
    {
        if (caption.XamlRoot is null) return;
        var source = InputNonClientPointerSource.GetForWindowId(window.AppWindow.Id);
        // Sin tapar los botones de minimizar, maximizar y cerrar, que tienen sus propias zonas.
        var area = ToPixels(caption);
        var clientWidth = (int)Math.Round(caption.XamlRoot.Size.Width * caption.XamlRoot.RasterizationScale);
        area.Width = Math.Max(0, Math.Min(area.Width, clientWidth - window.AppWindow.TitleBar.RightInset - area.X));
        source.SetRegionRects(NonClientRegionKind.Caption, [area]);
        source.SetRegionRects(NonClientRegionKind.Passthrough, passthrough is null ? [] : [ToPixels(passthrough)]);
    }

    private static RectInt32 ToPixels(FrameworkElement element)
    {
        var scale = element.XamlRoot.RasterizationScale;
        var bounds = element.TransformToVisual(null).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        return new RectInt32(
            (int)Math.Round(bounds.X * scale), (int)Math.Round(bounds.Y * scale),
            (int)Math.Round(bounds.Width * scale), (int)Math.Round(bounds.Height * scale));
    }

    /// <summary>La barra de título nativa sin nombre ni icono: solo los botones de Windows.</summary>
    public static void HideCaption(IntPtr hwnd)
    {
        const int WtaNonClient = 1;
        const uint NoDrawCaption = 0x1, NoDrawIcon = 0x2;
        var options = new ThemeOptions { Flags = NoDrawCaption | NoDrawIcon, Mask = NoDrawCaption | NoDrawIcon };
        SetWindowThemeAttribute(hwnd, WtaNonClient, ref options, Marshal.SizeOf<ThemeOptions>());
    }

    /// <summary>Barra de título y botones nativos en el tema oscuro o en el claro.</summary>
    public static void UseDarkFrame(IntPtr hwnd, bool dark)
    {
        const int DwmwaUseImmersiveDarkMode = 20;
        var value = dark ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref value, sizeof(int));
    }

    /// <summary>
    /// Mica de Windows en toda la ventana, barra de título y bordes incluidos, como en ShutterGrid:
    /// el marco se extiende sobre todo el contenido ("lámina de cristal") y DWM le pone el material.
    /// </summary>
    public static void UseMica(IntPtr hwnd)
    {
        const int DwmwaSystemBackdropType = 38;
        const int DwmsbtMainWindow = 2; // Mica
        var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        DwmExtendFrameIntoClientArea(hwnd, ref margins);
        var backdrop = DwmsbtMainWindow;
        DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref backdrop, sizeof(int));
    }

    /// <summary>
    /// Ventana normal de Windows 11 sin barra de título: el contenido ocupa todo el rectángulo de
    /// la ventana (WM_NCCALCSIZE no deja zona de marco), pero sigue siendo una ventana con marco,
    /// así que Windows le dibuja su borde original, sus esquinas redondeadas y su sombra. Evita la
    /// línea superior que WinUI pinta al extender el contenido sobre la barra.
    /// </summary>
    public static void UseNativeBorderWithoutTitleBar(Window window)
    {
        const uint WmNcCalcSize = 0x0083;
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        SubclassProc procedure = (h, msg, wParam, lParam, _, _) =>
            msg == WmNcCalcSize && wParam != IntPtr.Zero ? IntPtr.Zero : DefSubclassProc(h, msg, wParam, lParam);
        Procedures.Add(procedure);
        SetWindowSubclass(hwnd, procedure, (UIntPtr)2, UIntPtr.Zero);

        const uint SwpNoSize = 0x1, SwpNoMove = 0x2, SwpNoZOrder = 0x4, SwpNoActivate = 0x10, SwpFrameChanged = 0x20;
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SwpNoSize | SwpNoMove | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left, Right, Top, Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

    [StructLayout(LayoutKind.Sequential)]
    private struct ThemeOptions
    {
        public uint Flags;
        public uint Mask;
    }

    [DllImport("uxtheme.dll")]
    private static extern int SetWindowThemeAttribute(IntPtr hwnd, int attribute, ref ThemeOptions options, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("comctl32.dll")]
    private static extern bool SetWindowSubclass(IntPtr hwnd, SubclassProc proc, UIntPtr id, UIntPtr data);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
}
