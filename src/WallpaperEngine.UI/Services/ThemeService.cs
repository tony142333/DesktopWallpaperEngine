using System.Windows;
using Microsoft.Win32;
using WallpaperEngine.Core.Settings;

namespace WallpaperEngine.UI.Services;

public sealed class ThemeService
{
    public void Apply(AppTheme theme)
    {
        var effective = theme == AppTheme.System
            ? (IsSystemLight() ? AppTheme.Light : AppTheme.Dark)
            : theme;

        var dict = new ResourceDictionary { Source = new Uri($"Themes/{effective}.xaml", UriKind.Relative) };
        var merged = Application.Current.Resources.MergedDictionaries;

        var old = merged.FirstOrDefault(d => d.Source?.OriginalString.StartsWith("Themes/") == true);
        if (old != null) merged.Remove(old);
        merged.Add(dict);
    }

    private static bool IsSystemLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return (key?.GetValue("AppsUseLightTheme") as int? ?? 1) == 1;
        }
        catch { return true; }
    }
}