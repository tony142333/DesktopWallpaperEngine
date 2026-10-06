using WallpaperEngine.Core.Models;

namespace WallpaperEngine.Wallpaper;

public interface IWallpaperService
{
    IReadOnlyList<MonitorInfo> GetMonitors();

    /// <summary>Current wallpaper path for a monitor. Empty if unknown (slideshow / solid color).</summary>
    string GetWallpaper(string? monitorId);

    /// <summary>Sets a wallpaper. monitorId = null applies to all monitors.</summary>
    void SetWallpaper(string filePath, string? monitorId);

    /// <summary>Fit mode. Windows applies this to ALL monitors (API limitation).</summary>
    void SetFit(WallpaperFit fit);

    /// <summary>Saves the user's current wallpaper once, on first run. Never overwrites.</summary>
    void EnsureOriginalBackup();

    /// <summary>Restores the backed-up wallpaper. Returns false if no backup exists.</summary>
    bool RestoreOriginal();
}

