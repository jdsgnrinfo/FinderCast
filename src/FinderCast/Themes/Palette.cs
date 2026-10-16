using System.Diagnostics;
using Windows.UI;

namespace FinderCast.Themes;

/// <summary>
/// Colores de los controles de los ajustes, los mismos de ShutterGrid: paleta neutra de
/// shadcn/ui para el interruptor y las teclas, los botones de Arc UI y los menús de Windows 11.
/// </summary>
internal sealed record Palette(
    bool Dark, Color Text, Color Secondary, Color Hover,
    Color Track, Color TrackOn, Color Knob, Color KnobOn, byte KnobShadow,
    Color ButtonFill, Color ButtonFillLift, Color ButtonBorder, Color ButtonBorderLift, Color Chevron,
    Color Key, Color KeyBorder, Color OnKey,
    Color MenuSurface, Color MenuBorder, Color MenuHighlight,
    Color Tooltip, Color OnTooltip)
{
    public static Palette For(bool dark) => dark ? DarkTheme : Light;

    public static readonly Palette Light = new(
        Dark: false,
        Text: Rgb(0x1A, 0x1A, 0x1A),
        Secondary: Rgb(0x5F, 0x5F, 0x5F),
        Hover: Argb(13, 0, 0, 0),
        Track: Rgb(0xE5, 0xE5, 0xE5),        // input
        TrackOn: Rgb(0x17, 0x17, 0x17),      // primary
        Knob: Rgb(0xFF, 0xFF, 0xFF),         // background
        KnobOn: Rgb(0xFF, 0xFF, 0xFF),
        KnobShadow: 30,
        // Arc UI: surface → surface-muted y border → border-strong al levantarse.
        ButtonFill: Rgb(0xFF, 0xFF, 0xFF),
        ButtonFillLift: Rgb(0xF8, 0xF8, 0xF8),
        ButtonBorder: Rgb(0xE9, 0xE9, 0xE9),
        ButtonBorderLift: Rgb(0xC4, 0xC4, 0xC4),
        Chevron: Rgb(0x7D, 0x7D, 0x7D),
        // Las teclas llevan el relleno y el borde de los botones, sin levantar.
        Key: Rgb(0xFF, 0xFF, 0xFF),
        KeyBorder: Rgb(0xE9, 0xE9, 0xE9),
        OnKey: Rgb(0x5C, 0x5C, 0x5C),
        MenuSurface: Rgb(0xF9, 0xF9, 0xF9),
        MenuBorder: Rgb(0xE9, 0xE9, 0xE9),
        MenuHighlight: Rgb(0xF0, 0xF0, 0xF0), // Negro al 4 % sobre el panel.
        Tooltip: Rgb(0x0A, 0x0A, 0x0A),       // foreground
        OnTooltip: Rgb(0xFA, 0xFA, 0xFA));

    public static readonly Palette DarkTheme = new(
        Dark: true,
        Text: Rgb(0xFF, 0xFF, 0xFF),
        Secondary: Rgb(0xB0, 0xB0, 0xB0),
        Hover: Argb(16, 255, 255, 255),
        Track: Argb(38, 255, 255, 255),      // input: blanco al 15 %
        TrackOn: Rgb(0xE5, 0xE5, 0xE5),      // primary
        Knob: Rgb(0xFA, 0xFA, 0xFA),         // foreground
        KnobOn: Rgb(0x17, 0x17, 0x17),       // primary-foreground
        KnobShadow: 50,
        // Blanco translúcido, el mismo tono que las teclas, para que Mica lo tiña.
        ButtonFill: Argb(20, 255, 255, 255),
        ButtonFillLift: Argb(32, 255, 255, 255),
        ButtonBorder: Argb(24, 255, 255, 255),
        ButtonBorderLift: Argb(48, 255, 255, 255),
        Chevron: Rgb(0x8C, 0x8C, 0x8C),
        Key: Argb(20, 255, 255, 255),
        KeyBorder: Argb(24, 255, 255, 255),
        OnKey: Rgb(0xB5, 0xB5, 0xB5),
        MenuSurface: Rgb(0x2C, 0x2C, 0x2C),
        MenuBorder: Rgb(0x3A, 0x3A, 0x3A),
        MenuHighlight: Rgb(0x38, 0x38, 0x38), // Blanco al 6 % sobre el panel.
        Tooltip: Rgb(0xFA, 0xFA, 0xFA),
        OnTooltip: Rgb(0x17, 0x17, 0x17));

    public static Color Blend(Color from, Color to, double t)
    {
        t = Math.Clamp(t, 0, 1);
        byte Mix(byte a, byte b) => (byte)Math.Round(a + (b - a) * t);
        return Color.FromArgb(Mix(from.A, to.A), Mix(from.R, to.R), Mix(from.G, to.G), Mix(from.B, to.B));
    }

    public static Color WithAlpha(Color color, double alpha) => Color.FromArgb((byte)Math.Round(color.A * Math.Clamp(alpha, 0, 1)), color.R, color.G, color.B);

    private static Color Rgb(byte r, byte g, byte b) => Color.FromArgb(255, r, g, b);

    private static Color Argb(byte a, byte r, byte g, byte b) => Color.FromArgb(a, r, g, b);
}

/// <summary>Reloj común de las animaciones dibujadas a mano, en segundos.</summary>
internal static class MotionClock
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    public static double Now => Clock.Elapsed.TotalSeconds;
}

/// <summary>Un valor que llega a su objetivo en un tiempo fijo con la curva estándar (ease-out quart).</summary>
internal sealed class Fade(double initial)
{
    private double _from = initial, _to = initial, _start, _duration;

    public double Target => _to;

    public void Jump(double value)
    {
        _from = _to = value;
        _duration = 0;
    }

    public void To(double target, double duration)
    {
        if (target == _to) return;
        var now = MotionClock.Now;
        _from = Value(now);
        _to = target;
        _start = now;
        _duration = duration;
    }

    public double Value(double now) =>
        _duration <= 0 || now >= _start + _duration ? _to : _from + (_to - _from) * (1 - Math.Pow(1 - (now - _start) / _duration, 4));

    public bool Done(double now) => _duration <= 0 || now >= _start + _duration;
}

/// <summary>Muelle amortiguado en pasos fijos de 2 ms: se mueve igual a cualquier tasa de refresco.</summary>
internal struct Spring(double value)
{
    public double Value = value;
    public double Target = value;
    public double Velocity;

    public readonly bool Settled => Value == Target && Velocity == 0;

    public void Step(double dt, double omega, double damping, double epsilon = 0.01, double rest = 0.05)
    {
        for (var t = 0.0; t < dt; t += 0.002)
        {
            var h = Math.Min(0.002, dt - t);
            Velocity += (omega * omega * (Target - Value) - 2 * damping * omega * Velocity) * h;
            Value += Velocity * h;
        }

        if (Math.Abs(Target - Value) < epsilon && Math.Abs(Velocity) < rest)
        {
            Value = Target;
            Velocity = 0;
        }
    }
}
