using System.Runtime.InteropServices;
using static FinderCast.Interop.Native;

namespace FinderCast.Services;

/// <summary>
/// Captura el siguiente atajo que pulse el usuario, para el selector de los ajustes. Usa un
/// gancho de teclado de bajo nivel solo mientras graba: así recibe también combinaciones que
/// Windows se quedaría (Alt + Espacio abre el menú de la ventana, Win abre Inicio) y ninguna
/// tecla llega a otra app ni dispara el atajo actual.
/// </summary>
internal sealed class HotKeyRecorder : IDisposable
{
    private readonly LowLevelKeyboardProc _procedure;
    private IntPtr _hook;
    private HotKeyModifiers _held;

    public HotKeyRecorder() => _procedure = Callback;

    /// <summary>Atajo completo: al menos una modificadora y otra tecla (o una tecla F sola).</summary>
    public event Action<HotKey>? Recorded;

    /// <summary>Se pulsó Esc sin modificadoras.</summary>
    public event Action? Cancelled;

    public bool IsRecording => _hook != IntPtr.Zero;

    public void Start()
    {
        if (IsRecording) return;
        _held = HotKeyModifiers.None;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _procedure, GetModuleHandle(null), 0);
    }

    public void Stop()
    {
        if (!IsRecording) return;
        UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    public void Dispose() => Stop();

    private IntPtr Callback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code < 0) return CallNextHookEx(_hook, code, wParam, lParam);

        var key = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam).vkCode;
        var message = (uint)wParam;
        var down = message is WM_KEYDOWN or WM_SYSKEYDOWN;

        var modifier = ModifierOf(key);
        if (modifier != HotKeyModifiers.None)
        {
            _held = down ? _held | modifier : _held & ~modifier;
            return 1;
        }

        if (!down) return 1;

        if (key == VK_ESCAPE && _held == HotKeyModifiers.None)
        {
            Stop();
            Cancelled?.Invoke();
            return 1;
        }

        // Una letra sola no vale como atajo global: dejaría de poder escribirse.
        var isFunctionKey = key is >= VK_F1 and <= VK_F24;
        if (_held == HotKeyModifiers.None && !isFunctionKey) return 1;

        var hotKey = new HotKey(_held, key);
        Stop();
        Recorded?.Invoke(hotKey);
        return 1;
    }

    private static HotKeyModifiers ModifierOf(uint key) => key switch
    {
        VK_MENU or VK_LMENU or VK_RMENU => HotKeyModifiers.Alt,
        VK_CONTROL or VK_LCONTROL or VK_RCONTROL => HotKeyModifiers.Control,
        VK_SHIFT or VK_LSHIFT or VK_RSHIFT => HotKeyModifiers.Shift,
        VK_LWIN or VK_RWIN => HotKeyModifiers.Win,
        _ => HotKeyModifiers.None,
    };
}
