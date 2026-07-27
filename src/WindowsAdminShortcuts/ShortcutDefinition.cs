namespace WindowsAdminShortcuts;

internal sealed record ShortcutDefinition(
    string DisplayName,
    string FileName,
    string TargetPath,
    string Arguments,
    string Description,
    string IconPath,
    int IconIndex = 0);
