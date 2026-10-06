namespace WallpaperEngine.Wallpaper;

public sealed record MonitorInfo(string DevicePath, int Index, int Width, int Height, int X, int Y)
{
    public string Label => $"Monitor {Index} ({Width}x{Height})";
}

