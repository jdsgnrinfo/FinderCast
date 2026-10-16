using FinderCast.Interop;
using static FinderCast.Interop.Native;

namespace FinderCast.Services;

/// <summary>El atajo global que abre el lanzador desde cualquier app (RegisterHotKey).</summary>
internal sealed class GlobalHotKey : IDisposable
{
    private const int Id = 1;

    private readonly MessageWindow _window;
    private bool _registered;

    public GlobalHotKey(MessageWindow window)
    {
        _window = window;
        _window.MessageReceived += OnMessage;
    }

    public event Action? Pressed;

    public HotKey? Current { get; private set; }

    /// <summary>Cambia al atajo indicado. Si otra app ya lo usa, devuelve false y no queda ninguno activo.</summary>
    public bool Register(HotKey hotKey)
    {
        Unregister();
        _registered = RegisterHotKey(_window.Handle, Id, (uint)hotKey.Modifiers | MOD_NOREPEAT, hotKey.Key);
        Current = _registered ? hotKey : null;
        return _registered;
    }

    public void Unregister()
    {
        if (!_registered) return;
        UnregisterHotKey(_window.Handle, Id);
        _registered = false;
        Current = null;
    }

    public void Dispose()
    {
        Unregister();
        _window.MessageReceived -= OnMessage;
    }

    private bool OnMessage(uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg != WM_HOTKEY || wParam != Id) return false;
        Pressed?.Invoke();
        return true;
    }
}
