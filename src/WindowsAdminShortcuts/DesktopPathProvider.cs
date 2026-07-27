namespace WindowsAdminShortcuts;

internal static class DesktopPathProvider
{
    internal static string GetPath(DesktopScope scope)
    {
        Environment.SpecialFolder folder = scope == DesktopScope.AllUsers
            ? Environment.SpecialFolder.CommonDesktopDirectory
            : Environment.SpecialFolder.DesktopDirectory;

        string path = Environment.GetFolderPath(folder);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException(
                scope == DesktopScope.AllUsers
                    ? "Windows не вернула путь к общему рабочему столу."
                    : "Windows не вернула путь к рабочему столу текущего пользователя.");
        }

        return path;
    }
}
