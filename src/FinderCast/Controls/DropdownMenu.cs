using System.Globalization;
using System.Numerics;
using FinderCast.Interop;
using FinderCast.Themes;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.Graphics;
using Windows.System;
using DispatcherQueueTimer = Microsoft.UI.Dispatching.DispatcherQueueTimer;

namespace FinderCast.Controls;

/// <summary>
/// El desplegable del sistema de diseño (7.7): el movimiento de Arc UI con el aspecto de los
/// menús de Windows 11. Aparece 4 px bajo el botón (o encima si no cabe), con el borde derecho
/// alineado con él y al menos igual de ancho; panel con 4 px de margen y esquinas de 8 px,
/// opciones de 32 px con el texto a 10 px, resaltado de 4 px de radio y ✓ en la elegida.
/// Todo el movimiento corre en el compositor (muelles y curvas nativas), no en el hilo de la
/// interfaz: así no tiembla. No quita el foco a la ventana, que le pasa las teclas.
/// </summary>
internal sealed class DropdownMenu
{
    // Medidas (sección 7.7), en píxeles lógicos.
    private const double MenuPadding = 4;
    private const double ItemHeight = 32;
    private const double ItemInset = 10;
    private const double Radius = 8;
    private const double Offset = 4;
    private const double ShadowMargin = 48;
    private const double CheckSize = 15;

    // Movimiento (sección 6.3).
    private static readonly TimeSpan FadeIn = TimeSpan.FromMilliseconds(160);
    private static readonly TimeSpan Close = TimeSpan.FromMilliseconds(130);
    private static readonly TimeSpan ItemFade = TimeSpan.FromMilliseconds(240);
    private static readonly TimeSpan ItemStagger = TimeSpan.FromMilliseconds(35);
    private static readonly TimeSpan HighlightFade = TimeSpan.FromMilliseconds(80);
    private static readonly TimeSpan HoverGrace = TimeSpan.FromMilliseconds(70);
    private const float GrowFrom = 0.97f;
    private const float GrowShift = 5;
    private const float ItemNudge = 2;

