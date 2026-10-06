using WallpaperEngine.Wallpaper;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using WallpaperEngine.Core.Logging;
using WallpaperEngine.Core.Paths;
using WallpaperEngine.Core.Settings;
using WallpaperEngine.UI.Services;
using WallpaperEngine.UI.ViewModels;

namespace WallpaperEngine.UI;

public partial class App : Application
{
    private const string MutexName = @"Local\DesktopWallpaperEngine.Instance";
    private const string ShowEventName = @"Local\DesktopWallpaperEngine.Show";

    private Mutex? _mutex;
    private bool _ownsMutex;
    private EventWaitHandle? _showEvent;
    private ServiceProvider? _services;
    private TrayService? _tray;
    private MainWindow? _mainWindow;

    public bool IsExiting { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // --- Single instance ---
        _mutex = new Mutex(true, MutexName, out _ownsMutex);
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        if (!_ownsMutex)
        {
            _showEvent.Set(); // tell the running instance to show itself
            Shutdown();
            return;
        }

        AppPaths.EnsureCreated();
        LoggingSetup.Init();
        Log.Information("=== App starting ===");

        // --- Crash handling ---
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "Unhandled UI exception");
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Fatal(args.ExceptionObject as Exception, "Fatal unhandled exception");
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(args.Exception, "Unobserved task exception");
            args.SetObserved();
        };

        // --- Services ---
        var sc = new ServiceCollection();
        sc.AddSingleton<ISettingsService, JsonSettingsService>();
        sc.AddSingleton<StartupService>();
        sc.AddSingleton<ThemeService>();
        sc.AddSingleton<IWallpaperService, WallpaperService>();
        sc.AddSingleton<MainViewModel>();
        sc.AddSingleton<MainWindow>();
        _services = sc.BuildServiceProvider();

        var settings = _services.GetRequiredService<ISettingsService>();
        settings.Load();
        _services.GetRequiredService<ThemeService>().Apply(settings.Current.Theme);
        _services.GetRequiredService<IWallpaperService>().EnsureOriginalBackup();


        // --- Tray ---
        _tray = new TrayService(ShowMainWindow, RestoreOriginalWallpaper, ExitApplication);

        // --- Listen for "show" requests from a second launch ---
        var listener = new Thread(() =>
        {
            while (_showEvent.WaitOne())
            {
                if (IsExiting) break;
                Dispatcher.BeginInvoke(new Action(ShowMainWindow));
            }
        }) { IsBackground = true };
        listener.Start();

        // --- Initial window ---
        bool minimized = e.Args.Contains("--minimized") || settings.Current.StartMinimized;
        if (!minimized) ShowMainWindow();
    }

    private void ShowMainWindow()
    {
        _mainWindow ??= _services!.GetRequiredService<MainWindow>();
        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized)
            _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }
    private void RestoreOriginalWallpaper() =>
    _services!.GetRequiredService<IWallpaperService>().RestoreOriginal();
    

    public void ExitApplication()
    {
        if (IsExiting) return;
        IsExiting = true;
        Log.Information("=== App exiting ===");
        _tray?.Dispose();
        _showEvent?.Set(); // unblock listener thread
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        LoggingSetup.Shutdown();
        _services?.Dispose();
        if (_ownsMutex) _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}