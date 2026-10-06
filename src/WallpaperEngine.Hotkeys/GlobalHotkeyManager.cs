using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Serilog;
using WallpaperEngine.Core.Models;

namespace WallpaperEngine.Hotkeys;

/// <summary>
/// Registers global hotkeys on a dedicated STA thread with its own message loop.
/// Independent of the UI thread: hotkeys keep working even if the UI is frozen.
/// Handlers run on the hotkey thread, so they must not block waiting on the UI.
/// </summary>
public sealed class GlobalHotkeyManager : IDisposable
{
    private const uint WM_HOTKEY = 0x0312;
    private const uint WM_APP = 0x8000;
    private const uint WM_QUIT = 0x0012;
    private const uint MOD_NOREPEAT = 0x4000;
    private const int ERROR_HOTKEY_ALREADY_REGISTERED = 1409;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [DllImport("user32.dll")] private static extern int GetMessage(out MSG msg, IntPtr hWnd, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out MSG msg, IntPtr hWnd, uint min, uint max, uint remove);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

    private readonly ConcurrentQueue<Action> _work = new();
    private readonly Dictionary<int, Action> _handlers = new(); // hotkey thread only
    private readonly ManualResetEventSlim _ready = new();
    private readonly Thread _thread;
    private uint _threadId;
    private int _nextId = 1;
    private bool _disposed;

    public GlobalHotkeyManager()
    {
        _thread = new Thread(Loop) { IsBackground = true, Name = "GlobalHotkeyThread" };
        _thread.SetApartmentState(ApartmentState.STA); // wallpaper COM calls need STA
        _thread.Start();
        _ready.Wait();
    }

    private void Loop()
    {
        _threadId = GetCurrentThreadId();
        PeekMessage(out _, IntPtr.Zero, 0, 0, 0); // forces creation of this thread's message queue
        _ready.Set();

        while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            if (msg.message == WM_HOTKEY)
            {
                int id = (int)msg.wParam;
                if (_handlers.TryGetValue(id, out var handler))
                {
                    try { handler(); }
                    catch (Exception ex) { Log.Error(ex, "Hotkey handler threw"); }
                }
            }
            else if (msg.message == WM_APP)
            {
                while (_work.TryDequeue(out var job)) job();
            }
        }

        foreach (var id in _handlers.Keys) UnregisterHotKey(IntPtr.Zero, id);
        _handlers.Clear();
    }

    private T? Invoke<T>(Func<T> func)
    {
        if (_disposed || !_thread.IsAlive) return default;

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _work.Enqueue(() =>
        {
            try { tcs.SetResult(func()); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        PostThreadMessage(_threadId, WM_APP, IntPtr.Zero, IntPtr.Zero);

        return tcs.Task.Wait(TimeSpan.FromSeconds(3)) ? tcs.Task.Result : default;
    }

    public bool TryRegister(HotkeyGesture gesture, Action handler, out int id, out string? error)
    {
        var result = Invoke(() =>
        {
            int newId = _nextId++;
            uint mods = (uint)gesture.Modifiers | MOD_NOREPEAT;
            if (!RegisterHotKey(IntPtr.Zero, newId, mods, (uint)gesture.VirtualKey))
                return (Id: 0, Err: Marshal.GetLastWin32Error());

            _handlers[newId] = handler;
            return (Id: newId, Err: 0);
        });

        id = result.Id;
        if (id != 0) { error = null; return true; }

        error = result.Err switch
        {
            0 => "The hotkey thread did not respond.",
            ERROR_HOTKEY_ALREADY_REGISTERED => "That key combination is already used by another program. Pick a different one.",
            _ => $"Windows refused that hotkey (error {result.Err})."
        };
        return false;
    }

    public void Unregister(int id)
    {
        Invoke(() =>
        {
            UnregisterHotKey(IntPtr.Zero, id);
            _handlers.Remove(id);
            return true;
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(1000);
    }
}