    private readonly Popup _popup = new() { ShouldConstrainToRootBounds = false, IsLightDismissEnabled = false };
    private readonly Grid _root = new();
    private readonly Grid _motion = new();
    private readonly Grid _panel = new();
    private readonly Grid _shadowHost = new();
    private readonly Rectangle _surface = new() { RadiusX = Radius, RadiusY = Radius };
    // Como en ShutterGrid: el panel se rellena y encima va un trazo de 1 píxel físico centrado
    // sobre su borde (medio píxel por fuera y medio por dentro), sin redondeo a píxeles enteros.
    private readonly Border _border = new() { UseLayoutRounding = false };
    private readonly Canvas _items = new();
    private readonly Border _highlight = new() { CornerRadius = new CornerRadius(4), Height = ItemHeight };
    private readonly Grid _hit = new() { Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
    private readonly List<UIElement[]> _rows = [];
    private readonly Compositor _compositor;
    private readonly Visual _motionVisual;
    private readonly Visual _highlightVisual;
    private readonly CompositionEasingFunction _standard;
    private readonly DispatcherQueueTimer _closeTimer;
    private readonly DispatcherQueueTimer _graceTimer;

    private Action<int>? _chosen;
    private Palette _palette = Palette.Light;
    private bool _above;
    private bool _closing;
    private int _hover = -1;
    private int _pressedItem = -1;

    public DropdownMenu()
    {
        _compositor = CompositionTarget.GetCompositorForCurrentThread();
        // Curva estándar: ease-out quart, cubic-bezier(.22, 1, .36, 1).
        _standard = _compositor.CreateCubicBezierEasingFunction(new Vector2(0.22f, 1), new Vector2(0.36f, 1));

        _items.Children.Add(_highlight);
        _panel.Children.Add(_shadowHost);
        _panel.Children.Add(_surface);
        _panel.Children.Add(_border);
        _panel.Children.Add(_items);
        _panel.Children.Add(_hit); // Encima de todo: recibe el ratón en todo el panel.
        _motion.Children.Add(_panel);
        _root.Children.Add(_motion);
        _popup.Child = _root;

        // El panel y el resaltado se mueven desde su visual de composición: ahí corren los muelles.
        ElementCompositionPreview.SetIsTranslationEnabled(_motion, true);
        ElementCompositionPreview.SetIsTranslationEnabled(_highlight, true);
        _motionVisual = ElementCompositionPreview.GetElementVisual(_motion);
        _highlightVisual = ElementCompositionPreview.GetElementVisual(_highlight);

        _hit.PointerMoved += (_, e) => Hover(ItemAt(e.GetCurrentPoint(_panel).Position));
        _hit.PointerEntered += (_, e) => Hover(ItemAt(e.GetCurrentPoint(_panel).Position));
        _hit.PointerExited += (_, _) => Hover(-1);
        _hit.PointerPressed += (_, e) =>
        {
            _pressedItem = IsOpen ? ItemAt(e.GetCurrentPoint(_panel).Position) : -1;
            e.Handled = true;
        };
        _hit.PointerReleased += (_, e) =>
        {
            // Una opción pulsada y soltada cuenta aunque el menú haya empezado a cerrarse.
            var pressed = _pressedItem;
            _pressedItem = -1;
            var index = ItemAt(e.GetCurrentPoint(_panel).Position);
            if (index >= 0 && index == pressed) Choose(index);
            e.Handled = true;
        };

        var queue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        _closeTimer = queue.CreateTimer();
        _closeTimer.IsRepeating = false;
        _closeTimer.Interval = Close;
        _closeTimer.Tick += (_, _) => _popup.IsOpen = false;
        _graceTimer = queue.CreateTimer();
        _graceTimer.IsRepeating = false;
        _graceTimer.Interval = HoverGrace;
        _graceTimer.Tick += (_, _) =>
        {
            _hover = -1;
            FadeHighlight(0);
        };
    }

    /// <summary>Se lanza una vez, al empezar a cerrarse (sea como sea).</summary>
    public event Action? Dismissing;

    /// <summary>Abierto y no de salida.</summary>
    public bool IsOpen => _popup.IsOpen && !_closing;

    public void Open(FrameworkElement trigger, IntPtr hwnd, IReadOnlyList<(string Label, bool Checked)> items, Action<int> chosen)
    {
        _closeTimer.Stop();
        _graceTimer.Stop();
        _chosen = chosen;
        _closing = false;
        _hover = -1;
        _palette = Palette.For(trigger.ActualTheme == ElementTheme.Dark);
        _popup.XamlRoot = trigger.XamlRoot;
        _root.RequestedTheme = trigger.ActualTheme;

        var labels = BuildItems(items);
        var widest = labels.Max(label =>
        {
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return label.DesiredSize.Width;
        });
        // Al menos tan ancho como el botón, o más si una etiqueta y la ✓ lo necesitan.
        var width = Math.Max(trigger.ActualWidth, widest + ItemInset * 2 + MenuPadding * 2 + CheckSize + 8);
        var height = items.Count * ItemHeight + MenuPadding * 2 + 2;

        var bounds = trigger.TransformToVisual(null).TransformBounds(new Rect(0, 0, trigger.ActualWidth, trigger.ActualHeight));
        _above = !FitsBelow(hwnd, trigger.XamlRoot.RasterizationScale, bounds.Bottom + Offset + height);
        var left = bounds.Right - width;
        var top = _above ? bounds.Top - Offset - height : bounds.Bottom + Offset;

        _root.Width = width + ShadowMargin * 2;
        _root.Height = height + ShadowMargin * 2;
        _panel.Width = width;
        _panel.Height = height;
        _panel.Margin = new Thickness(ShadowMargin);
        _popup.HorizontalOffset = left - ShadowMargin;
        _popup.VerticalOffset = top - ShadowMargin;

        _surface.Fill = new SolidColorBrush(_palette.MenuSurface);
        _border.BorderBrush = new SolidColorBrush(_palette.MenuBorder);
        var pixel = 1 / trigger.XamlRoot.RasterizationScale;
        _border.Margin = new Thickness(-pixel / 2);
        _border.BorderThickness = new Thickness(pixel);
        _border.CornerRadius = new CornerRadius(Radius + pixel / 2);
        _highlight.Background = new SolidColorBrush(_palette.MenuHighlight);
        _highlight.Width = width - MenuPadding * 2;
        Canvas.SetLeft(_highlight, MenuPadding);
        _highlightVisual.Opacity = 0;
        _highlightVisual.Properties.InsertVector3("Translation", new Vector3(0, (float)ItemTop(0), 0));

        for (var i = 0; i < labels.Count; i++)
        {
            var label = labels[i];
            label.Width = width - MenuPadding * 2 - ItemInset * 2 - CheckSize - 8;
            Canvas.SetLeft(label, MenuPadding + ItemInset);
            Canvas.SetTop(label, ItemTop(i) + (ItemHeight - label.DesiredSize.Height) / 2);
            if (_rows[i].Length > 1)
            {
                Canvas.SetLeft(_rows[i][1], width - MenuPadding - ItemInset - CheckSize);
                Canvas.SetTop(_rows[i][1], ItemTop(i) + (ItemHeight - CheckSize) / 2);
            }
        }

        // Anclado en la esquina del botón: crece del 97 % y 5 px hacia él.
        _motionVisual.CenterPoint = new Vector3((float)(ShadowMargin + width), (float)(ShadowMargin + (_above ? height : 0)), 0);
        var towards = _above ? 1 : -1;
        _motionVisual.Opacity = 0;
        _motionVisual.Scale = new Vector3(GrowFrom, GrowFrom, 1);
        _motionVisual.Properties.InsertVector3("Translation", new Vector3(0, GrowShift * towards, 0));

        _popup.IsOpen = true;
        AttachShadow(width, height);

        _motionVisual.StartAnimation("Opacity", Fade(0, 1, FadeIn));
        // El muelle "crecer": ω 14, ζ 0,85, un leve rebote.
        _motionVisual.StartAnimation("Scale", Spring(new Vector3(1, 1, 1), omega: 14, damping: 0.85));
        _motionVisual.StartAnimation("Translation", Spring(Vector3.Zero, omega: 14, damping: 0.85));

        // Las opciones aparecen escalonadas (como mucho 4 escalones), empujadas 2 px desde el botón.
        for (var i = 0; i < _rows.Count; i++)
        {
            var delay = ItemStagger * Math.Min(i, 4);
            foreach (var element in _rows[i])
            {
                element.Opacity = 0;
                element.Translation = new Vector3(0, ItemNudge * towards, 0);
                element.StartAnimation(Fade(0, 1, ItemFade, delay));
                element.StartAnimation(Move(new Vector3(0, ItemNudge * towards, 0), Vector3.Zero, ItemFade, delay));
            }
        }
    }

    /// <summary>Se desvanece en 130 ms mientras vuelve a la mitad del crecimiento.</summary>
    public void Dismiss(bool animate = true)
    {
        if (!_popup.IsOpen || _closing) return;
        _closing = true;
        _graceTimer.Stop();
        Dismissing?.Invoke();

        if (!animate)
        {
            _popup.IsOpen = false;
            return;
        }

        var towards = _above ? 1 : -1;
        var half = 1 - (1 - GrowFrom) / 2;
        _motionVisual.StartAnimation("Opacity", Fade(1, 0, Close));
        _motionVisual.StartAnimation("Scale", Move(new Vector3(1, 1, 1), new Vector3(half, half, 1), Close, target: "Scale"));
        _motionVisual.StartAnimation("Translation", Move(Vector3.Zero, new Vector3(0, GrowShift / 2 * towards, 0), Close));
        _closeTimer.Start();
    }

    /// <summary>Teclas de la ventana mientras está abierto: flechas, Inicio, Fin, Enter, Esc. True si la ha usado.</summary>
    public bool HandleKey(VirtualKey key)
    {
        if (!IsOpen) return false;
        var count = _rows.Count;
        switch (key)
        {
            case VirtualKey.Down:
                MoveHighlight(_hover < 0 ? 0 : (_hover + 1) % count, glide: false);
                return true;
            case VirtualKey.Up:
                MoveHighlight(_hover < 0 ? count - 1 : (_hover + count - 1) % count, glide: false);
                return true;
            case VirtualKey.Home:
                MoveHighlight(0, glide: false);
                return true;
            case VirtualKey.End:
                MoveHighlight(count - 1, glide: false);
                return true;
            case VirtualKey.Enter or VirtualKey.Space when _hover >= 0:
                Choose(_hover);
                return true;
            case VirtualKey.Escape:
                Dismiss();
                return true;
            default:
                return false;
        }
    }

    private List<TextBlock> BuildItems(IReadOnlyList<(string Label, bool Checked)> items)
    {
        foreach (var row in _rows)
        {
            foreach (var element in row) _items.Children.Remove(element);
        }
        _rows.Clear();

        var labels = new List<TextBlock>();
        foreach (var (text, isChecked) in items)
        {
            var label = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Segoe UI Variable Text"),
                FontSize = 9.5 * 4 / 3.0,
                Foreground = new SolidColorBrush(_palette.Text),
                TextTrimming = TextTrimming.CharacterEllipsis,
                IsHitTestVisible = false,
            };
            _items.Children.Add(label);
            labels.Add(label);

            if (isChecked)
            {
                var tick = Chevron.Check(CheckSize, strokeWidth: 1.8);
                tick.Stroke = new SolidColorBrush(_palette.Secondary);
                tick.IsHitTestVisible = false;
                _items.Children.Add(tick);
                _rows.Add([label, tick]);
            }
            else
            {
                _rows.Add([label]);
            }
        }
        return labels;
    }

