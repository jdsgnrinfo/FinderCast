using System.Runtime.InteropServices;
using static FinderCast.Interop.Native;

namespace FinderCast.Interop;

/// <summary>
/// Ventana Win32 invisible que recibe los mensajes del atajo global y del icono de la bandeja.
/// Es de nivel superior (no HWND_MESSAGE) para recibir "TaskbarCreated" cuando el Explorador se
/// reinicia. Vive en el hilo de la interfaz, así que sus mensajes llegan ya en ese hilo.
/// </summary>
internal sealed class MessageWindow : IDisposable
{
    private const string ClassName = "FinderCast.MessageWindow";

    // El delegado se guarda en un campo estático: si el recolector lo liberara, Windows llamaría a memoria inválida.
    private static readonly WndProc Procedure = StaticWndProc;
    private static readonly Dictionary<IntPtr, MessageWindow> Windows = [];
    private static bool _classRegistered;

    public IntPtr Handle { get; }

    /// <summary>Mensaje recibido; devuelve true si se ha atendido.</summary>
    public event Func<uint, IntPtr, IntPtr, bool>? MessageReceived;

    public MessageWindow()
    {
        var instance = GetModuleHandle(null);
        if (!_classRegistered)
        {
            var wc = new WNDCLASSEX
            {
                cbSize = Marshal.SizeOf<WNDCLASSEX>(),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(Procedure),
                hInstance = instance,
                lpszClassName = ClassName,
            };
            RegisterClassEx(ref wc);
            _classRegistered = true;
        }

        Handle = CreateWindowEx(0, ClassName, "FinderCast", 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);
        if (Handle == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        Windows[Handle] = this;
    }

    public void Dispose()
    {
        Windows.Remove(Handle);
        DestroyWindow(Handle);
    }

    private static IntPtr StaticWndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (Windows.TryGetValue(hwnd, out var window) && window.MessageReceived is { } handlers)
        {
            foreach (var handler in handlers.GetInvocationList().Cast<Func<uint, IntPtr, IntPtr, bool>>())
            {
                if (handler(msg, wParam, lParam)) return IntPtr.Zero;
            }
        }
        return DefWindowProc(hwnd, msg, wParam, lParam);
    }
}
