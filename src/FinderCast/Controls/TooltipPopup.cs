using System.Numerics;
using FinderCast.Interop;
using FinderCast.Themes;
using Microsoft.UI.Composition;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.Graphics;

namespace FinderCast.Controls;

/// <summary>
/// El tooltip del sistema de diseño (7.6), el de shadcn/ui: caja de 28 px de alto, 12 px de
/// margen y esquinas de 6 px, con colores invertidos respecto al tema y una flecha (un cuadrado
/// de 10 px con esquinas de 2 px girado 45°). Aparece 8 px por encima del elemento y centrado
/// en él, a no menos de 4 px del borde de la pantalla, subiendo 4 px en 150 ms (ease-out cubic).
/// No recibe clics ni quita el foco.
/// </summary>
internal sealed class TooltipPopup
{
    private const double BoxHeight = 28;
    private const double PadX = 12;
    private const double Arrow = 10;
    private const double Gap = 8;
    private const double ScreenMargin = 4;
    private const float Rise = 4;
    private static readonly TimeSpan FadeTime = TimeSpan.FromMilliseconds(150);

    private readonly Popup _popup = new() { ShouldConstrainToRootBounds = false, IsHitTestVisible = false };
    private readonly Canvas _root = new() { IsHitTestVisible = false };
    private readonly Border _box = new() { CornerRadius = new CornerRadius(6), Height = BoxHeight, Padding = new Thickness(PadX, 0, PadX, 0) };
    private readonly TextBlock _text = new()
    {
        FontFamily = new FontFamily("Segoe UI Variable Text"),
        FontSize = 12,
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
    };
    private readonly Rectangle _arrow = new()
    {
        Width = Arrow,
        Height = Arrow,
        RadiusX = 2,
        RadiusY = 2,
        RenderTransform = new RotateTransform { Angle = 45, CenterX = Arrow / 2, CenterY = Arrow / 2 },
    };
    private readonly Compositor _compositor = CompositionTarget.GetCompositorForCurrentThread();

    public TooltipPopup()
    {
        _box.Child = _text;
        _root.Children.Add(_arrow);
        _root.Children.Add(_box);
        _popup.Child = _root;
    }

    public bool IsOpen => _popup.IsOpen;

    /// <summary>Lo muestra encima del elemento, centrado en él.</summary>
    public void ShowAbove(FrameworkElement target, IntPtr hwnd, string text)
    {
        HideNow();
        var palette = Palette.For(target.ActualTheme == ElementTheme.Dark);
        _box.Background = new SolidColorBrush(palette.Tooltip);
        _arrow.Fill = new SolidColorBrush(palette.Tooltip);
        _text.Foreground = new SolidColorBrush(palette.OnTooltip);
        _text.Text = text;

        _text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var width = Math.Ceiling(_text.DesiredSize.Width + PadX * 2);
        _box.Width = width;

        // Centrado en el elemento, pero sin acercarse a menos de 4 px del borde de la pantalla.
        var bounds = target.TransformToVisual(null).TransformBounds(new Rect(0, 0, target.ActualWidth, target.ActualHeight));
        var centre = bounds.Left + bounds.Width / 2;
        var (minLeft, maxLeft) = ScreenLimits(hwnd, target.XamlRoot.RasterizationScale, width);
        var left = Math.Clamp(centre - width / 2, minLeft, Math.Max(minLeft, maxLeft));

        // La flecha, justo dentro del borde inferior, bajo el centro del elemento.
        Canvas.SetLeft(_arrow, centre - left - Arrow / 2);
        Canvas.SetTop(_arrow, BoxHeight - 1 - Arrow / 2);

        _popup.XamlRoot = target.XamlRoot;
        _popup.HorizontalOffset = left;
        _popup.VerticalOffset = bounds.Top - Gap - BoxHeight;
        _root.Opacity = 0;
        _root.Translation = new Vector3(0, Rise, 0);
        _popup.IsOpen = true;

        var easing = _compositor.CreateCubicBezierEasingFunction(new Vector2(0.33f, 1), new Vector2(0.68f, 1));
        var fade = _compositor.CreateScalarKeyFrameAnimation();
        fade.Target = "Opacity";
        fade.InsertKeyFrame(0, 0);
        fade.InsertKeyFrame(1, 1, easing);
        fade.Duration = FadeTime;
        var rise = _compositor.CreateVector3KeyFrameAnimation();
        rise.Target = "Translation";
        rise.InsertKeyFrame(0, new Vector3(0, Rise, 0));
        rise.InsertKeyFrame(1, Vector3.Zero, easing);
        rise.Duration = FadeTime;
        _root.StartAnimation(fade);
        _root.StartAnimation(rise);
    }

    public void HideNow() => _popup.IsOpen = false;

    /// <summary>Límites de la posición izquierda (en la ventana) para quedar a 4 px de los bordes de la pantalla.</summary>
    private static (double Min, double Max) ScreenLimits(IntPtr hwnd, double scale, double width)
    {
        var origin = new Native.POINT();
        Native.ClientToScreen(hwnd, ref origin);
        var area = DisplayArea.GetFromPoint(new PointInt32(origin.X, origin.Y), DisplayAreaFallback.Nearest).WorkArea;
        var min = (area.X - origin.X) / scale + ScreenMargin;
        var max = (area.X + area.Width - origin.X) / scale - ScreenMargin - width;
        return (min, max);
    }
}