    /// <summary>Sombra de la sección 5.2: oscuro, 8 px abajo y 12 px de desenfoque al 32 %; claro, 6 px y 10 px al 12 % más una de contacto.</summary>
    private void AttachShadow(double width, double height)
    {
        var container = _compositor.CreateContainerVisual();
        var mask = _surface.GetAlphaMask();
        var size = new Vector2((float)width, (float)height);

        void Layer(float down, float blur, float opacity)
        {
            var shadow = _compositor.CreateDropShadow();
            shadow.Mask = mask;
            // El desenfoque de la composición es un radio: unas 2,5 veces la desviación del gaussiano.
            shadow.BlurRadius = blur * 2.5f;
            shadow.Offset = new Vector3(0, down, 0);
            shadow.Opacity = opacity;
            shadow.Color = Microsoft.UI.Colors.Black;
            var sprite = _compositor.CreateSpriteVisual();
            sprite.Size = size;
            sprite.Shadow = shadow;
            container.Children.InsertAtTop(sprite);
        }

        if (_palette.Dark)
        {
            Layer(8, 12, 0.32f);
        }
        else
        {
            Layer(6, 10, 0.12f);
            Layer(1, 2, 0.04f);
        }
        ElementCompositionPreview.SetElementChildVisual(_shadowHost, container);
    }

