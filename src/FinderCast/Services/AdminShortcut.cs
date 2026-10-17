using FinderCast.Interop;
using Microsoft.UI.Input;
using Windows.System;
using Windows.UI.Core;

namespace FinderCast.Services;

/// <summary>
/// Atajo para abrir el resultado elegido como administrador: una o más modificadoras (Ctrl, Alt,
/// Shift) con Enter. Se graba en los ajustes igual que el atajo para abrir.
/// </summary>
internal static class AdminShortcut
{
    public const HotKeyModifiers Default = HotKeyModifiers.Control;

    /// <summary>True si el atajo grabado vale: termina en Enter y lleva modificadoras, sin Win.</summary>
    public static bool IsValid(HotKey hotKey)
        => hotKey.Key == Native.VK_RETURN
           && hotKey.Modifiers != HotKeyModifiers.None
           && !hotKey.Modifiers.HasFlag(HotKeyModifiers.Win);

    /// <summary>Las teclas que se dibujan, Enter incluida: ["Ctrl", "Enter"].</summary>
    public static IReadOnlyList<string> Keys(HotKeyModifiers modifiers) => new HotKey(modifiers, Native.VK_RETURN).Parts;

    /// <summary>True si ahora mismo están pulsadas justo esas modificadoras (ni más ni menos).</summary>
    public static bool IsHeld(HotKeyModifiers modifiers)
    {
        var held = HotKeyModifiers.None;
        if (Down(VirtualKey.Control)) held |= HotKeyModifiers.Control;
        if (Down(VirtualKey.Shift)) held |= HotKeyModifiers.Shift;
        if (Down(VirtualKey.Menu)) held |= HotKeyModifiers.Alt;
        return held == modifiers;
    }

    private static bool Down(VirtualKey key)
        => InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(CoreVirtualKeyStates.Down);
}
