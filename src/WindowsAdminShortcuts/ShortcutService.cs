namespace WindowsAdminShortcuts;

internal sealed record ShortcutCreateResult(string Path, string? BackupPath);

internal static class ShortcutService
{
    internal static string GetDestinationPath(string displayName, DesktopScope scope)
    {
        string fileName = NormalizeShortcutFileName(displayName);
        return Path.Combine(DesktopPathProvider.GetPath(scope), fileName);
    }

    internal static ShortcutCreateResult Create(ShortcutDefinition definition, DesktopScope scope)
    {
        string shortcutPath = Path.Combine(
            DesktopPathProvider.GetPath(scope),
            NormalizeShortcutFileName(definition.FileName));

        string workingDirectory = GetWorkingDirectory(definition.TargetPath);
        string iconPath = ShortcutIconService.CreateIcon(
            definition.IconSourcePath,
            definition.FileName,
            definition.IconBadge,
            definition.Category);
        string? backupPath = ShellLink.Create(
            shortcutPath,
            definition.TargetPath,
            definition.Arguments,
            definition.Description,
            workingDirectory,
            iconPath,
            runAsAdministrator: definition.RunAsAdministrator);

        return new ShortcutCreateResult(shortcutPath, backupPath);
    }

    internal static ShortcutCreateResult CreateCustom(
        string displayName,
        string targetPath,
        string arguments,
        bool runAsAdministrator,
        DesktopScope scope)
    {
        string expandedTarget = Environment.ExpandEnvironmentVariables(targetPath.Trim().Trim('"'));
        if (!File.Exists(expandedTarget) && !Directory.Exists(expandedTarget))
        {
            throw new FileNotFoundException("Указанная цель ярлыка не существует.", expandedTarget);
        }

        string shortcutPath = GetDestinationPath(displayName, scope);
        string iconPath = ShortcutIconService.CreateIcon(expandedTarget, displayName);
        string? backupPath = ShellLink.Create(
            shortcutPath,
            expandedTarget,
            arguments.Trim(),
            displayName.Trim(),
            GetWorkingDirectory(expandedTarget),
            iconPath,
            runAsAdministrator);

        return new ShortcutCreateResult(shortcutPath, backupPath);
    }

    internal static string NormalizeShortcutFileName(string value)
    {
        string name = value.Trim();
        if (name.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }

        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        name = name.Trim().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Название ярлыка не может быть пустым.", nameof(value));
        }

        return $"{name}.lnk";
    }

    private static string GetWorkingDirectory(string targetPath)
    {
        if (Directory.Exists(targetPath))
        {
            return targetPath;
        }

        return Path.GetDirectoryName(targetPath) ?? Environment.SystemDirectory;
    }
}
