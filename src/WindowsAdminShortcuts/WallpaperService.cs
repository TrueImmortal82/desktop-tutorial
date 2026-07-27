using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;

namespace WindowsAdminShortcuts;

internal sealed record WallpaperApplyResult(
    int ProfileCount,
    string ManagedWallpaperPath,
    string BackupPath,
    IReadOnlyList<string> SkippedProfiles);

internal static class WallpaperService
{
    private const string DesktopRegistryPath = @"Control Panel\Desktop";
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".bmp", ".jpg", ".jpeg", ".png" };

    internal static bool IsSupportedImageExtension(string path)
    {
        return SupportedExtensions.Contains(Path.GetExtension(path));
    }

    internal static WallpaperApplyResult ApplyToAllUsers(string sourcePath, WallpaperLayout layout)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Настройка обоев поддерживается только в Windows.");
        }

        if (!ElevationService.IsAdministrator())
        {
            throw new UnauthorizedAccessException(
                "Для изменения обоев всех пользователей приложение должно быть запущено от администратора.");
        }

        string fullSourcePath = Path.GetFullPath(sourcePath);
        ValidateImage(fullSourcePath);

        string dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "WindowsAdminShortcuts");
        string wallpaperDirectory = Path.Combine(dataDirectory, "Wallpapers");
        string backupDirectory = Path.Combine(dataDirectory, "Backups");
        Directory.CreateDirectory(wallpaperDirectory);
        Directory.CreateDirectory(backupDirectory);

        string operationId = $"{DateTime.UtcNow:yyyyMMdd-HHmmssfff}-{Guid.NewGuid():N}";
        string managedWallpaperPath = Path.Combine(
            wallpaperDirectory,
            $"wallpaper-{operationId}{Path.GetExtension(fullSourcePath).ToLowerInvariant()}");

        (List<ProfileTarget> profiles, List<string> skippedProfiles) = EnumerateProfiles();
        if (profiles.Count == 0)
        {
            throw new InvalidOperationException("Не найдено ни одного пользовательского профиля с NTUSER.DAT.");
        }

        var snapshots = new List<ProfileSnapshot>();
        foreach (ProfileTarget profile in profiles)
        {
            snapshots.Add(new ProfileSnapshot(profile, ReadState(profile)));
        }

        string backupPath = Path.Combine(backupDirectory, $"wallpaper-{operationId}.json");
        var backupDocument = new WallpaperBackupDocument(
            SchemaVersion: 1,
            CreatedUtc: DateTime.UtcNow,
            NewWallpaperPath: managedWallpaperPath,
            Profiles: snapshots);
        File.WriteAllText(
            backupPath,
            JsonSerializer.Serialize(backupDocument, new JsonSerializerOptions { WriteIndented = true }));
        try
        {
            File.Copy(fullSourcePath, managedWallpaperPath, overwrite: false);
        }
        catch (Exception copyException)
        {
            string? cleanupError = TryDelete(managedWallpaperPath);
            throw new IOException(
                cleanupError is null
                    ? "Не удалось подготовить общую копию файла обоев."
                    : $"Не удалось подготовить общую копию файла обоев. {cleanupError}",
                copyException);
        }

        WallpaperRegistryValues registryValues = layout.GetRegistryValues();
        var modified = new List<ProfileSnapshot>();
        string? currentSid = WindowsIdentity.GetCurrent().User?.Value;

        try
        {
            foreach (ProfileSnapshot snapshot in snapshots)
            {
                modified.Add(snapshot);
                WriteState(snapshot.Profile, managedWallpaperPath, registryValues);
            }

            if (!NativeMethods.SystemParametersInfo(
                    NativeMethods.SpiSetDesktopWallpaper,
                    0,
                    managedWallpaperPath,
                    NativeMethods.SpifUpdateIniFile | NativeMethods.SpifSendChange))
            {
                throw new InvalidOperationException(
                    $"Windows не применила обои для текущего пользователя. Win32={Marshal.GetLastWin32Error()}.");
            }
        }
        catch (Exception applyException)
        {
            List<string> rollbackErrors = Rollback(modified, currentSid);
            string? cleanupError = TryDelete(managedWallpaperPath);
            if (cleanupError is not null)
            {
                rollbackErrors.Add(cleanupError);
            }

            string rollbackMessage = rollbackErrors.Count == 0
                ? "Все уже изменённые профили возвращены к исходным настройкам."
                : $"Ошибки отката: {string.Join(" | ", rollbackErrors)}";
            throw new InvalidOperationException(
                $"Обои не применены ко всем профилям. {rollbackMessage} Backup: {backupPath}",
                applyException);
        }

        return new WallpaperApplyResult(
            snapshots.Count,
            managedWallpaperPath,
            backupPath,
            skippedProfiles);
    }

    private static void ValidateImage(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Файл обоев не найден.", path);
        }

        if (!IsSupportedImageExtension(path))
        {
            throw new InvalidDataException("Поддерживаются изображения BMP, JPG, JPEG и PNG.");
        }

        using Image image = Image.FromFile(path);
        if (image.Width <= 0 || image.Height <= 0)
        {
            throw new InvalidDataException("Выбранный файл не содержит корректного изображения.");
        }
    }

    private static (List<ProfileTarget> Profiles, List<string> Skipped) EnumerateProfiles()
    {
        var profiles = new List<ProfileTarget>();
        var skipped = new List<string>();

        using RegistryKey localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using RegistryKey profileList = localMachine.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList",
            writable: false) ?? throw new InvalidOperationException("Не найден системный список профилей Windows.");
        string windowsDirectory = Path.GetFullPath(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows));

        foreach (string sid in profileList.GetSubKeyNames())
        {
            if (sid is "S-1-5-18" or "S-1-5-19" or "S-1-5-20")
            {
                continue;
            }

            using RegistryKey? profileKey = profileList.OpenSubKey(sid, writable: false);
            string? rawPath = profileKey?.GetValue("ProfileImagePath") as string;
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                continue;
            }

            string profilePath = Environment.ExpandEnvironmentVariables(rawPath);
            if (Path.GetFullPath(profilePath).StartsWith(
                    windowsDirectory + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string hivePath = Path.Combine(profilePath, "NTUSER.DAT");
            if (!File.Exists(hivePath))
            {
                skipped.Add($"{sid}: отсутствует {hivePath}");
                continue;
            }

            profiles.Add(new ProfileTarget(
                Identifier: sid,
                DisplayName: Path.GetFileName(profilePath.TrimEnd(Path.DirectorySeparatorChar)),
                HivePath: hivePath,
                LoadedHiveName: sid));
        }

        string profilesDirectory = Environment.ExpandEnvironmentVariables(
            profileList.GetValue("ProfilesDirectory") as string
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".."));
        string defaultProfileName = profileList.GetValue("DefaultUserProfile") as string ?? "Default";
        string defaultHivePath = Path.Combine(profilesDirectory, defaultProfileName, "NTUSER.DAT");
        if (File.Exists(defaultHivePath))
        {
            profiles.Add(new ProfileTarget(
                Identifier: "DefaultUser",
                DisplayName: "Новые пользователи (Default)",
                HivePath: defaultHivePath,
                LoadedHiveName: null));
        }
        else
        {
            skipped.Add($"Default: отсутствует {defaultHivePath}");
        }

        return (
            profiles
                .GroupBy(profile => profile.Identifier, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList(),
            skipped);
    }

    private static WallpaperState ReadState(ProfileTarget profile)
    {
        using HiveSession hive = HiveSession.Open(profile, writable: false);
        using RegistryKey? desktop = hive.Root.OpenSubKey(DesktopRegistryPath, writable: false);
        return new WallpaperState(
            ReadString(desktop, "Wallpaper"),
            ReadString(desktop, "WallpaperStyle"),
            ReadString(desktop, "TileWallpaper"));
    }

    private static void WriteState(
        ProfileTarget profile,
        string wallpaperPath,
        WallpaperRegistryValues values)
    {
        using HiveSession hive = HiveSession.Open(profile, writable: true);
        using RegistryKey desktop = hive.Root.CreateSubKey(DesktopRegistryPath, writable: true)
            ?? throw new InvalidOperationException($"Не удалось открыть настройки профиля {profile.DisplayName}.");
        desktop.SetValue("Wallpaper", wallpaperPath, RegistryValueKind.String);
        desktop.SetValue("WallpaperStyle", values.WallpaperStyle, RegistryValueKind.String);
        desktop.SetValue("TileWallpaper", values.TileWallpaper, RegistryValueKind.String);
        desktop.Flush();
    }

    private static RegistryStringSnapshot ReadString(RegistryKey? key, string name)
    {
        if (key is null || !key.GetValueNames().Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            return new RegistryStringSnapshot(false, null);
        }

        return new RegistryStringSnapshot(true, key.GetValue(name)?.ToString());
    }

    private static List<string> Rollback(IReadOnlyList<ProfileSnapshot> modified, string? currentSid)
    {
        var errors = new List<string>();
        foreach (ProfileSnapshot snapshot in modified.Reverse())
        {
            try
            {
                RestoreState(snapshot.Profile, snapshot.State);
            }
            catch (Exception ex)
            {
                errors.Add($"{snapshot.Profile.DisplayName}: {ex.Message}");
            }
        }

        ProfileSnapshot? current = modified.FirstOrDefault(
            snapshot => string.Equals(
                snapshot.Profile.Identifier,
                currentSid,
                StringComparison.OrdinalIgnoreCase));
        if (current is not null)
        {
            string previousWallpaper = current.State.Wallpaper.Exists
                ? current.State.Wallpaper.Value ?? string.Empty
                : string.Empty;
            if (!NativeMethods.SystemParametersInfo(
                NativeMethods.SpiSetDesktopWallpaper,
                0,
                previousWallpaper,
                NativeMethods.SpifUpdateIniFile | NativeMethods.SpifSendChange))
            {
                errors.Add(
                    $"Текущие обои не восстановлены. Win32={Marshal.GetLastWin32Error()}.");
            }
        }

        return errors;
    }

    private static void RestoreState(ProfileTarget profile, WallpaperState state)
    {
        using HiveSession hive = HiveSession.Open(profile, writable: true);
        using RegistryKey desktop = hive.Root.CreateSubKey(DesktopRegistryPath, writable: true)
            ?? throw new InvalidOperationException($"Не удалось восстановить профиль {profile.DisplayName}.");
        RestoreString(desktop, "Wallpaper", state.Wallpaper);
        RestoreString(desktop, "WallpaperStyle", state.WallpaperStyle);
        RestoreString(desktop, "TileWallpaper", state.TileWallpaper);
        desktop.Flush();
    }

    private static void RestoreString(RegistryKey key, string name, RegistryStringSnapshot snapshot)
    {
        if (snapshot.Exists)
        {
            key.SetValue(name, snapshot.Value ?? string.Empty, RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(name, throwOnMissingValue: false);
        }
    }

    private static string? TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return null;
        }
        catch (Exception ex)
        {
            return $"Не удалось удалить неподтверждённый файл {path}: {ex.Message}";
        }
    }

    private sealed class HiveSession : IDisposable
    {
        private readonly bool _loadedByApplication;
        private readonly string _mountName;
        private bool _disposed;

        private HiveSession(RegistryKey root, string mountName, bool loadedByApplication)
        {
            Root = root;
            _mountName = mountName;
            _loadedByApplication = loadedByApplication;
        }

        internal RegistryKey Root { get; }

        internal static HiveSession Open(ProfileTarget profile, bool writable)
        {
            if (!string.IsNullOrWhiteSpace(profile.LoadedHiveName))
            {
                RegistryKey? loaded = Registry.Users.OpenSubKey(profile.LoadedHiveName, writable);
                if (loaded is not null)
                {
                    return new HiveSession(loaded, profile.LoadedHiveName, loadedByApplication: false);
                }
            }

            string mountName = $"WindowsAdminShortcuts_{Environment.ProcessId}_{Guid.NewGuid():N}";
            RunReg("load", $@"HKU\{mountName}", profile.HivePath);
            try
            {
                RegistryKey root = Registry.Users.OpenSubKey(mountName, writable)
                    ?? throw new InvalidOperationException(
                        $"Не удалось открыть загруженный профиль {profile.DisplayName}.");
                return new HiveSession(root, mountName, loadedByApplication: true);
            }
            catch (Exception openException)
            {
                try
                {
                    RunReg("unload", $@"HKU\{mountName}");
                }
                catch (Exception unloadException)
                {
                    throw new AggregateException(
                        $"Профиль {profile.DisplayName} не открыт и не выгружен.",
                        openException,
                        unloadException);
                }

                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Root.Dispose();
            if (_loadedByApplication)
            {
                RunReg("unload", $@"HKU\{_mountName}");
            }

            _disposed = true;
        }

        private static void RunReg(params string[] arguments)
        {
            string regExe = Path.Combine(Environment.SystemDirectory, "reg.exe");
            var startInfo = new ProcessStartInfo
            {
                FileName = regExe,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Не удалось запустить reg.exe.");
            string standardOutput = process.StandardOutput.ReadToEnd();
            string standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"reg.exe завершился с кодом {process.ExitCode}: {standardError} {standardOutput}".Trim());
            }
        }
    }

    internal sealed record ProfileTarget(
        string Identifier,
        string DisplayName,
        string HivePath,
        string? LoadedHiveName);

    internal sealed record RegistryStringSnapshot(bool Exists, string? Value);

    internal sealed record WallpaperState(
        RegistryStringSnapshot Wallpaper,
        RegistryStringSnapshot WallpaperStyle,
        RegistryStringSnapshot TileWallpaper);

    internal sealed record ProfileSnapshot(ProfileTarget Profile, WallpaperState State);

    internal sealed record WallpaperBackupDocument(
        int SchemaVersion,
        DateTime CreatedUtc,
        string NewWallpaperPath,
        IReadOnlyList<ProfileSnapshot> Profiles);

    private static class NativeMethods
    {
        internal const uint SpiSetDesktopWallpaper = 0x0014;
        internal const uint SpifUpdateIniFile = 0x0001;
        internal const uint SpifSendChange = 0x0002;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SystemParametersInfo(
            uint action,
            uint parameter,
            string value,
            uint flags);
    }
}
