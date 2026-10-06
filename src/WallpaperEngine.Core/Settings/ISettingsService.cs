namespace WallpaperEngine.Core.Settings;

public interface ISettingsService
{
    AppSettings Current { get; }
    event EventHandler? Changed;
    void Load();
    void Save();
}