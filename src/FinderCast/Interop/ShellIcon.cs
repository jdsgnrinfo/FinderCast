using System.Runtime.InteropServices;
using System.Text;

namespace FinderCast.Interop;

/// <summary>
/// El icono de un acceso directo del menú Inicio. Se lee el icono real al que apunta (el que
/// indica el acceso directo, o el del programa) y se extrae al tamaño pedido: así los programas
/// que solo traen iconos pequeños no salen sobre la placa blanca que el Shell pone al ampliarlos,
/// y los accesos .url (los juegos de Steam) muestran su icono y no una hoja genérica. Si no se
/// puede resolver, se pide al Shell tal como lo dibuja el Explorador.
/// </summary>
internal static class ShellIcon
{
    private const int SIIGBF_ICONONLY = 0x4;

    /// <summary>Píxeles BGRA premultiplicados, de arriba abajo, o null si no hay icono.</summary>
    public static (byte[] Pixels, int Width, int Height)? Load(string path, int size)
    {
        if (IconLocation(path) is { } location && FromIconFile(location.File, location.Index, size) is { } icon) return icon;
        return FromShell(path, size);
    }

    /// <summary>El programa al que apunta un acceso directo .lnk, o null si no es un archivo.</summary>
    public static string? LinkTarget(string path)
    {
        if (!path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) return null;
        try
        {
            var link = (IShellLinkW)new ShellLink();
            try
            {
                ((IPersistFile)link).Load(path, 0);
                var buffer = new StringBuilder(1024);
                link.GetPath(buffer, buffer.Capacity, IntPtr.Zero, 0);
                var target = Environment.ExpandEnvironmentVariables(buffer.ToString());
                return target.Length > 0 && File.Exists(target) ? target : null;
            }
            finally
            {
                Marshal.ReleaseComObject(link);
            }
        }
        catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException or InvalidCastException)
        {
            return null;
        }
    }

    /// <summary>Dónde está el icono de un acceso directo: archivo e índice (negativo = id de recurso).</summary>
    private static (string File, int Index)? IconLocation(string path)
    {
        try
        {
            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".lnk" => LinkIcon(path),
                ".url" => UrlIcon(path),
                _ => null,
            };
        }
        catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException or InvalidCastException)
        {
            return null;
        }
    }

    private static (string File, int Index)? LinkIcon(string path)
    {
        var link = (IShellLinkW)new ShellLink();
        try
        {
            ((IPersistFile)link).Load(path, 0);

            var buffer = new StringBuilder(1024);
            link.GetIconLocation(buffer, buffer.Capacity, out var index);
            var file = Environment.ExpandEnvironmentVariables(buffer.ToString());
            if (file.Length > 0 && File.Exists(file)) return (file, index);

            // Sin icono propio: el del programa al que apunta (o el de la página, si apunta a un .url).
            buffer.Clear();
            link.GetPath(buffer, buffer.Capacity, IntPtr.Zero, 0);
            var target = Environment.ExpandEnvironmentVariables(buffer.ToString());
            if (target.Length == 0 || !File.Exists(target)) return null;
            return Path.GetExtension(target).Equals(".url", StringComparison.OrdinalIgnoreCase) ? UrlIcon(target) : (target, 0);
        }
        finally
        {
            Marshal.ReleaseComObject(link);
        }
    }

    /// <summary>[InternetShortcut] IconFile= / IconIndex=, como los que crea Steam.</summary>
    private static (string File, int Index)? UrlIcon(string path)
    {
        string? file = null;
        var index = 0;
        foreach (var line in File.ReadLines(path))
        {
            if (line.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase)) file = line["IconFile=".Length..].Trim();
            else if (line.StartsWith("IconIndex=", StringComparison.OrdinalIgnoreCase)) int.TryParse(line["IconIndex=".Length..].Trim(), out index);
        }
        if (file is not null)
        {
            file = Environment.ExpandEnvironmentVariables(file);
            if (File.Exists(file)) return (file, index);
        }

        // Un enlace web sin icono propio: el del navegador predeterminado, como en el Explorador.
        return BrowserIcon();
    }

    /// <summary>El icono de la aplicación del navegador predeterminado (no el de documento web, que es una hoja).</summary>
    private static (string File, int Index)? BrowserIcon()
    {
        const int AssocStrExecutable = 2;
        uint length = 1024;
        var buffer = new StringBuilder((int)length);
        if (AssocQueryString(0, AssocStrExecutable, "http", "open", buffer, ref length) != 0) return null;
        var browser = Environment.ExpandEnvironmentVariables(buffer.ToString().Trim().Trim('"'));
        return File.Exists(browser) ? (browser, 0) : null;
    }

    /// <summary>El icono escalado por Windows desde la imagen más grande que tenga, sin placa.</summary>
    private static (byte[] Pixels, int Width, int Height)? FromIconFile(string file, int index, int size)
    {
        if (SHDefExtractIcon(file, index, 0, out var icon, IntPtr.Zero, (uint)size) != 0 || icon == IntPtr.Zero) return null;
        try
        {
            return FromIcon(icon);
        }
        finally
        {
            DestroyIcon(icon);
        }
    }

    private static (byte[] Pixels, int Width, int Height)? FromIcon(IntPtr icon)
    {
        if (!GetIconInfo(icon, out var info)) return null;
        try
        {
            if (info.hbmColor == IntPtr.Zero) return null; // Icono monocromo: se deja al Shell.
            if (ReadBitmap(info.hbmColor) is not { } color) return null;
            var (pixels, width, height) = color;

            // Iconos antiguos sin canal alfa: la transparencia está en la máscara.
            var hasAlpha = false;
            for (var i = 3; i < pixels.Length && !hasAlpha; i += 4) hasAlpha = pixels[i] != 0;
            if (!hasAlpha && ReadBitmap(info.hbmMask) is { } mask)
            {
                for (var i = 0; i < pixels.Length; i += 4) pixels[i + 3] = mask.Pixels[i] == 0 ? (byte)255 : (byte)0;
            }

            // Los iconos guardan el alfa sin premultiplicar; WriteableBitmap lo quiere premultiplicado.
            for (var i = 0; i < pixels.Length; i += 4)
            {
                var alpha = pixels[i + 3];
                if (alpha == 255) continue;
                pixels[i] = (byte)(pixels[i] * alpha / 255);
                pixels[i + 1] = (byte)(pixels[i + 1] * alpha / 255);
                pixels[i + 2] = (byte)(pixels[i + 2] * alpha / 255);
            }
            return (pixels, width, height);
        }
        finally
        {
            if (info.hbmColor != IntPtr.Zero) DeleteObject(info.hbmColor);
            if (info.hbmMask != IntPtr.Zero) DeleteObject(info.hbmMask);
        }
    }

    /// <summary>Respaldo: el icono tal como lo dibuja el Explorador (ya premultiplicado).</summary>
    private static (byte[] Pixels, int Width, int Height)? FromShell(string path, int size)
    {
        IShellItemImageFactory? factory = null;
        var bitmap = IntPtr.Zero;
        try
        {
            var iid = typeof(IShellItemImageFactory).GUID;
            if (SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out factory) != 0) return null;
            if (factory.GetImage(new SIZE { cx = size, cy = size }, SIIGBF_ICONONLY, out bitmap) != 0) return null;
            return ReadBitmap(bitmap);
        }
        catch (COMException)
        {
            return null;
        }
        finally
        {
            if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
            if (factory is not null) Marshal.ReleaseComObject(factory);
        }
    }

    /// <summary>Los píxeles de un mapa de bits en BGRA de 32 bits, de arriba abajo.</summary>
    private static (byte[] Pixels, int Width, int Height)? ReadBitmap(IntPtr bitmap)
    {
        GetObject(bitmap, Marshal.SizeOf<BITMAP>(), out var info);
        var header = new BITMAPINFOHEADER
        {
            biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth = info.bmWidth,
            biHeight = -info.bmHeight, // Negativo: filas de arriba abajo.
            biPlanes = 1,
            biBitCount = 32,
        };
        var pixels = new byte[info.bmWidth * info.bmHeight * 4];
        var dc = GetDC(IntPtr.Zero);
        var lines = GetDIBits(dc, bitmap, 0, (uint)info.bmHeight, pixels, ref header, 0);
        ReleaseDC(IntPtr.Zero, dc);
        return lines == 0 ? null : (pixels, info.bmWidth, info.bmHeight);
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink;

    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int size, IntPtr findData, uint flags);
        void GetIDList(out IntPtr list);
        void SetIDList(IntPtr list);
        void GetDescription([MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int size);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] StringBuilder dir, int size);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);
        void GetArguments([MarshalAs(UnmanagedType.LPWStr)] StringBuilder args, int size);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int showCmd);
        void SetShowCmd(int showCmd);
        void GetIconLocation([MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath, int size, out int index);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(IntPtr hwnd, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
    }

    [ComImport]
    [Guid("0000010B-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPersistFile
    {
        void GetClassID(out Guid classId);
        [PreserveSig] int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string fileName, uint mode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string fileName, [MarshalAs(UnmanagedType.Bool)] bool remember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string fileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string fileName);
    }

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(SIZE size, int flags, out IntPtr bitmap);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx;
        public int cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        [MarshalAs(UnmanagedType.Bool)] public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public IntPtr bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr bindContext, ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory factory);

    /// <summary>Extrae el icono de un archivo al tamaño pedido (palabra baja de <paramref name="size"/>).</summary>
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHDefExtractIcon(string iconFile, int index, uint flags, out IntPtr large, IntPtr small, uint size);

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int AssocQueryString(uint flags, int str, string assoc, string? extra, StringBuilder output, ref uint length);

    [DllImport("user32.dll")]
    private static extern bool GetIconInfo(IntPtr icon, out ICONINFO info);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("gdi32.dll")]
    private static extern int GetObject(IntPtr h, int size, out BITMAP bitmap);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr dc, IntPtr bitmap, uint start, uint lines, byte[] bits, ref BITMAPINFOHEADER info, uint usage);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
}
