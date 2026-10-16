using FinderCast.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace FinderCast;

/// <summary>
/// Punto de entrada. Si FinderCast ya está en marcha, esta instancia solo le pide que muestre
/// los ajustes y termina, sin llegar a arrancar XAML.
/// </summary>
public static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        using var instance = SingleInstance.TryAcquire();
        if (instance is null)
        {
            SingleInstance.SignalPrimary();
            return 0;
        }

        Application.Start(callback =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App(instance, args);
        });
        return 0;
    }
}
