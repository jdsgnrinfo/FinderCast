using FinderCast.Themes;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FinderCast.Controls;

/// <summary>
/// El atajo como teclas sueltas, igual que la lista de atajos de ShutterGrid: 20 px de alto,
/// 5 px a cada lado del texto (9 pt Medium), esquinas de 4 px, el relleno y el borde de los
/// botones y 4 px entre teclas. Al pasar por encima aparece detrás un cuadro suave de 6 px de
/// esquina en 150 ms (como el chevron de "Atajos"); un clic pasa a grabar, y mientras graba se
/// ve un botón levantado con el aviso.
/// </summary>
internal sealed partial class ShortcutPicker : PressTarget
{
    private const double KeyHeight = 20;
    private const double KeyPad = 5;
    private const double HoverPad = 4;
    private const double ButtonHeight = 28;

    private readonly Border _hoverBox = new() { CornerRadius = new CornerRadius(6) };
    private readonly StackPanel _keys = new() { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
    // El aviso va en un botón de contorno levantado, como "Cambiar" en ShutterGrid.
    private readonly Outline _prompt = new()
    {
        Radius = 8,
        Height = ButtonHeight,
        VerticalAlignment = VerticalAlignment.Center,
        Visibility = Visibility.Collapsed,
    };
    private readonly TextBlock _promptText = new()
    {
        FontFamily = new FontFamily("Segoe UI Variable Text"),
        FontSize = 9.5 * 4 / 3.0,
        FontWeight = FontWeights.Medium,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(10, 0, 10, 0),
    };
    private readonly Fade _hover = new(0);
    private readonly FrameLoop _loop;
    private IReadOnlyList<string> _parts = [];
    private Palette _palette = Palette.Light;

    public ShortcutPicker()
    {
        _loop = new FrameLoop(_ => Render());
        VerticalAlignment = VerticalAlignment.Center;
        // El cuadro del hover sobresale 4 px: las teclas quedan alineadas con el margen.
        Margin = new Thickness(0, 0, -HoverPad, 0);
        _keys.Margin = new Thickness(HoverPad);
        _prompt.Children.Add(_promptText);
        Children.Add(_hoverBox);
        Children.Add(_keys);
        Children.Add(_prompt);

        StateChanged += () =>
        {
            _hover.To(IsHovered && !IsPrompting ? 1 : 0, 0.15);
            _loop.Start();
        };
        ActualThemeChanged += (_, _) => ApplyTheme();
        Loaded += (_, _) => ApplyTheme();
    }

    public bool IsPrompting => _prompt.Visibility == Visibility.Visible;

    public void ShowKeys(IReadOnlyList<string> parts)
    {
        _parts = parts;
        _prompt.Visibility = Visibility.Collapsed;
        _keys.Visibility = Visibility.Visible;
        BuildKeys();
    }

    /// <summary>En lugar de las teclas, un aviso ("Pulsa el atajo…", "Ya está en uso").</summary>
    public void ShowPrompt(string text)
    {
        _promptText.Text = text;
        _keys.Visibility = Visibility.Collapsed;
        _prompt.Visibility = Visibility.Visible;
        _prompt.Margin = new Thickness(0, 0, HoverPad, 0);
        _hover.Jump(0);
        Render();
    }

    private void ApplyTheme()
    {
        _palette = Palette.For(ActualTheme == ElementTheme.Dark);
        _prompt.Fill = new SolidColorBrush(_palette.ButtonFillLift);
        _prompt.Stroke = new SolidColorBrush(_palette.ButtonBorderLift);
        _promptText.Foreground = new SolidColorBrush(_palette.Secondary);
        BuildKeys();
        Render();
    }

    private void BuildKeys()
    {
        _keys.Children.Clear();
        foreach (var part in _parts)
        {
            // Mismo contorno que los botones, con esquinas de 4 px y sin levantar.
            var key = new Outline
            {
                Radius = 4,
                Height = KeyHeight,
                MinWidth = KeyHeight,
                Fill = new SolidColorBrush(_palette.Key),
                Stroke = new SolidColorBrush(_palette.KeyBorder),
            };
            key.Children.Add(new TextBlock
            {
                Text = part,
                FontFamily = new FontFamily("Segoe UI Variable Text"),
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(_palette.OnKey),
                Margin = new Thickness(KeyPad, 0, KeyPad, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            });
            _keys.Children.Add(key);
        }
    }

    private bool Render()
    {
        var now = MotionClock.Now;
        _hoverBox.Background = new SolidColorBrush(Palette.WithAlpha(_palette.Hover, _hover.Value(now)));
        return !_hover.Done(now);
    }
}
