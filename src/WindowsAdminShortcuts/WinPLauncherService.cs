using System.Text;

namespace WindowsAdminShortcuts;

internal sealed record LauncherCreateResult(string Path, string? BackupPath);

internal static class WinPLauncherService
{
    internal const string FileName = "Win+P.cmd";
    internal const string ScriptContent =
        "@echo off\r\n" +
        "\"%SystemRoot%\\System32\\DisplaySwitch.exe\" /clone\r\n";

    internal static LauncherCreateResult Create(DesktopScope scope, bool overwrite)
    {
        return CreateInDirectory(DesktopPathProvider.GetPath(scope), overwrite);
    }

    internal static LauncherCreateResult CreateInDirectory(string directory, bool overwrite)
    {
        Directory.CreateDirectory(directory);
        string targetPath = Path.Combine(directory, FileName);
        string? backupPath = null;

        if (File.Exists(targetPath))
        {
            if (!overwrite)
            {
                throw new IOException($"Файл уже существует: {targetPath}");
            }

            backupPath = Path.Combine(
                directory,
                $"{FileName}.backup-{DateTime.Now:yyyyMMdd-HHmmss}");
            File.Copy(targetPath, backupPath, overwrite: false);
        }

        string temporaryPath = Path.Combine(directory, $".{FileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporaryPath, ScriptContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            VerifyScript(temporaryPath);
            File.Move(temporaryPath, targetPath, overwrite);
            VerifyScript(targetPath);
            return new LauncherCreateResult(targetPath, backupPath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    internal static void VerifyScript(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        for (int index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] == (byte)'\n' && (index == 0 || bytes[index - 1] != (byte)'\r'))
            {
                throw new InvalidDataException("Win+P launcher содержит LF без CRLF.");
            }
        }

        string content = File.ReadAllText(path, Encoding.UTF8);
        if (!string.Equals(content, ScriptContent, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Содержимое Win+P launcher не прошло проверку.");
        }
    }
}
