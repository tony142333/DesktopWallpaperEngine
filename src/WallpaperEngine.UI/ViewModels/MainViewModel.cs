using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Serilog;
using WallpaperEngine.Core.Models;
using WallpaperEngine.Core.Paths;
using WallpaperEngine.Core.Settings;
using WallpaperEngine.UI.Services;
using WallpaperEngine.Wallpaper;

namespace WallpaperEngine.UI.ViewModels;

public sealed record MonitorTarget(string? DevicePath, string Label);

public partial class MainViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly StartupService _startup;
    private readonly ThemeService _theme;
    private readonly IWallpaperService _wallpaper;
    private bool _loading = true;

    // General
    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private bool _startMinimized;
    [ObservableProperty] private bool _closeToTray;
    [ObservableProperty] private AppTheme _selectedTheme;

    // Wallpaper
    [ObservableProperty] private WallpaperFit _selectedFit;
    [ObservableProperty] private MonitorTarget? _selectedTarget;
    [ObservableProperty] private ImageSource? _previewImage;
    [ObservableProperty] private string _statusText = "Ready";

    public ObservableCollection<MonitorTarget> Targets { get; } = new();
    public AppTheme[] Themes { get; } = Enum.GetValues<AppTheme>();
    public WallpaperFit[] Fits { get; } = Enum.GetValues<WallpaperFit>();
    public string DataFolder => AppPaths.Root;
    public string VersionText =>
        "Version " + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0");

    public MainViewModel(ISettingsService settings, StartupService startup,
                         ThemeService theme, IWallpaperService wallpaper)
    {
        _settings = settings;
        _startup = startup;
        _theme = theme;
        _wallpaper = wallpaper;

        var s = settings.Current;
        _startWithWindows = s.StartWithWindows;
        _startMinimized = s.StartMinimized;
        _closeToTray = s.CloseToTray;
        _selectedTheme = s.Theme;
        _selectedFit = s.Fit;

        RefreshMonitors();
        _loading = false;
    }

    // ---- General settings ----
    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_loading) return;
        _settings.Current.StartWithWindows = value;
        _startup.SetEnabled(value);
        _settings.Save();
    }

    partial void OnStartMinimizedChanged(bool value)
    {
        if (_loading) return;
        _settings.Current.StartMinimized = value;
        _settings.Save();
    }

    partial void OnCloseToTrayChanged(bool value)
    {
        if (_loading) return;
        _settings.Current.CloseToTray = value;
        _settings.Save();
    }

    partial void OnSelectedThemeChanged(AppTheme value)
    {
        if (_loading) return;
        _settings.Current.Theme = value;
        _theme.Apply(value);
        _settings.Save();
    }

    // ---- Wallpaper ----
    partial void OnSelectedFitChanged(WallpaperFit value)
    {
        if (_loading) return;
        _settings.Current.Fit = value;
        _settings.Save();
        try
        {
            _wallpaper.SetFit(value);
            StatusText = $"Fit mode: {value}";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SetFit failed");
            StatusText = "Could not change fit mode: " + ex.Message;
        }
    }

    partial void OnSelectedTargetChanged(MonitorTarget? value) => LoadPreview();

    [RelayCommand]
    private void RefreshMonitors()
    {
        try
        {
            Targets.Clear();
            Targets.Add(new MonitorTarget(null, "All monitors"));
            foreach (var m in _wallpaper.GetMonitors())
                Targets.Add(new MonitorTarget(m.DevicePath, m.Label));
            SelectedTarget = Targets[0];
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Monitor enumeration failed");
            StatusText = "Could not read monitors: " + ex.Message;
        }
    }

    [RelayCommand]
    private void ChooseWallpaper()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Choose a wallpaper",
            Filter = WallpaperService.FileDialogFilter,
            CheckFileExists = true
        };
        if (dlg.ShowDialog() != true) return;
        Apply(dlg.FileName);
    }

    private void Apply(string path)
    {
        try
        {
            _wallpaper.SetFit(SelectedFit);
            _wallpaper.SetWallpaper(path, SelectedTarget?.DevicePath);
            StatusText = $"Applied '{Path.GetFileName(path)}' to {SelectedTarget?.Label ?? "All monitors"}";
            LoadPreview();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Apply wallpaper failed");
            StatusText = "Could not apply wallpaper: " + ex.Message;
        }
    }

    [RelayCommand]
    private void RestoreOriginal()
    {
        bool ok = _wallpaper.RestoreOriginal();
        StatusText = ok ? "Original wallpaper restored" : "No usable backup found (see logs)";
        LoadPreview();
    }

    private void LoadPreview()
    {
        try
        {
            // "All monitors" has no single path, so preview the first real monitor.
            var id = SelectedTarget?.DevicePath ?? Targets.Skip(1).FirstOrDefault()?.DevicePath;
            var path = _wallpaper.GetWallpaper(id);

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                PreviewImage = null;
                return;
            }

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad; // don't lock the file
            bmp.DecodePixelWidth = 640;
            bmp.UriSource = new Uri(path);
            bmp.EndInit();
            bmp.Freeze();
            PreviewImage = bmp;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Preview failed");
            PreviewImage = null;
        }
    }

    // ---- Misc ----
    [RelayCommand]
    private void OpenDataFolder() => Process.Start("explorer.exe", AppPaths.Root);

    [RelayCommand]
    private void OpenLogsFolder() => Process.Start("explorer.exe", AppPaths.Logs);

    [RelayCommand]
    private void ExitApp() => ((App)Application.Current).ExitApplication();
}