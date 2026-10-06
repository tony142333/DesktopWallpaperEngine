using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;
using WallpaperEngine.Core.Paths;

namespace WallpaperEngine.Core.Settings;

public sealed class JsonSettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly object _lock = new();

    public AppSettings Current { get; private set; } = new();
    public event EventHandler? Changed;

    public void Load()
    {
        lock (_lock)
        {
            var path = AppPaths.SettingsFile;
            if (!File.Exists(path))
            {
                Current = new AppSettings();
                Save();
                return;
            }

            try
            {
                var json = File.ReadAllText(path);
                Current = JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Settings file corrupt. Backing up and resetting to defaults.");
                try { File.Move(path, path + ".bad", overwrite: true); } catch { /* ignore */ }
                Current = new AppSettings();
                Save();
            }
        }
    }

    public void Save()
    {
        lock (_lock)
        {
            try
            {
                var tmp = AppPaths.SettingsFile + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(Current, Options));
                File.Move(tmp, AppPaths.SettingsFile, overwrite: true); // atomic-ish save
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save settings");
            }
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
