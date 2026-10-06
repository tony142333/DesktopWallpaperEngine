using System.Windows.Controls;
using System.Windows.Input;
using WallpaperEngine.UI.ViewModels;

namespace WallpaperEngine.UI;

public partial class PanicPanel : UserControl
{
    public PanicPanel() => InitializeComponent();

    private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        // Alt combinations arrive as Key.System
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Ignore bare modifier presses; wait for the real key
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin
            or Key.None or Key.DeadCharProcessed)
            return;

        if (DataContext is PanicViewModel vm)
            vm.CaptureHotkey(Keyboard.Modifiers, key);
    }
}