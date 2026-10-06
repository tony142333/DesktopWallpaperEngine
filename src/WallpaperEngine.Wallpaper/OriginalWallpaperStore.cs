using System.Text.Json;
using System.Text.Json.Serialization;
using WallpaperEngine.Core.Models;
using WallpaperEngine.Core.Paths;

namespace WallpaperEngine.Wallpaper;

internal sealed class MonitorBackup
{
    public string DevicePath { get; set; } = "";
    public string WallpaperPath { get; set; } = "";
}

internal sealed class OriginalWallpaperBackup
{
    public DateTime CapturedUtc { get; set; }
    public WallpaperFit Fit { get; set; }
    public uint BackgroundColor { get; set; }
    public List<MonitorBackup> Monitors { get; set; } = new();
}

internal sealed class OriginalWallpaperStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static string FilePath => Path.Combine(AppPaths.Data, "original-wallpaper.json");

    public bool Exists => File.Exists(FilePath);

    public void Save(OriginalWallpaperBackup backup)
    {
        Directory.CreateDirectory(AppPaths.Data);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(backup, Options));
    }

    public OriginalWallpaperBackup? Load()
    {
        if (!Exists) return null;
        return JsonSerializer.Deserialize<OriginalWallpaperBackup>(File.ReadAllText(FilePath), Options);
    }
}

