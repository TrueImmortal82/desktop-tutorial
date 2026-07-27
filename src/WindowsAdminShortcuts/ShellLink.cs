using System.Runtime.InteropServices;
using System.Text;

namespace WindowsAdminShortcuts;

internal static class ShellLink
{
    internal const uint RunAsUserFlag = 0x00002000;

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLinkObject
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int maxPath, IntPtr findData, uint flags);
        void GetIDList(out IntPtr pidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int maxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int maxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int maxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int showCommand);
        void SetShowCmd(int showCommand);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath, int iconPathLength, out int iconIndex);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(IntPtr windowHandle, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid classId);
        [PreserveSig] int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string fileName, uint mode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string fileName, bool remember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string fileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string fileName);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("45E2B4AE-B1C3-11D0-B92F-00A0C90312E1")]
    private interface IShellLinkDataList
    {
        void AddDataBlock(IntPtr dataBlock);
        void CopyDataBlock(uint signature, out IntPtr dataBlock);
        void RemoveDataBlock(uint signature);
        void GetFlags(out uint flags);
        void SetFlags(uint flags);
    }

    internal sealed record Inspection(string IconPath, int IconIndex, uint Flags);

    internal static string? Create(
        string shortcutPath,
        string targetPath,
        string arguments,
        string description,
        string workingDirectory,
        string iconPath,
        bool runAsAdministrator)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Создание ярлыков поддерживается только в Windows.");
        }

        string directory = Path.GetDirectoryName(shortcutPath)
            ?? throw new InvalidOperationException("Не удалось определить папку ярлыка.");
        Directory.CreateDirectory(directory);

        string? backupPath = null;
        if (File.Exists(shortcutPath))
        {
            backupPath = $"{shortcutPath}.backup-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
            File.Copy(shortcutPath, backupPath, overwrite: false);
        }

        object shellLinkObject = new ShellLinkObject();
        try
        {
            var shellLink = (IShellLinkW)shellLinkObject;
            shellLink.SetPath(targetPath);
            shellLink.SetArguments(arguments);
            shellLink.SetDescription(description);
            shellLink.SetWorkingDirectory(workingDirectory);
            if (!string.IsNullOrWhiteSpace(iconPath))
            {
                shellLink.SetIconLocation(iconPath, 0);
            }
            if (runAsAdministrator)
            {
                var dataList = (IShellLinkDataList)shellLinkObject;
                dataList.GetFlags(out uint flags);
                dataList.SetFlags(flags | RunAsUserFlag);
            }
            shellLink.SetShowCmd(1);

            ((IPersistFile)shellLink).Save(shortcutPath, true);
            if (!File.Exists(shortcutPath))
            {
                throw new IOException($"Windows не создала ярлык: {shortcutPath}");
            }

            return backupPath;
        }
        catch (Exception createException)
        {
            try
            {
                if (backupPath is not null)
                {
                    File.Copy(backupPath, shortcutPath, overwrite: true);
                }
                else if (File.Exists(shortcutPath))
                {
                    File.Delete(shortcutPath);
                }
            }
            catch (Exception restoreException)
            {
                throw new AggregateException(
                    $"Ярлык не создан, исходный файл не восстановлен: {shortcutPath}",
                    createException,
                    restoreException);
            }

            throw;
        }
        finally
        {
            if (Marshal.IsComObject(shellLinkObject))
            {
                Marshal.FinalReleaseComObject(shellLinkObject);
            }
        }
    }

    internal static Inspection Inspect(string shortcutPath)
    {
        object shellLinkObject = new ShellLinkObject();
        try
        {
            ((IPersistFile)shellLinkObject).Load(shortcutPath, 0);
            var iconPath = new StringBuilder(32768);
            ((IShellLinkW)shellLinkObject).GetIconLocation(
                iconPath,
                iconPath.Capacity,
                out int iconIndex);
            ((IShellLinkDataList)shellLinkObject).GetFlags(out uint flags);
            return new Inspection(iconPath.ToString(), iconIndex, flags);
        }
        finally
        {
            if (Marshal.IsComObject(shellLinkObject))
            {
                Marshal.FinalReleaseComObject(shellLinkObject);
            }
        }
    }
}
