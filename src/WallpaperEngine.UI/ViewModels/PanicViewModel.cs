using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WallpaperEngine.Core.Models;
using WallpaperEngine.Core.Settings;
using WallpaperEngine.Hotkeys;

namespace WallpaperEngine.UI.ViewModels;

public partial class PanicViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly PanicService _panic;
    private readonly bool _loading = true;

    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private bool _hideWindow;
    [ObservableProperty] private string _hotkeyText = "";
    [ObservableProperty] private string _statusText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleButtonText))]
    private bool _isActive;

    public string ToggleButtonText => IsActive ? "Resume" : "Test panic";

    public PanicViewModel(ISettingsService settings, PanicService panic)
    {
        _settings = settings;
        _panic = panic;

        var s = settings.Current;
        _enabled = s.PanicEnabled;
        _hideWindow = s.PanicHideWindow;
        _hotkeyText = Format(s.PanicHotkey);
        _isActive = panic.IsActive;

        panic.StateChanged += (_, active) =>
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                IsActive = active;
                UpdateStatus();
            }));

        _loading = false;
        UpdateStatus(panic.LastError);
    }

    public static string Format(HotkeyGesture g)
    {
        var parts = new List<string>();
        if (g.Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (g.Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (g.Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (g.Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        parts.Add(KeyInterop.KeyFromVirtualKey(g.VirtualKey).ToString());
        return string.Join(" + ", parts);
    }

    private void UpdateStatus(string? error = null)
    {
        if (IsActive)
            StatusText = "PANIC ACTIVE: original wallpaper is showing. Press the hotkey again (after 2 seconds) or click Resume to bring back your wallpapers.";
        else if (error != null)
            StatusText = error;
        else if (!Enabled)
            StatusText = "Panic hotkey is disabled.";
        else
            StatusText = $"Armed. Press {HotkeyText} from any app to show your original wallpaper. Press it again to resume.";
    }

    partial void OnEnabledChanged(bool value)
    {
        if (_loading) return;
        _settings.Current.PanicEnabled = value;
        _settings.Save();
        _panic.Apply(out var error);
        UpdateStatus(error);
    }

    partial void OnHideWindowChanged(bool value)
    {
        if (_loading) return;
        _settings.Current.PanicHideWindow = value;
        _settings.Save();
    }

    public void CaptureHotkey(ModifierKeys modifiers, Key key)
    {
        var mods = (HotkeyModifiers)(int)modifiers;
        if (mods == HotkeyModifiers.None)
        {
            StatusText = "Use at least one modifier key (Ctrl, Alt, Shift or Win).";
            return;
        }

        var gesture = new HotkeyGesture(mods, KeyInterop.VirtualKeyFromKey(key));
        if (_panic.TryChangeHotkey(gesture, out var error))
        {
            HotkeyText = Format(gesture);
            UpdateStatus();
        }
        else
        {
            StatusText = error ?? "Could not register that hotkey.";
        }
    }

    [RelayCommand]
    private void TogglePanic()
    {
        _panic.KeepWindowVisible = true;   // button press shouldn't hide the window
        try { _panic.Trigger(); }
        finally { _panic.KeepWindowVisible = false; }
    }
}