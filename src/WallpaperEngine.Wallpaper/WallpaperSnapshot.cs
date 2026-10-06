using WallpaperEngine.Core.Models;

namespace WallpaperEngine.Wallpaper;

public sealed record MonitorWallpaper(string DevicePath, string WallpaperPath);

/// <summary>What the desktop looked like at a moment in time. Used to undo a panic.</summary>
public sealed record WallpaperSnapshot(WallpaperFit Fit, IReadOnlyList<MonitorWallpaper> Monitors);