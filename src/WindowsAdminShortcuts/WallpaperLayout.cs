namespace WindowsAdminShortcuts;

internal enum WallpaperLayout
{
    Fill,
    Fit,
    Stretch,
    Center,
    Tile,
    Span
}

internal sealed record WallpaperRegistryValues(string WallpaperStyle, string TileWallpaper);

internal static class WallpaperLayoutExtensions
{
    internal static WallpaperRegistryValues GetRegistryValues(this WallpaperLayout layout)
    {
        return layout switch
        {
            WallpaperLayout.Fill => new("10", "0"),
            WallpaperLayout.Fit => new("6", "0"),
            WallpaperLayout.Stretch => new("2", "0"),
            WallpaperLayout.Center => new("0", "0"),
            WallpaperLayout.Tile => new("0", "1"),
            WallpaperLayout.Span => new("22", "0"),
            _ => throw new ArgumentOutOfRangeException(nameof(layout), layout, null)
        };
    }
}
