using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;
using WallpaperEngine.Core.Models;
using WallpaperEngine.Core.Paths;
using WallpaperEngine.Core.Settings;
using WallpaperEngine.Wallpaper;

namespace WallpaperEngine.Hotkeys;

/// <summary>Anything that must stop/mute on panic (playlist, live wallpaper, viewer...).</summary>
public interface IPanicAware
{
    string Name { get; }
    void OnPanic();
    void OnResume();
}

public sealed class PanicService : IDisposable
{
    private static readonly TimeSpan HotkeyResumeCooldown = TimeSpan.FromSeconds(2);

    private static readonly JsonSerializerOptions SnapshotJson = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static string SnapshotFile => Path.Combine(AppPaths.Data, "panic-snapshot.json");

    private readonly IWallpaperService _wallpaper;
    private readonly ISettingsService _settings;
    private readonly GlobalHotkeyManager _hotkeys = new();
    private readonly List<IPanicAware> _participants = new();
    private readonly object _gate = new();

    private WallpaperSnapshot? _snapshot;
    private int _hotkeyId;
    private DateTime _lastToggleUtc = DateTime.MinValue;
    private volatile bool _isActive;

    public bool IsActive => _isActive;
    public string? LastError { get; private set; }

    /// <summary>Set by the UI button so a manual test doesn't hide the window.</summary>
    public bool KeepWindowVisible { get; set; }

    /// <summary>Raised after every toggle. May fire on a non-UI thread. Argument = panic is now active.</summary>
    public event EventHandler<bool>? StateChanged;

    public PanicService(IWallpaperService wallpaper, ISettingsService settings)
    {
        _wallpaper = wallpaper;
        _settings = settings;

        // If the app closed during panic, the saved wallpaper is still waiting to be resumed.
        try
        {
            if (File.Exists(SnapshotFile))
            {
                _snapshot = JsonSerializer.Deserialize<WallpaperSnapshot>(File.ReadAllText(SnapshotFile), SnapshotJson);
                _isActive = _snapshot != null;
            }
        }
        catch (Exception ex) { Log.Warning(ex, "Could not load saved panic snapshot"); }
    }

    public void AddParticipant(IPanicAware participant)
    {
        lock (_gate) _participants.Add(participant);
    }

    /// <summary>(Re)registers the hotkey according to current settings.</summary>
    public bool Apply(out string? error)
    {
        error = null;

        if (_hotkeyId != 0)
        {
            _hotkeys.Unregister(_hotkeyId);
            _hotkeyId = 0;
        }

        var s = _settings.Current;
        if (!s.PanicEnabled)
        {
            LastError = null;
            return true;
        }

        if (_hotkeys.TryRegister(s.PanicHotkey, () => Trigger(fromHotkey: true), out var id, out error))
        {
            _hotkeyId = id;
            LastError = null;
            Log.Information("Panic hotkey registered (modifiers {Mods}, vk 0x{Vk:X})",
                s.PanicHotkey.Modifiers, s.PanicHotkey.VirtualKey);
            return true;
        }

        LastError = error;
        Log.Warning("Panic hotkey registration failed: {Error}", error);
        return false;
    }

    /// <summary>Tries a new hotkey. On failure the old one stays active.</summary>
    public bool TryChangeHotkey(HotkeyGesture gesture, out string? error)
    {
        var old = _settings.Current.PanicHotkey;
        _settings.Current.PanicHotkey = gesture;

        if (Apply(out error))
        {
            _settings.Save();
            return true;
        }

        _settings.Current.PanicHotkey = old;
        Apply(out _);
        return false;
    }

    /// <summary>Toggles panic. The cooldown only guards hotkey presses, and only for resuming.</summary>
    public void Trigger(bool fromHotkey = false)
    {
        bool nowActive;
        lock (_gate)
        {
            if (fromHotkey && _isActive && DateTime.UtcNow - _lastToggleUtc < HotkeyResumeCooldown)
            {
                Log.Debug("Hotkey press ignored (resume cooldown)");
                return;
            }

            if (_isActive) Resume(); else Panic();

            _lastToggleUtc = DateTime.UtcNow;
            nowActive = _isActive;
        }
        StateChanged?.Invoke(this, nowActive);
    }

    private void Panic()
    {
        Log.Warning("PANIC triggered");

        // 1. Remember what is showing right now (also saved to disk).
        try
        {
            _snapshot = _wallpaper.CaptureSnapshot();
            Directory.CreateDirectory(AppPaths.Data);
            File.WriteAllText(SnapshotFile, JsonSerializer.Serialize(_snapshot, SnapshotJson));
        }
        catch (Exception ex) { Log.Error(ex, "Snapshot failed"); }

        // 2. Show the original wallpaper.
        try
        {
            if (!_wallpaper.RestoreOriginal())
                Log.Error("Original wallpaper could not be restored");
        }
        catch (Exception ex) { Log.Error(ex, "RestoreOriginal threw"); }

        // 3. Stop everything else (live wallpaper, playlist, audio...).
        foreach (var p in _participants.ToArray())
        {
            try { p.OnPanic(); }
            catch (Exception ex) { Log.Error(ex, "Panic participant {Name} failed", p.Name); }
        }

        _isActive = true;
    }

    private void Resume()
    {
        Log.Information("Panic resumed");

        if (_snapshot != null)
        {
            try { _wallpaper.RestoreSnapshot(_snapshot); }
            catch (Exception ex) { Log.Error(ex, "Snapshot restore failed"); }
        }
        else
        {
            Log.Warning("Resume requested but no snapshot exists");
        }

        _snapshot = null;
        try { File.Delete(SnapshotFile); } catch { /* ignore */ }

        foreach (var p in _participants.ToArray())
        {
            try { p.OnResume(); }
            catch (Exception ex) { Log.Error(ex, "Resume participant {Name} failed", p.Name); }
        }

        _isActive = false;
    }

    public void Dispose() => _hotkeys.Dispose();
}