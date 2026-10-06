using WallpaperEngine.Core.Models;

namespace WallpaperEngine.Wallpaper;

public interface IWallpaperService
{
    IReadOnlyList<MonitorInfo> GetMonitors();
    string GetWallpaper(string? monitorId);
    void SetWallpaper(string filePath, string? monitorId);
    void SetFit(WallpaperFit fit);
    void EnsureOriginalBackup();
    bool RestoreOriginal();

    /// <summary>Captures current fit + per-monitor wallpapers.</summary>
    WallpaperSnapshot CaptureSnapshot();

    /// <summary>Puts a captured snapshot back.</summary>
    void RestoreSnapshot(WallpaperSnapshot snapshot);

    /// <summary>
    /// Shows the safe wallpaper on every monitor. If no usable safe image is given,
    /// falls back to the backed-up original wallpaper.
    /// </summary>
    bool ApplySafeWallpaper(string? safeImagePath);
}