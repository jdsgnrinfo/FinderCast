using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FinderCast.Services;

/// <summary>
/// El icono del área de notificación (sistema de diseño 7.8), como el de ShutterGrid: un clic
/// abre el lanzador y el clic derecho muestra un menú de WinForms con los colores de los menús
/// de Windows 11, esquinas redondeadas por el propio Windows y dos opciones, ajustes y salir. El
/// icono es la lupa de design/bandeja.svg, blanca o negra según el tema de la barra de tareas.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ContextMenuStrip _menu;
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private Icon? _image;
    private readonly MenuRenderer _renderer = new();

    public TrayIcon(Action launch, Action settings, Action quit)
    {
        _menu = new ContextMenuStrip
        {
            Renderer = _renderer,
            ShowImageMargin = false,
            Font = new Font("Segoe UI Variable Text", 9f),
        };

        // Los márgenes separan el resaltado de los bordes superior e inferior del menú.
        var settingsItem = AddItem(settings, new Padding(0, 4, 0, 1));
        var quitItem = AddItem(quit, new Padding(0, 1, 0, 4));

        _menu.HandleCreated += (_, _) => UseRoundedCorners(_menu.Handle);
        _menu.Opening += (_, _) =>
        {
            // En el idioma y el tema que haya en este momento.
            settingsItem.Text = Strings.Current.Settings;
            quitItem.Text = Strings.Current.Quit;
            _renderer.Dark = !IsLightTheme();
            _menu.BackColor = _renderer.Background;
        };

        // Al pasar el ratón solo se muestra el nombre de la app.
        _icon = new NotifyIcon { Text = App.ProductName, ContextMenuStrip = _menu };
        UpdateImage();
        // Al cambiar entre tema claro y oscuro (llega en otro hilo).
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) launch();
        };
    }

    /// <summary>Muestra u oculta el icono; la app sigue en marcha igualmente.</summary>
    public bool Visible
    {
        get => _icon.Visible;
        set => _icon.Visible = value;
    }

    public void Dispose()
    {
        Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _icon.Visible = false; // Si no, el icono se queda hasta que el ratón pasa por encima.
        _icon.Dispose();
        _menu.Dispose();
        DestroyImage();
    }

    private void OnUserPreferenceChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        if (e.Category != Microsoft.Win32.UserPreferenceCategory.General) return;
        if (_ui is null) UpdateImage();
        else _ui.Post(_ => UpdateImage(), null);
    }

    /// <summary>La lupa en blanco sobre la barra de tareas oscura y en negro sobre la clara.</summary>
    private void UpdateImage()
    {
        var previous = _image;
        _image = DrawLoupe(SystemInformation.SmallIconSize.Width, IsLightTaskbar() ? Color.Black : Color.White);
        _icon.Icon = _image;
        if (previous is not null) DestroyIcon(previous.Handle);
        previous?.Dispose();
    }

    private void DestroyImage()
    {
        if (_image is null) return;
        DestroyIcon(_image.Handle);
        _image.Dispose();
        _image = null;
    }

    /// <summary>
    /// design/bandeja.svg (12 × 12) dibujado al tamaño pedido: un anillo de radio 4 centrado en
    /// (5, 5) y un mango con los extremos redondeados hasta (10.5, 10.5). Con trazo de 1.4 en vez de
    /// 1 y la lupa (de 0.7 a 11.2) centrada en un recuadro de 12.4: ocupa 13 × 13 px, como los
    /// iconos de Wi-Fi y volumen de Windows.
    /// </summary>
    private static Icon DrawLoupe(int size, Color color)
    {
        using var bitmap = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            const float Stroke = 1.4f, From = -0.25f, To = 12.15f;
            var scale = size / (To - From);
            g.ScaleTransform(scale, scale);
            g.TranslateTransform(-From, -From);
            using var pen = new Pen(color, Stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawEllipse(pen, 1.5f, 1.5f, 7f, 7f);
            g.DrawLine(pen, 7.6f, 7.6f, 10.5f, 10.5f);
        }
        // FromHandle no se queda el icono: se libera con DestroyIcon al cambiarlo y al salir.
        return Icon.FromHandle(bitmap.GetHicon());
    }

    // Opciones con margen interior de 10/6/28/6 px.
    private ToolStripMenuItem AddItem(Action action, Padding margin)
    {
        var item = new ToolStripMenuItem { Padding = new Padding(10, 6, 28, 6), Margin = margin };
        item.Click += (_, _) => action();
        _menu.Items.Add(item);
        return item;
    }

    private static bool IsLightTheme()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
    }

    /// <summary>Tema de la barra de tareas (puede ser distinto del de las apps).</summary>
    private static bool IsLightTaskbar()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("SystemUsesLightTheme") is int value && value != 0;
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    private static void UseRoundedCorners(IntPtr hwnd)
    {
        const int DwmwaWindowCornerPreference = 33;
        var round = 2;
        DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref round, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>Colores planos del tema y un resaltado redondeado, como los menús de Windows 11.</summary>
    private sealed class MenuRenderer : ToolStripRenderer
    {
        public bool Dark { get; set; }

        public Color Background => Dark ? Color.FromArgb(0x2C, 0x2C, 0x2C) : Color.FromArgb(0xF9, 0xF9, 0xF9);

        private Color Text => Dark ? Color.White : Color.FromArgb(0x1A, 0x1A, 0x1A);

        // Negro al 4 % en claro, blanco al 7 % en oscuro.
        private Color Highlight => Dark ? Color.FromArgb(18, 255, 255, 255) : Color.FromArgb(10, 0, 0, 0);

        private static int MenuWidth(ToolStripItem item) => item.Owner?.ClientSize.Width ?? item.Width;

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using var fill = new SolidBrush(Background);
            e.Graphics.FillRectangle(fill, e.AffectedBounds);
        }

        // Windows dibuja el borde de una ventana con esquinas redondeadas.
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            // A 4 px de los bordes, con 4 px de radio, como en Windows 11. Con el ancho del
            // propio menú: WinForms puede hacer las opciones más anchas que él.
            var inset = 4 * g.DpiX / 96;
            var width = MenuWidth(e.Item) - e.Item.Bounds.X;
            var bounds = new RectangleF(inset, 0, width - inset * 2, e.Item.Height);
            var radius = 4 * g.DpiX / 96;

            using var shape = new GraphicsPath();
            shape.AddArc(bounds.X, bounds.Y, radius * 2, radius * 2, 180, 90);
            shape.AddArc(bounds.Right - radius * 2, bounds.Y, radius * 2, radius * 2, 270, 90);
            shape.AddArc(bounds.Right - radius * 2, bounds.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            shape.AddArc(bounds.X, bounds.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            shape.CloseFigure();
            using var fill = new SolidBrush(Highlight);
            g.FillPath(fill, shape);
        }

        // El texto se centra en la misma caja que el resaltado (WinForms lo deja algo alto).
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            var box = new Rectangle(e.Item.Padding.Left, 0, MenuWidth(e.Item) - e.Item.Padding.Left, e.Item.Height);
            TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, box, Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }
    }
}
