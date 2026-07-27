using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace WindowsAdminShortcuts;

internal static class ShortcutIconService
{
    internal static string CreateIcon(
        string sourcePath,
        string cacheKey,
        string? iconBadge = null,
        string? category = null)
    {
        string iconDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "WindowsAdminShortcuts",
            "Icons");
        return CreateIconInDirectory(sourcePath, cacheKey, iconDirectory, iconBadge, category);
    }

    internal static string CreateIconInDirectory(
        string sourcePath,
        string cacheKey,
        string iconDirectory,
        string? iconBadge = null,
        string? category = null)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Извлечение Shell-иконок поддерживается только в Windows.");
        }

        string fullSourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullSourcePath) && !Directory.Exists(fullSourcePath))
        {
            throw new FileNotFoundException("Источник иконки не найден.", fullSourcePath);
        }

        Directory.CreateDirectory(iconDirectory);
        string iconPath = Path.Combine(iconDirectory, BuildIconFileName(cacheKey, fullSourcePath));
        string temporaryPath = Path.Combine(iconDirectory, $".{Guid.NewGuid():N}.ico.tmp");

        try
        {
            if (string.IsNullOrWhiteSpace(iconBadge))
            {
                using Icon icon = GetShellIcon(fullSourcePath);
                using FileStream stream = new(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None);
                icon.Save(stream);
                stream.Flush(flushToDisk: true);
            }
            else
            {
                WriteBadgeIcon(temporaryPath, iconBadge, category);
            }

            VerifyIcon(temporaryPath);
            File.Move(temporaryPath, iconPath, overwrite: true);
            VerifyIcon(iconPath);
            return iconPath;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void WriteBadgeIcon(string path, string badge, string? category)
    {
        const int size = 256;
        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.Clear(Color.Transparent);

        using GraphicsPath shadowPath = CreateRoundedRectangle(new RectangleF(22, 26, 218, 218), 48);
        using var shadowBrush = new SolidBrush(Color.FromArgb(45, 15, 23, 42));
        graphics.FillPath(shadowBrush, shadowPath);

        using GraphicsPath tilePath = CreateRoundedRectangle(new RectangleF(14, 14, 218, 218), 48);
        using var tileBrush = new SolidBrush(GetCategoryColor(category));
        graphics.FillPath(tileBrush, tilePath);
        using var borderPen = new Pen(Color.FromArgb(150, 255, 255, 255), 7);
        graphics.DrawPath(borderPen, tilePath);

        string label = badge.Trim().ToUpperInvariant();
        float fontSize = label.Length <= 2 ? 92F : 70F;
        using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(Color.White);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.None
        };
        graphics.DrawString(label, font, textBrush, new RectangleF(16, 10, 214, 220), format);

        using var png = new MemoryStream();
        bitmap.Save(png, ImageFormat.Png);
        byte[] imageBytes = png.ToArray();

        using FileStream output = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)1);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(imageBytes.Length);
        writer.Write(22);
        writer.Write(imageBytes);
        writer.Flush();
        output.Flush(flushToDisk: true);
    }

    private static GraphicsPath CreateRoundedRectangle(RectangleF rectangle, float radius)
    {
        float diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Color GetCategoryColor(string? category)
    {
        return category switch
        {
            "Основное" => Color.FromArgb(37, 99, 235),
            "Система" => Color.FromArgb(8, 145, 178),
            "Оборудование и диски" => Color.FromArgb(217, 119, 6),
            "Безопасность и пользователи" => Color.FromArgb(5, 150, 105),
            "Сеть" => Color.FromArgb(124, 58, 237),
            "Консоли" => Color.FromArgb(51, 65, 85),
            "Серверные роли" => Color.FromArgb(220, 38, 38),
            _ => Color.FromArgb(71, 85, 105)
        };
    }

    internal static void VerifyIcon(string path)
    {
        byte[] header = new byte[4];
        using FileStream stream = File.OpenRead(path);
        if (stream.Length <= header.Length || stream.Read(header, 0, header.Length) != header.Length)
        {
            throw new InvalidDataException($"Файл иконки пуст или повреждён: {path}");
        }

        if (header[0] != 0 || header[1] != 0 || header[2] != 1 || header[3] != 0)
        {
            throw new InvalidDataException($"Файл не является ICO: {path}");
        }
    }

    private static Icon GetShellIcon(string path)
    {
        var info = new ShellFileInfo();
        IntPtr result = NativeMethods.SHGetFileInfo(
            path,
            0,
            ref info,
            (uint)Marshal.SizeOf<ShellFileInfo>(),
            NativeMethods.ShgfiIcon | NativeMethods.ShgfiLargeIcon);
        if (result == IntPtr.Zero || info.IconHandle == IntPtr.Zero)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                $"Windows Shell не вернула иконку для {path}.");
        }

        try
        {
            using Icon borrowed = Icon.FromHandle(info.IconHandle);
            return (Icon)borrowed.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(info.IconHandle);
        }
    }

    private static string BuildIconFileName(string cacheKey, string sourcePath)
    {
        string name = Path.GetFileNameWithoutExtension(cacheKey);
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            name = "shortcut";
        }

        byte[] sourceBytes = Encoding.UTF8.GetBytes(sourcePath.ToUpperInvariant());
        string hash = Convert.ToHexString(SHA256.HashData(sourceBytes))[..12];
        return $"{name}-{hash}.ico";
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellFileInfo
    {
        internal IntPtr IconHandle;
        internal int IconIndex;
        internal uint Attributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        internal string DisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        internal string TypeName;
    }

    private static class NativeMethods
    {
        internal const uint ShgfiIcon = 0x000000100;
        internal const uint ShgfiLargeIcon = 0x000000000;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr SHGetFileInfo(
            string path,
            uint fileAttributes,
            ref ShellFileInfo fileInfo,
            uint fileInfoSize,
            uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DestroyIcon(IntPtr iconHandle);
    }
}
