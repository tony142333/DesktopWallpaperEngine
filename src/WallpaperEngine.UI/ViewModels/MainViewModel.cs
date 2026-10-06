using System.Diagnostics;
using System.Reflection;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WallpaperEngine.Core.Paths;
using WallpaperEngine.Core.Settings;
using WallpaperEngine.UI.Services;

namespace WallpaperEngine.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly StartupService _startup;
    private readonly ThemeService _theme;
    private readonly bool _loading = true;

    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private bool _startMinimized;
    [ObservableProperty] private bool _closeToTray;
    [ObservableProperty] private AppTheme _selectedTheme;

    public AppTheme[] Themes { get; } = Enum.GetValues<AppTheme>();
    public string DataFolder => AppPaths.Root;
    public string VersionText =>
        "Version " + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0");

    public MainViewModel(ISettingsService settings, StartupService startup, ThemeService theme)
    {
        _settings = settings;
        _startup = startup;
        _theme = theme;

        var s = settings.Current;
        _startWithWindows = s.StartWithWindows;
        _startMinimized = s.StartMinimized;
        _closeToTray = s.CloseToTray;
        _selectedTheme = s.Theme;
        _loading = false;
    }

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

    [RelayCommand]
    private void OpenDataFolder() => Process.Start("explorer.exe", AppPaths.Root);

    [RelayCommand]
    private void OpenLogsFolder() => Process.Start("explorer.exe", AppPaths.Logs);

    [RelayCommand]
    private void ExitApp() => ((App)Application.Current).ExitApplication();
}
