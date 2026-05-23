using SandboxSync.Interop;
using SandboxSync.Services;

namespace SandboxSync.Core;

/// <summary>
/// Simple click replicator. Routes the click to the deepest child HWND at the
/// mapped point in the target window and posts WM_*BUTTONDOWN/UP to that child.
/// No cursor movement, no foreground / focus changes.
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

        // Walk down to the deepest child HWND at the mapped point.
        // Most apps host their real input target (button, render canvas, web view)
        // as a child of the top-level window. Posting to the top-level alone
        // often does nothing — we need to deliver to that child.
        var childHwnd = ResolveDeepestChild(targetHwnd, mapped.TargetScreenPoint);
        var childClientPoint = Win32Interop.ScreenToClientDpi(childHwnd, mapped.TargetScreenPoint);
        var lParam = Win32Interop.PackMouseLParam(childClientPoint.X, childClientPoint.Y);

        // Move-then-click sequence so apps update hover/hit-test state first.
        NativeMethods.PostMessage(childHwnd, (uint)WindowMessage.WM_MOUSEMOVE, 0, lParam);
        NativeMethods.PostMessage(childHwnd, (uint)downMsg, wParam, lParam);
        NativeMethods.PostMessage(childHwnd, (uint)upMsg, 0, lParam);
        return true;
    }

    private static IntPtr ResolveDeepestChild(IntPtr parent, POINT screenPoint)
    {
        var current = parent;
        for (var depth = 0; depth < 16; depth++)
        {
            var clientPoint = Win32Interop.ScreenToClientDpi(current, screenPoint);
            var child = NativeMethods.ChildWindowFromPointEx(
                current,
                clientPoint,
                NativeMethods.CWP_SKIPINVISIBLE | NativeMethods.CWP_SKIPDISABLED | NativeMethods.CWP_SKIPTRANSPARENT);

            if (child == IntPtr.Zero || child == current)
            {
                return current;
            }

            current = child;
        }

        return current;
    }
}