    private static bool FitsBelow(IntPtr hwnd, double scale, double bottomInWindow)
    {
        var origin = new Native.POINT();
        Native.ClientToScreen(hwnd, ref origin);
        var area = DisplayArea.GetFromPoint(new PointInt32(origin.X, origin.Y), DisplayAreaFallback.Nearest).WorkArea;
        return origin.Y + bottomInWindow * scale <= area.Y + area.Height;
    }

    private static double ItemTop(int index) => 1 + MenuPadding + index * ItemHeight;

    private int ItemAt(Point point)
    {
        if (point.X < 0 || point.X > _panel.Width) return -1;
        var index = (int)Math.Floor((point.Y - ItemTop(0)) / ItemHeight);
        return index >= 0 && index < _rows.Count ? index : -1;
    }

    private void Hover(int index)
    {
        if (_closing) return;
        if (index >= 0)
        {
            MoveHighlight(index, glide: true);
        }
        else if (_hover >= 0 && !_graceTimer.IsRunning)
        {
            // Se mantiene 70 ms al cruzar entre opciones.
            _graceTimer.Start();
        }
    }

    private void MoveHighlight(int index, bool glide)
    {
        _graceTimer.Stop();
        if (index == _hover) return;

        var top = new Vector3(0, (float)ItemTop(index), 0);
        // El ratón lo desliza con el muelle "snappy" de Arc UI; el teclado, o la primera opción tocada, lo pone en su sitio.
        if (glide && _hover >= 0)
        {
            _highlightVisual.StartAnimation("Translation", Spring(top, omega: 24, damping: 0.88));
        }
        else
        {
            _highlightVisual.StopAnimation("Translation");
            _highlightVisual.Properties.InsertVector3("Translation", top);
        }

        _hover = index;
        FadeHighlight(1);
    }

