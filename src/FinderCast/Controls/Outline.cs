using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FinderCast.Controls;

/// <summary>
/// El contorno de los botones y las teclas, construido como DrawOutline en ShutterGrid: el
/// trazado es el rectángulo metido medio píxel físico hacia dentro, con radio R·escala − 0,5;
/// primero se rellena ese trazado y encima se dibuja un trazo de 1 píxel físico sobre él. Así el
/// relleno cubre justo la mitad del píxel del borde, y el trazo mide 1 píxel a cualquier escala.
/// El contenido se añade como hijo, por encima de las dos capas.
/// </summary>
internal sealed partial class Outline : Grid
{
    public static readonly DependencyProperty FillProperty =
        DependencyProperty.Register(nameof(Fill), typeof(Brush), typeof(Outline), new PropertyMetadata(null, (d, e) => ((Outline)d)._fill.Background = (Brush)e.NewValue));

    public static readonly DependencyProperty StrokeProperty =
        DependencyProperty.Register(nameof(Stroke), typeof(Brush), typeof(Outline), new PropertyMetadata(null, (d, e) => ((Outline)d)._stroke.BorderBrush = (Brush)e.NewValue));

    public static readonly DependencyProperty RadiusProperty =
        DependencyProperty.Register(nameof(Radius), typeof(double), typeof(Outline), new PropertyMetadata(8.0, (d, _) => ((Outline)d).UpdateGeometry()));

    // Sin redondeo a píxeles enteros: el medio píxel del trazado tiene que respetarse.
    private readonly Border _fill = new() { UseLayoutRounding = false };
    private readonly Border _stroke = new() { UseLayoutRounding = false };

    public Outline()
    {
        Children.Add(_fill);
        Children.Add(_stroke);
        Loaded += (_, _) =>
        {
            UpdateGeometry();
            XamlRoot.Changed += (_, _) => UpdateGeometry();
        };
    }

    public Brush? Fill
    {
        get => (Brush?)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public Brush? Stroke
    {
        get => (Brush?)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    /// <summary>Radio de las esquinas en píxeles lógicos (8 en los botones, 4 en las teclas).</summary>
    public double Radius
    {
        get => (double)GetValue(RadiusProperty);
        set => SetValue(RadiusProperty, value);
    }

    private void UpdateGeometry()
    {
        var scale = XamlRoot?.RasterizationScale ?? 1;
        var pixel = 1 / scale;

        // Relleno: el trazado, medio píxel hacia dentro, con radio R·escala − 0,5 (en píxeles).
        _fill.Margin = new Thickness(pixel / 2);
        _fill.CornerRadius = new CornerRadius(Math.Max(0, Radius - pixel / 2));

        // Trazo de 1 píxel centrado en el trazado: su borde exterior es el del rectángulo, con radio R.
        _stroke.BorderThickness = new Thickness(pixel);
        _stroke.CornerRadius = new CornerRadius(Radius);
    }
}
