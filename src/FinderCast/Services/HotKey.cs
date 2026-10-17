using System.Text;
using System.Text.Json.Serialization;
using FinderCast.Interop;

namespace FinderCast.Services;

/// <summary>Teclas modificadoras, con los mismos valores que espera RegisterHotKey.</summary>
[Flags]
internal enum HotKeyModifiers : uint
{
    None = 0,
    Alt = 0x1,
    Control = 0x2,
    Shift = 0x4,
    Win = 0x8,
}

/// <summary>Un atajo de teclado global: modificadoras más una tecla (código virtual).</summary>
internal readonly record struct HotKey(HotKeyModifiers Modifiers, uint Key)
{
    /// <summary>Alt + Espacio, como en los lanzadores de macOS.</summary>
    public static readonly HotKey Default = new(HotKeyModifiers.Alt, Native.VK_SPACE);

    /// <summary>
    /// Nombres de cada tecla en el orden de Windows (Ctrl, Alt, Shift, Win, tecla), para
    /// dibujarlas como teclas sueltas.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<string> Parts
    {
        get
        {
            var parts = new List<string>(5);
            if (Modifiers.HasFlag(HotKeyModifiers.Control)) parts.Add("Ctrl");
            if (Modifiers.HasFlag(HotKeyModifiers.Alt)) parts.Add("Alt");
            if (Modifiers.HasFlag(HotKeyModifiers.Shift)) parts.Add("Shift");
            if (Modifiers.HasFlag(HotKeyModifiers.Win)) parts.Add("Win");
            parts.Add(KeyName(Key));
            return parts;
        }
    }

    public override string ToString() => string.Join(" + ", Parts);

    private static string KeyName(uint vk)
    {
        switch (vk)
        {
            case Native.VK_SPACE: return Strings.Current.SpaceKey;
            case Native.VK_RETURN: return "Enter";
            case >= 0x30 and <= 0x39: // 0-9
            case >= 0x41 and <= 0x5A: // A-Z
                return ((char)vk).ToString();
            case >= 0x70 and <= 0x87: // F1-F24
                return $"F{vk - 0x6F}";
        }

        // El resto (signos, flechas, Inicio…) con el nombre que les da la distribución del teclado.
        var scanCode = Native.MapVirtualKey(vk, Native.MAPVK_VK_TO_VSC);
        var lParam = (int)(scanCode << 16);
        if (vk is >= 0x21 and <= 0x2E) lParam |= 1 << 24; // Teclas extendidas (Re Pág, flechas, Supr…).
        var name = new StringBuilder(64);
        return Native.GetKeyNameText(lParam, name, name.Capacity) > 0 ? name.ToString() : $"0x{vk:X2}";
    }
}
