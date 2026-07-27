namespace WindowsAdminShortcuts;

internal sealed record ShortcutDefinition(
    string Category,
    string DisplayName,
    string FileName,
    string TargetPath,
    string Arguments,
    string Description,
    string IconSourcePath,
    string? IconBadge,
    bool RunAsAdministrator = true);