    private void FadeHighlight(float to)
    {
        var fade = _compositor.CreateScalarKeyFrameAnimation();
        fade.InsertExpressionKeyFrame(0, "this.StartingValue");
        fade.InsertKeyFrame(1, to, _compositor.CreateLinearEasingFunction());
        fade.Duration = HighlightFade;
        _highlightVisual.StartAnimation("Opacity", fade);
    }

    private void Choose(int index)
    {
        var chosen = _chosen;
        Dismiss();
        chosen?.Invoke(index);
    }

    private ScalarKeyFrameAnimation Fade(float from, float to, TimeSpan duration, TimeSpan delay = default)
    {
        var animation = _compositor.CreateScalarKeyFrameAnimation();
        animation.Target = "Opacity";
        animation.InsertKeyFrame(0, from);
        animation.InsertKeyFrame(1, to, _standard);
        animation.Duration = duration;
        animation.DelayTime = delay;
        animation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        return animation;
    }

    private Vector3KeyFrameAnimation Move(Vector3 from, Vector3 to, TimeSpan duration, TimeSpan delay = default, string target = "Translation")
    {
        var animation = _compositor.CreateVector3KeyFrameAnimation();
        animation.Target = target;
        animation.InsertKeyFrame(0, from);
        animation.InsertKeyFrame(1, to, _standard);
        animation.Duration = duration;
        animation.DelayTime = delay;
        animation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        return animation;
    }

    /// <summary>
    /// Un muelle amortiguado (sección 6.2) desde el valor actual hasta <paramref name="to"/>,
    /// muestreado a 120 Hz en fotogramas clave del compositor con su respuesta exacta.
    /// </summary>
    private Vector3KeyFrameAnimation Spring(Vector3 to, double omega, double damping)
    {
        // Tiempo hasta quedar a menos de una milésima del destino.
        var settle = Math.Max(0.05, Math.Log(1000) / (damping * omega));
        var steps = (int)Math.Ceiling(settle * 120);
        var linear = _compositor.CreateLinearEasingFunction();
        var target = string.Create(CultureInfo.InvariantCulture, $"Vector3({to.X}, {to.Y}, {to.Z})");

        var animation = _compositor.CreateVector3KeyFrameAnimation();
        animation.InsertExpressionKeyFrame(0, "this.StartingValue");
        for (var k = 1; k <= steps; k++)
        {
            var t = settle * k / steps;
            var progress = k == steps ? 1 : StepResponse(t, omega, damping);
            animation.InsertExpressionKeyFrame((float)k / steps,
                string.Create(CultureInfo.InvariantCulture, $"this.StartingValue + ({target} - this.StartingValue) * {progress:0.######}"), linear);
        }
        animation.Duration = TimeSpan.FromSeconds(settle);
        return animation;
    }

    /// <summary>Posición (0 → 1) de un muelle de frecuencia ω y amortiguación ζ que parte quieto.</summary>
    private static double StepResponse(double t, double omega, double damping)
    {
        if (damping >= 1) return 1 - Math.Exp(-omega * t) * (1 + omega * t);
        var damped = omega * Math.Sqrt(1 - damping * damping);
        return 1 - Math.Exp(-damping * omega * t) * (Math.Cos(damped * t) + damping / Math.Sqrt(1 - damping * damping) * Math.Sin(damped * t));
    }
}
