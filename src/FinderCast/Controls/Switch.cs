using FinderCast.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace FinderCast.Controls;

/// <summary>
/// El interruptor de shadcn/ui, como en ShutterGrid: pista de 32 × 18 con una bola de 16 px a
/// 1 px del borde. La pista pasa del gris al color de "activado" en 240 ms (y la bola de clara a
/// oscura en el tema oscuro) mientras la bola se desliza con un muelle críticamente amortiguado,
/// se estira hasta un 16 % con la velocidad y se ensancha 4 px mientras se mantiene pulsada.
/// La zona de clic sobresale 6 px a los lados y 8 px arriba y abajo.
/// </summary>
internal sealed partial class Switch : Grid
{
    private const double TrackWidth = 32;
    private const double TrackHeight = 18;
    private const double KnobSize = 16;
    private const double Inset = 1;
    private const double HitX = 6;
    private const double HitY = 8;

    private const double Omega = 19; // Frecuencia del muelle: llega en unos 0,4 s sin pasarse.
    private const double PeakSpeed = Omega / Math.E; // Velocidad máxima del muelle, en recorridos/s.
    private const double MaxStretch = 0.16 * KnobSize;
    private const double HeldWidening = 4;

    private readonly Border _track = new() { Width = TrackWidth, Height = TrackHeight, CornerRadius = new CornerRadius(TrackHeight / 2) };
    private readonly Border _shadow = new() { Height = KnobSize, CornerRadius = new CornerRadius(KnobSize / 2) };
    private readonly Border _knob = new() { Height = KnobSize, CornerRadius = new CornerRadius(KnobSize / 2) };
    private readonly TranslateTransform _knobMove = new();
    private readonly TranslateTransform _shadowMove = new();
    private readonly Fade _colour = new(0);
    private readonly FrameLoop _loop;
    private Spring _position = new(0);
    private double _widening;
    private bool _hover;
    private bool _pressed;
    private Palette _palette = Palette.Light;

    public Switch()
    {
        _loop = new FrameLoop(Step);
        Width = TrackWidth + HitX * 2;
        Height = TrackHeight + HitY * 2;
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent); // Recibe clics en toda la zona.

        // Sin redondeo a píxeles: la bola se mueve en fracciones de píxel, suave.
        var canvas = new Canvas
        {
            Width = TrackWidth,
            Height = TrackHeight,
            UseLayoutRounding = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _shadow.RenderTransform = _shadowMove;
        _knob.RenderTransform = _knobMove;
        canvas.Children.Add(_track);
        canvas.Children.Add(_shadow);
        canvas.Children.Add(_knob);
        Children.Add(canvas);

        PointerEntered += (_, _) => SetHover(true);
        PointerExited += (_, _) => SetHover(false);
        PointerCanceled += (_, _) => Release();
        PointerCaptureLost += (_, _) => Release();
        PointerPressed += OnPressed;
        PointerReleased += OnReleased;
        ActualThemeChanged += (_, _) => ApplyTheme();
        Loaded += (_, _) => ApplyTheme();
    }

    /// <summary>El usuario lo ha cambiado (no se lanza con <see cref="Jump"/>).</summary>
    public event Action<bool>? Toggled;

    public bool IsOn { get; private set; }

    /// <summary>Pone el estado sin animar, p. ej. al leerlo del registro.</summary>
    public void Jump(bool on)
    {
        IsOn = on;
        _position = new Spring(on ? 1 : 0);
        _colour.Jump(on ? 1 : 0);
        Render();
    }

    /// <summary>Cambia el estado con su animación.</summary>
    public void Set(bool on)
    {
        IsOn = on;
        _position.Target = on ? 1 : 0;
        _colour.To(on ? 1 : 0, 0.24);
        _loop.Start();
    }

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _pressed = true;
        CapturePointer(e.Pointer);
        _loop.Start();
        e.Handled = true;
    }

    private void OnReleased(object sender, PointerRoutedEventArgs e)
    {
        var wasPressed = _pressed;
        Release();
        ReleasePointerCapture(e.Pointer);
        if (!wasPressed || !_hover) return;

        Set(!IsOn);
        Toggled?.Invoke(IsOn);
        e.Handled = true;
    }

    private void Release()
    {
        _pressed = false;
        _loop.Start();
    }

    private void SetHover(bool hover)
    {
        _hover = hover;
        _loop.Start();
    }

    private void ApplyTheme()
    {
        _palette = Palette.For(ActualTheme == ElementTheme.Dark);
        Render();
    }

    private bool Step(double dt)
    {
        var target = IsOn ? 1.0 : 0.0;
        _position.Target = target;
        _position.Step(dt, Omega, damping: 1, epsilon: 0.0005, rest: 0.01);

        // Pulsada: se ensancha rápido; al soltar, vuelve a ser redonda.
        var widenTarget = _pressed && _hover ? HeldWidening : 0;
        _widening += (widenTarget - _widening) * Math.Min(1, dt * 18);
        if (Math.Abs(widenTarget - _widening) < 0.02) _widening = widenTarget;

        Render();
        return !_position.Settled || _widening != widenTarget || !_colour.Done(MotionClock.Now);
    }

    private void Render()
    {
        var colour = _colour.Value(MotionClock.Now);
        var stretch = MaxStretch * Math.Min(1, Math.Abs(_position.Velocity) / PeakSpeed);
        var width = KnobSize + _widening + stretch;
        var travel = TrackWidth - Inset * 2 - width;
        var x = Inset + travel * _position.Value;

        _track.Background = new SolidColorBrush(Palette.Blend(_palette.Track, _palette.TrackOn, colour));
        _knob.Background = new SolidColorBrush(Palette.Blend(_palette.Knob, _palette.KnobOn, colour));
        _shadow.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(_palette.KnobShadow, 0, 0, 0));

        _knob.Width = _shadow.Width = width;
        _knobMove.X = _shadowMove.X = x;
        _knobMove.Y = Inset;
        _shadowMove.Y = Inset + 0.5; // Sombra de contacto medio píxel por debajo.
    }
}
