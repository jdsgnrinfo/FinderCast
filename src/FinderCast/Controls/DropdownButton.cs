using System.Numerics;
using FinderCast.Themes;
using Microsoft.UI.Composition;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace FinderCast.Controls;

/// <summary>
/// El disparador del desplegable de Arc UI, compactado como en ShutterGrid: 28 px de alto,
/// esquinas de 8 px, 10 px a cada lado, la etiqueta en 9,5 pt Medium, 6 px y un chevron de
/// Lucide de 15 px que se da la vuelta mientras el menú está abierto. Al pasar por encima (o con
/// el menú abierto) el relleno se aclara y el borde se refuerza en 160 ms.
/// </summary>
internal sealed partial class DropdownButton : PressTarget
{
    private const double ButtonHeight = 28;
    private const double Inset = 10;
    private const double Gap = 6;
    private const double ChevronSize = 15;

    private readonly Outline _outline = new() { Radius = 8 };
    private readonly SolidColorBrush _fill = new();
    private readonly SolidColorBrush _stroke = new();
    private readonly TextBlock _label = new()
    {
        FontFamily = new FontFamily("Segoe UI Variable Text"),
        FontSize = 9.5 * 4 / 3.0,
        FontWeight = FontWeights.Medium,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly Polyline _chevron = Chevron.Create(ChevronSize, strokeWidth: 1.8 * ChevronSize / 24); // 1,8 en la rejilla de 24.
    private readonly Fade _hover = new(0);
    private readonly Fade _open = new(0);
    private readonly Visual _chevronVisual;
    private readonly FrameLoop _loop;
    private Palette _palette = Palette.Light;

    public DropdownButton()
    {
        _loop = new FrameLoop(_ => Render());
        Height = ButtonHeight;
        VerticalAlignment = VerticalAlignment.Center;

        // El chevron gira en el compositor (300 ms, curva estándar), fuera del hilo de la interfaz.
        _chevronVisual = ElementCompositionPreview.GetElementVisual(_chevron);
        _chevronVisual.CenterPoint = new Vector3((float)ChevronSize / 2, (float)ChevronSize / 2, 0);
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Gap,
            Padding = new Thickness(Inset, 0, Inset, 0),
            Children = { _label, _chevron },
        };
        _chevron.VerticalAlignment = VerticalAlignment.Center;
        _outline.Fill = _fill;
        _outline.Stroke = _stroke;
        _outline.Children.Add(content);
        Children.Add(_outline);

        StateChanged += () =>
        {
            _hover.To(IsHovered ? 1 : 0, 0.16);
            _loop.Start();
        };
        ActualThemeChanged += (_, _) => ApplyTheme();
        Loaded += (_, _) => ApplyTheme();
    }

    public string Text
    {
        get => _label.Text;
        set => _label.Text = value;
    }

    /// <summary>Con el menú abierto el botón queda levantado y el chevron mira hacia arriba (300 ms).</summary>
    public bool IsOpen
    {
        get => _open.Target > 0;
        set
        {
            _open.To(value ? 1 : 0, 0.3);
            _loop.Start();

            var compositor = _chevronVisual.Compositor;
            var turn = compositor.CreateScalarKeyFrameAnimation();
            turn.InsertExpressionKeyFrame(0, "this.StartingValue");
            turn.InsertKeyFrame(1, value ? 180 : 0, compositor.CreateCubicBezierEasingFunction(new Vector2(0.22f, 1), new Vector2(0.36f, 1)));
            turn.Duration = TimeSpan.FromMilliseconds(300);
            _chevronVisual.StartAnimation("RotationAngleInDegrees", turn);
        }
    }

    private void ApplyTheme()
    {
        _palette = Palette.For(ActualTheme == ElementTheme.Dark);
        _label.Foreground = new SolidColorBrush(_palette.Text);
        _chevron.Stroke = new SolidColorBrush(_palette.Chevron);
        Render();
    }

    private bool Render()
    {
        var now = MotionClock.Now;
        var lift = Math.Max(_hover.Value(now), _open.Value(now));
        _fill.Color = Palette.Blend(_palette.ButtonFill, _palette.ButtonFillLift, lift);
        _stroke.Color = Palette.Blend(_palette.ButtonBorder, _palette.ButtonBorderLift, lift);
        return !_hover.Done(now) || !_open.Done(now);
    }
}

/// <summary>Iconos de Lucide dibujados como trazos, en su rejilla de 24 px.</summary>
internal static class Chevron
{
    /// <summary>chevron-down: 6,9 → 12,15 → 18,9.</summary>
    public static Polyline Create(double size, double strokeWidth) => Lucide(size, strokeWidth, (6, 9), (12, 15), (18, 9));

    /// <summary>check: 20,6 → 9,17 → 4,12.</summary>
    public static Polyline Check(double size, double strokeWidth) => Lucide(size, strokeWidth, (4, 12), (9, 17), (20, 6));

    private static Polyline Lucide(double size, double strokeWidth, params (double X, double Y)[] points)
    {
        var unit = size / 24;
        var line = new Polyline
        {
            Width = size,
            Height = size,
            StrokeThickness = strokeWidth, // En píxeles.
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
        };
        foreach (var (x, y) in points) line.Points.Add(new Point(x * unit, y * unit));
        return line;
    }
}
