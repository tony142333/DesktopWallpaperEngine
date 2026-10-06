namespace WallpaperEngine.Core.Settings;

public enum AppTheme { System, Light, Dark }

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;

    // General
    public bool StartWithWindows { get; set; }
    public bool StartMinimized { get; set; }
    public bool CloseToTray { get; set; } = true;

    // Appearance
    public AppTheme Theme { get; set; } = AppTheme.System;
}
