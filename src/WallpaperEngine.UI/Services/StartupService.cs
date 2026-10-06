using Microsoft.Win32;
using WallpaperEngine.Core.Paths;

namespace WallpaperEngine.UI.Services;

public sealed class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(AppPaths.AppName) != null;
    }

    public void SetEnabled(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKey);

        if (enable)
        {
            var exe = Environment.ProcessPath ?? throw new InvalidOperationException("No process path");
            key.SetValue(AppPaths.AppName, $"\"{exe}\" --minimized");
        }
        else
        {
            key.DeleteValue(AppPaths.AppName, throwOnMissingValue: false);
        }
    }
}