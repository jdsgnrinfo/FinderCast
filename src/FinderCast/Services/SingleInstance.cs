using System.Diagnostics;

namespace FinderCast.Services;

/// <summary>
/// Una sola instancia por usuario y sesión. Abrir FinderCast otra vez (menú Inicio, acceso
/// directo) avisa a la que ya está en marcha, que muestra los ajustes.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private static readonly string Id = $"FinderCast.{Environment.UserName}.{Process.GetCurrentProcess().SessionId}";
    private static readonly string MutexName = @"Local\" + Id;
    private static readonly string EventName = @"Local\" + Id + ".show";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _signal;
    private RegisteredWaitHandle? _wait;

    private SingleInstance(Mutex mutex, EventWaitHandle signal)
    {
        _mutex = mutex;
        _signal = signal;
    }

    /// <summary>Devuelve null si ya hay otra instancia en ejecución.</summary>
    public static SingleInstance? TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            return null;
        }
        return new SingleInstance(mutex, new EventWaitHandle(false, EventResetMode.AutoReset, EventName));
    }

    public static void SignalPrimary()
    {
        // Quien abre la app de nuevo tiene permiso para pasar ventanas al frente: se lo cede a la
        // instancia que ya está en marcha, para que sus ajustes aparezcan delante y no detrás.
        AllowSetForegroundWindow(-1);
        if (EventWaitHandle.TryOpenExisting(EventName, out var signal))
        {
            using (signal) signal.Set();
        }
    }

    /// <summary>Llama a <paramref name="onActivated"/> (en un hilo de fondo) cada vez que otra instancia pide mostrarse.</summary>
    public void Listen(Action onActivated)
        => _wait = ThreadPool.RegisterWaitForSingleObject(_signal, (_, _) => onActivated(), null, Timeout.Infinite, executeOnlyOnce: false);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int processId);

    public void Dispose()
    {
        _wait?.Unregister(null);
        _signal.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
