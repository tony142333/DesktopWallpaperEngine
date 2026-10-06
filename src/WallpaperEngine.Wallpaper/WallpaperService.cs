using System.Runtime.InteropServices;
using Serilog;
using WallpaperEngine.Core.Models;
using WallpaperEngine.Wallpaper.Native;

namespace WallpaperEngine.Wallpaper;

/// <summary>
/// Wraps IDesktopWallpaper. Must be called from an STA thread (the WPF UI thread is one).
/// </summary>
public sealed class WallpaperService : IWallpaperService
{
    private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".jfif", ".png", ".bmp", ".gif", ".tif", ".tiff"
    };

    public const string FileDialogFilter =
        "Images|*.jpg;*.jpeg;*.jfif;*.png;*.bmp;*.gif;*.tif;*.tiff|All files|*.*";

    private readonly OriginalWallpaperStore _store = new();

    // ---- COM helpers (create, use, release) ----
    private static T Get<T>(Func<IDesktopWallpaper, T> action)
    {
        var dw = DesktopWallpaperFactory.Create();
        try { return action(dw); }
        finally { Marshal.ReleaseComObject(dw); }
    }

    private static void Run(Action<IDesktopWallpaper> action) =>
        Get<object?>(dw => { action(dw); return null; });

    private static List<MonitorInfo> ReadMonitors(IDesktopWallpaper dw)
    {
        var list = new List<MonitorInfo>();
        uint count = dw.GetMonitorDevicePathCount();
        for (uint i = 0; i < count; i++)
        {
            try
            {
                var id = dw.GetMonitorDevicePathAt(i);
                if (string.IsNullOrEmpty(id)) continue;
                var r = dw.GetMonitorRECT(id);
                list.Add(new MonitorInfo(id, list.Count + 1, r.Right - r.Left, r.Bottom - r.Top, r.Left, r.Top));
            }
            catch (COMException)
            {
                // Monitor listed but not currently attached. Skip it.
            }
        }
        return list;
    }

    // ---- Public API ----
    public IReadOnlyList<MonitorInfo> GetMonitors() => Get(ReadMonitors);

    public string GetWallpaper(string? monitorId) => Get(dw => dw.GetWallpaper(monitorId) ?? "");

    public void SetWallpaper(string filePath, string? monitorId)
    {
        var full = Path.GetFullPath(filePath);
        if (!File.Exists(full))
            throw new FileNotFoundException("Wallpaper file not found.", full);
        if (!Supported.Contains(Path.GetExtension(full)))
            throw new NotSupportedException($"'{Path.GetExtension(full)}' is not supported for static wallpapers.");

        Run(dw => dw.SetWallpaper(monitorId, full));
        Log.Information("Wallpaper set: {Path} (monitor: {Monitor})", full, monitorId ?? "all");
    }

    public void SetFit(WallpaperFit fit)
    {
        Run(dw => dw.SetPosition((int)fit));
        Log.Information("Fit mode set: {Fit}", fit);
    }

    public void EnsureOriginalBackup()
    {
        if (_store.Exists) return;

        try
        {
            var backup = Get(dw =>
            {
                var b = new OriginalWallpaperBackup
                {
                    CapturedUtc = DateTime.UtcNow,
                    Fit = (WallpaperFit)dw.GetPosition(),
                    BackgroundColor = dw.GetBackgroundColor()
                };
                foreach (var m in ReadMonitors(dw))
                {
                    string path = "";
                    try { path = dw.GetWallpaper(m.DevicePath) ?? ""; } catch (COMException) { }
                    b.Monitors.Add(new MonitorBackup { DevicePath = m.DevicePath, WallpaperPath = path });
                }
                return b;
            });

            _store.Save(backup);
            Log.Information("Original wallpaper backed up ({Count} monitor(s))", backup.Monitors.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to back up original wallpaper");
        }
    }

    public bool RestoreOriginal()
    {
        OriginalWallpaperBackup? backup;
        try { backup = _store.Load(); }
        catch (Exception ex)
        {
            Log.Error(ex, "Original wallpaper backup unreadable");
            return false;
        }

        if (backup == null)
        {
            Log.Warning("RestoreOriginal called but no backup exists");
            return false;
        }

        try
        {
            Run(dw =>
            {
                try { dw.SetPosition((int)backup.Fit); } catch (COMException ex) { Log.Warning(ex, "Restore fit failed"); }
                try { dw.SetBackgroundColor(backup.BackgroundColor); } catch (COMException ex) { Log.Warning(ex, "Restore color failed"); }

                // Fallback if the original was a slideshow/solid color (empty path) or the file is gone.
                string? fallback = backup.Monitors.Select(m => m.WallpaperPath).FirstOrDefault(IsUsable)
                                   ?? DefaultWindowsWallpaper();

                foreach (var monitor in ReadMonitors(dw))
                {
                    var saved = backup.Monitors.FirstOrDefault(m => m.DevicePath == monitor.DevicePath);
                    string? path = saved != null && IsUsable(saved.WallpaperPath) ? saved.WallpaperPath : fallback;

                    if (path == null)
                    {
                        Log.Warning("No usable wallpaper to restore for {Monitor}", monitor.Label);
                        continue;
                    }
                    dw.SetWallpaper(monitor.DevicePath, path);
                }
            });

            Log.Information("Original wallpaper restored");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Restore failed");
            return false;
        }
        
    }
        public WallpaperSnapshot CaptureSnapshot() => Get(dw =>
    {
        var fit = (WallpaperFit)dw.GetPosition();
        var list = new List<MonitorWallpaper>();
        foreach (var m in ReadMonitors(dw))
        {
            string path = "";
            try { path = dw.GetWallpaper(m.DevicePath) ?? ""; } catch (COMException) { }
            list.Add(new MonitorWallpaper(m.DevicePath, path));
        }
        return new WallpaperSnapshot(fit, list);
    });

    public void RestoreSnapshot(WallpaperSnapshot snapshot) => Run(dw =>
    {
        try { dw.SetPosition((int)snapshot.Fit); }
        catch (COMException ex) { Log.Warning(ex, "Snapshot fit restore failed"); }

        foreach (var m in snapshot.Monitors)
        {
            if (!IsUsable(m.WallpaperPath)) continue; // slideshow/solid color: nothing to restore
            try { dw.SetWallpaper(m.DevicePath, m.WallpaperPath); }
            catch (COMException ex) { Log.Warning(ex, "Snapshot restore failed for a monitor"); }
        }
        Log.Information("Wallpaper snapshot restored");
    });

    public bool ApplySafeWallpaper(string? safeImagePath)
    {
        if (IsUsable(safeImagePath) && Supported.Contains(Path.GetExtension(safeImagePath!)))
        {
            try
            {
                Run(dw =>
                {
                    dw.SetPosition((int)WallpaperFit.Fill);
                    dw.SetWallpaper(null, Path.GetFullPath(safeImagePath!));
                });
                Log.Information("Safe wallpaper applied");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Safe wallpaper failed, falling back to original");
            }
        }
        return RestoreOriginal();
    }
    private static bool IsUsable(string? path) => !string.IsNullOrWhiteSpace(path) && File.Exists(path);

    private static string? DefaultWindowsWallpaper()
    {
        var p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                             @"Web\Wallpaper\Windows\img0.jpg");
        return File.Exists(p) ? p : null;
    }
}

