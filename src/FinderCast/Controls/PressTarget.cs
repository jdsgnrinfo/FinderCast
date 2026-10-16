using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace FinderCast.Controls;

/// <summary>
/// Una zona que se pulsa como en ShutterGrid: el clic cuenta al soltar el botón si el puntero
/// sigue encima. Lleva la cuenta de "encima" y "pulsado" para que cada control anime su estado.
/// </summary>
internal partial class PressTarget : Grid
{
    public PressTarget()
    {
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        PointerEntered += (_, _) => Update(hover: true, Pressed);
        PointerExited += (_, _) => Update(hover: false, Pressed);
        PointerCanceled += (_, _) => Update(IsHovered, pressed: false);
        PointerCaptureLost += (_, _) => Update(IsHovered, pressed: false);
        PointerPressed += OnPressed;
        PointerReleased += OnReleased;
    }

    public event Action? Click;

    /// <summary>Se lanza al cambiar "encima" o "pulsado".</summary>
    public event Action? StateChanged;

    public bool IsHovered { get; private set; }

    public bool Pressed { get; private set; }

    /// <summary>La mano de los enlaces al pasar por encima.</summary>
    public bool ShowsHand
    {
        set => ProtectedCursor = value ? InputSystemCursor.Create(InputSystemCursorShape.Hand) : null;
    }

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        CapturePointer(e.Pointer);
        Update(IsHovered, pressed: true);
        e.Handled = true;
    }

    private void OnReleased(object sender, PointerRoutedEventArgs e)
    {
        var wasPressed = Pressed;
        ReleasePointerCapture(e.Pointer);
        Update(IsHovered, pressed: false);
        if (!wasPressed || !IsHovered) return;
        e.Handled = true;
        Click?.Invoke();
    }

    private void Update(bool hover, bool pressed)
    {
        if (hover == IsHovered && pressed == Pressed) return;
        IsHovered = hover;
        Pressed = pressed;
        StateChanged?.Invoke();
    }
}
