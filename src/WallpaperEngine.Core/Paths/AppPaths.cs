namespace WallpaperEngine.Core.Paths;

public static class AppPaths
{
    public const string AppName = "DesktopWallpaperEngine";

    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);

    public static string Logs => Path.Combine(Root, "Logs");
    public static string Data => Path.Combine(Root, "Data");
    public static string Cache => Path.Combine(Root, "Cache");
    public static string SettingsFile => Path.Combine(Root, "settings.json");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Logs);
        Directory.CreateDirectory(Data);
        Directory.CreateDirectory(Cache);
    }
}