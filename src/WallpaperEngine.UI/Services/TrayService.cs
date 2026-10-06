using System.Windows.Controls;
using H.NotifyIcon;

namespace WallpaperEngine.UI.Services;

public sealed class TrayService : IDisposable
{
    private readonly TaskbarIcon _icon;

    public TrayService(Action open, Action panic, Action restoreOriginal, Action exit)
    {
        var menu = new ContextMenu();

        var openItem = new MenuItem { Header = "Open" };
        openItem.Click += (_, _) => open();

        var panicItem = new MenuItem { Header = "Panic / Resume" };
        panicItem.Click += (_, _) => panic();

        var restoreItem = new MenuItem { Header = "Restore original wallpaper" };
        restoreItem.Click += (_, _) => restoreOriginal();

        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => exit();

        menu.Items.Add(openItem);
        menu.Items.Add(panicItem);
        menu.Items.Add(restoreItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(exitItem);

        _icon = new TaskbarIcon
        {
            ToolTipText = "Desktop Wallpaper Engine",
            Icon = System.Drawing.SystemIcons.Application,
            ContextMenu = menu
        };
        _icon.TrayMouseDoubleClick += (_, _) => open();
        _icon.ForceCreate();
    }

    public void Dispose() => _icon.Dispose();
}