using System.Runtime.InteropServices;

namespace WallpaperEngine.Wallpaper.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct NativeRect
{
    public int Left, Top, Right, Bottom;
}

// Method ORDER matters (COM vtable). Do not reorder or remove members.
[ComImport]
[Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDesktopWallpaper
{
    void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorID,
                      [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);

    [return: MarshalAs(UnmanagedType.LPWStr)]
    string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorID);

    [return: MarshalAs(UnmanagedType.LPWStr)]
    string GetMonitorDevicePathAt(uint monitorIndex);

    uint GetMonitorDevicePathCount();

    NativeRect GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorID);

    void SetBackgroundColor(uint color);
    uint GetBackgroundColor();

    void SetPosition(int position);
    int GetPosition();

    void SetSlideshow(IntPtr items);
    IntPtr GetSlideshow();
    void SetSlideshowOptions(uint options, uint slideshowTick);
    void GetSlideshowOptions(out uint options, out uint slideshowTick);
    void AdvanceSlideshow([MarshalAs(UnmanagedType.LPWStr)] string? monitorID, int direction);
    int GetStatus();
    void Enable([MarshalAs(UnmanagedType.Bool)] bool enable);
}

internal static class DesktopWallpaperFactory
{
    private static readonly Guid Clsid = new("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD");

    public static IDesktopWallpaper Create()
    {
        var type = Type.GetTypeFromCLSID(Clsid)
                   ?? throw new InvalidOperationException("DesktopWallpaper COM class not found.");
        return (IDesktopWallpaper)Activator.CreateInstance(type)!;
    }
}

