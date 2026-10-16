using FinderCast.Themes;
using Microsoft.UI.Xaml.Media;

namespace FinderCast.Controls;

/// <summary>
/// Llama a una función en cada fotograma mientras algo se mueve (devuelve true) y se detiene
/// sola al acabar: los controles quietos no gastan CPU.
/// </summary>
internal sealed class FrameLoop(Func<double, bool> step)
{
    private bool _running;
    private double _last;

    public void Start()
    {
        if (_running) return;
        _running = true;
        _last = MotionClock.Now;
        CompositionTarget.Rendering += OnFrame;
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        CompositionTarget.Rendering -= OnFrame;
    }

    private void OnFrame(object? sender, object e)
    {
        var now = MotionClock.Now;
        var dt = Math.Min(0.05, now - _last);
        _last = now;
        if (!step(dt)) Stop();
    }
}
