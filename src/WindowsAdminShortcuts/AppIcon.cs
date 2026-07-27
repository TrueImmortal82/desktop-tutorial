using System.Reflection;

namespace WindowsAdminShortcuts;

internal static class AppIcon
{
    internal const string ResourceName = "WindowsAdminShortcuts.AppIcon.ico";

    internal static Icon Load()
    {
        using Stream stream = Assembly
            .GetExecutingAssembly()
            .GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Встроенный ресурс иконки не найден: {ResourceName}");
        using var icon = new Icon(stream);
        return (Icon)icon.Clone();
    }
}
