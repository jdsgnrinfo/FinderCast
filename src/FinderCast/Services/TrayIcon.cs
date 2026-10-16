using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FinderCast.Services;

/// <summary>
/// El icono del área de notificación (sistema de diseño 7.8), como el de ShutterGrid: un clic
/// abre los ajustes y el clic derecho muestra un menú de WinForms con los colores de los menús
/// de Windows 11, esquinas redondeadas por el propio Windows y dos opciones, abrir y salir.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ContextMenuStrip _menu;
    private readonly Icon _image;
    private readonly MenuRenderer _renderer = new();

    public TrayIcon(Action open, Action quit)
    {
        _menu = new ContextMenuStrip
        {
            Renderer = _renderer,
            ShowImageMargin = false,
            Font = new Font("Segoe UI Variable Text", 9f),
        };

        // Los márgenes separan el resaltado de los bordes superior e inferior del menú.
        var openItem = AddItem(open, new Padding(0, 4, 0, 1));
        var quitItem = AddItem(quit, new Padding(0, 1, 0, 4));

        _menu.HandleCreated += (_, _) => UseRoundedCorners(_menu.Handle);
        _menu.Opening += (_, _) =>
        {
            // En el idioma y el tema que haya en este momento.
            openItem.Text = string.Format(Strings.Current.Open, App.ProductName);
            quitItem.Text = Strings.Current.Quit;
            _renderer.Dark = !IsLightTheme();
            _menu.BackColor = _renderer.Background;
        };

        _image = new Icon(App.IconPath, SystemInformation.SmallIconSize);
        // Al pasar el ratón solo se muestra el nombre de la app.
        _icon = new NotifyIcon { Icon = _image, Text = App.ProductName, ContextMenuStrip = _menu };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) open();
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
        _icon.Visible = false; // Si no, el icono se queda hasta que el ratón pasa por encima.
        _icon.Dispose();
        _menu.Dispose();
        _image.Dispose();
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
