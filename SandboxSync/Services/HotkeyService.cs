using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using SandboxSync.Interop;
using SandboxSync.Models;

namespace SandboxSync.Services;

public enum HotkeyAction
{
    Start,
    Stop,
    Pause
}

public sealed class HotkeyService : IDisposable
{
    private HwndSource? _source;
    private IntPtr _hwnd;
    private bool _registered;

    public event EventHandler<HotkeyAction>? HotkeyPressed;

    public void Initialize()
    {
        if (_registered)
        {
            return;
        }

        var parameters = new HwndSourceParameters("SandboxSyncHotkeySink")
        {
            Width = 0,
            Height = 0,
            PositionX = 0,
            PositionY = 0,
            WindowStyle = 0,
            ParentWindow = new IntPtr(-3) // HWND_MESSAGE
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
        _hwnd = _source.Handle;
        _registered = true;
    }

    public bool Register(SyncProfile profile)
    {
        Initialize();

        UnregisterAll();

        var okStart = NativeMethods.RegisterHotKey(
            _hwnd,
            NativeMethods.HOTKEY_ID_START,
            profile.StartHotkey.Modifiers,
            profile.StartHotkey.VirtualKey);

        var okStop = NativeMethods.RegisterHotKey(
            _hwnd,
            NativeMethods.HOTKEY_ID_STOP,
            profile.StopHotkey.Modifiers,
            profile.StopHotkey.VirtualKey);

        var okPause = NativeMethods.RegisterHotKey(
            _hwnd,
            NativeMethods.HOTKEY_ID_PAUSE,
            profile.PauseHotkey.Modifiers,
            profile.PauseHotkey.VirtualKey);

        return okStart && okStop && okPause;
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if ((uint)msg == (uint)WindowMessage.WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            var action = id switch
            {
                NativeMethods.HOTKEY_ID_START => HotkeyAction.Start,
                NativeMethods.HOTKEY_ID_STOP => HotkeyAction.Stop,
                NativeMethods.HOTKEY_ID_PAUSE => HotkeyAction.Pause,
                _ => (HotkeyAction?)null
            };

            if (action.HasValue)
            {
                HotkeyPressed?.Invoke(this, action.Value);
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    private void UnregisterAll()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.UnregisterHotKey(_hwnd, NativeMethods.HOTKEY_ID_START);
        NativeMethods.UnregisterHotKey(_hwnd, NativeMethods.HOTKEY_ID_STOP);
        NativeMethods.UnregisterHotKey(_hwnd, NativeMethods.HOTKEY_ID_PAUSE);
    }

    public void Dispose()
    {
        UnregisterAll();
        _source?.RemoveHook(WndProc);
        _source?.Dispose();
        _source = null;
        _hwnd = IntPtr.Zero;
        _registered = false;
    }
}
