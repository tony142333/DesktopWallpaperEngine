namespace WallpaperEngine.Core.Models;

/// <summary>Values match the native MOD_* flags. Do not renumber.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8
}

public sealed class HotkeyGesture
{
    public HotkeyModifiers Modifiers { get; set; }
    public int VirtualKey { get; set; }

    public HotkeyGesture() { } // needed for JSON

    public HotkeyGesture(HotkeyModifiers modifiers, int virtualKey)
    {
        Modifiers = modifiers;
        VirtualKey = virtualKey;
    }
}