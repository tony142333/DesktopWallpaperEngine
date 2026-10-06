using WallpaperEngine.Core.Models;

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

    // Wallpaper
    public WallpaperFit Fit { get; set; } = WallpaperFit.Fill;

    // Panic
    public bool PanicEnabled { get; set; } = true;
    public HotkeyGesture PanicHotkey { get; set; } =
        new(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x24); // Ctrl+Alt+Home
    public string? SafeWallpaperPath { get; set; }
    public bool PanicHideWindow { get; set; } = true;
}