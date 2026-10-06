using System.ComponentModel;
using System.Windows;
using WallpaperEngine.Core.Settings;
using WallpaperEngine.UI.ViewModels;

namespace WallpaperEngine.UI;

public partial class MainWindow : Window
{
    private readonly ISettingsService _settings;

    public MainWindow(MainViewModel viewModel, ISettingsService settings)
    {
        InitializeComponent();
        DataContext = viewModel;
        _settings = settings;
        Closing += OnClosing;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        var app = (App)Application.Current;
        if (app.IsExiting) return;

        if (_settings.Current.CloseToTray)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            app.ExitApplication();
        }
    }
}