using SandboxSync.Interop;
using SandboxSync.Services;

namespace SandboxSync.Core;

/// <summary>
/// Simple click replicator. Posts a complete click (DOWN+UP) to each target
/// HWND via PostMessage. No cursor movement, no foreground / focus changes.
/// </summary>
public sealed class InputReplicator
{
    private readonly LoggingService _logger;

    public InputReplicator(LoggingService logger) => _logger = logger;

    public void ResetTargetModes()
    {
    }

    public void Configure(bool _)
    {
    }

    public bool ReplicateClickPair(IntPtr targetHwnd, WindowMessage upMessage, MappedPoint mapped)
    {
        if (!Win32Interop.IsWindowAlive(targetHwnd))
        {
            return false;
        }

        var (downMsg, upMsg, wParam) = upMessage switch
        {
            WindowMessage.WM_LBUTTONUP => (WindowMessage.WM_LBUTTONDOWN, WindowMessage.WM_LBUTTONUP, (nint)1),
            WindowMessage.WM_RBUTTONUP => (WindowMessage.WM_RBUTTONDOWN, WindowMessage.WM_RBUTTONUP, (nint)2),
            WindowMessage.WM_MBUTTONUP => (WindowMessage.WM_MBUTTONDOWN, WindowMessage.WM_MBUTTONUP, (nint)0x10),
            _ => (WindowMessage.WM_NULL, WindowMessage.WM_NULL, (nint)0)
        };

        if (downMsg == WindowMessage.WM_NULL)
        {
            return false;
        }

        var lParam = Win32Interop.PackMouseLParam(mapped.TargetClientPoint.X, mapped.TargetClientPoint.Y);
        NativeMethods.PostMessage(targetHwnd, (uint)downMsg, wParam, lParam);
        NativeMethods.PostMessage(targetHwnd, (uint)upMsg, 0, lParam);
        return true;
    }
}
