using System.Windows.Controls;
using H.NotifyIcon;

namespace WallpaperEngine.UI.Services;

public sealed class TrayService : IDisposable
{
    private readonly TaskbarIcon _icon;

    public TrayService(Action open, Action exit)
    {
        var menu = new ContextMenu();

        var openItem = new MenuItem { Header = "Open" };
        openItem.Click += (_, _) => open();

        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => exit();

        menu.Items.Add(openItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(exitItem);

        _icon = new TaskbarIcon
        {
            ToolTipText = "Desktop Wallpaper Engine",
            Icon = System.Drawing.SystemIcons.Application, // custom icon comes in the installer phase
            ContextMenu = menu
        };
        _icon.TrayMouseDoubleClick += (_, _) => open();
        _icon.ForceCreate();
    }

    public void Dispose() => _icon.Dispose();
}