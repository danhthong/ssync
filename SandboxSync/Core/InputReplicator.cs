using System.Runtime.InteropServices;
using SandboxSync.Interop;
using SandboxSync.Models;
using SandboxSync.Services;

namespace SandboxSync.Core;

/// <summary>
/// Replicates a click into a single target. Two strategies:
///   PostMessage — posts WM_*BUTTONDOWN/UP to the deepest child HWND at the
///   mapped point. Real-time, no focus switching. Works for web/browser games
///   and normal Win32 apps. Games that read Raw Input ignore these messages.
///
///   SendInput — brings target to foreground, moves cursor, fires SendInput.
///   Required for Raw Input / DirectInput games. Causes per-target focus flicker.
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

    public bool ReplicateClickPair(
        IntPtr targetHwnd,
        WindowMessage upMessage,
        MappedPoint mapped,
        InputReplicationMode mode)
    {
        if (!Win32Interop.IsWindowAlive(targetHwnd))
        {
            return false;
        }

        return mode switch
        {
            InputReplicationMode.PostMessage => PostClick(targetHwnd, upMessage, mapped),
            _ => SendInputClick(targetHwnd, upMessage, mapped)
        };
    }

    private static bool PostClick(IntPtr targetHwnd, WindowMessage upMessage, MappedPoint mapped)
    {
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

        var childHwnd = ResolveDeepestChild(targetHwnd, mapped.TargetScreenPoint);
        var childClientPoint = Win32Interop.ScreenToClientDpi(childHwnd, mapped.TargetScreenPoint);
        var lParam = Win32Interop.PackMouseLParam(childClientPoint.X, childClientPoint.Y);

        NativeMethods.PostMessage(childHwnd, (uint)WindowMessage.WM_MOUSEMOVE, 0, lParam);
        NativeMethods.PostMessage(childHwnd, (uint)downMsg, wParam, lParam);
        NativeMethods.PostMessage(childHwnd, (uint)upMsg, 0, lParam);
        return true;
    }

    private bool SendInputClick(IntPtr targetHwnd, WindowMessage upMessage, MappedPoint mapped)
    {
        var (downFlag, upFlag) = upMessage switch
        {
            WindowMessage.WM_LBUTTONUP => (MouseEventFlags.MOUSEEVENTF_LEFTDOWN, MouseEventFlags.MOUSEEVENTF_LEFTUP),
            WindowMessage.WM_RBUTTONUP => (MouseEventFlags.MOUSEEVENTF_RIGHTDOWN, MouseEventFlags.MOUSEEVENTF_RIGHTUP),
            WindowMessage.WM_MBUTTONUP => (MouseEventFlags.MOUSEEVENTF_MIDDLEDOWN, MouseEventFlags.MOUSEEVENTF_MIDDLEUP),
            _ => ((MouseEventFlags)0, (MouseEventFlags)0)
        };

        if (downFlag == 0)
        {
            return false;
        }

        if ((NativeMethods.GetWindowLong(targetHwnd, NativeMethods.GWL_STYLE) & NativeMethods.WS_MINIMIZE) != 0)
        {
            NativeMethods.ShowWindow(targetHwnd, NativeMethods.SW_RESTORE);
        }

        if (!ForceForeground(targetHwnd))
        {
            _logger.Warning($"Failed to bring 0x{targetHwnd.ToInt64():X} to foreground; click skipped.");
            return false;
        }

        NativeMethods.SetCursorPos(mapped.TargetScreenPoint.X, mapped.TargetScreenPoint.Y);
        Thread.Sleep(5);

        var inputs = new[]
        {
            CreateMouseInput(downFlag),
            CreateMouseInput(upFlag)
        };

        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        return sent == inputs.Length;
    }

    public void RestoreForeground(IntPtr masterHwnd, POINT cursorPos, InputReplicationMode mode)
    {
        if (mode == InputReplicationMode.PostMessage)
        {
            return; // nothing to restore — we never moved focus / cursor.
        }

        if (masterHwnd != IntPtr.Zero)
        {
            ForceForeground(masterHwnd);
        }

        NativeMethods.SetCursorPos(cursorPos.X, cursorPos.Y);
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

    private static bool ForceForeground(IntPtr targetHwnd)
    {
        var current = NativeMethods.GetForegroundWindow();
        if (current == targetHwnd)
        {
            return true;
        }

        var currentThread = NativeMethods.GetCurrentThreadId();
        var targetThread = NativeMethods.GetWindowThreadProcessId(targetHwnd, out _);
        var foregroundThread = current != IntPtr.Zero
            ? NativeMethods.GetWindowThreadProcessId(current, out _)
            : 0u;

        var attachedTarget = false;
        var attachedForeground = false;

        if (targetThread != 0 && targetThread != currentThread)
        {
            attachedTarget = NativeMethods.AttachThreadInput(currentThread, targetThread, true);
        }
        if (foregroundThread != 0 && foregroundThread != currentThread && foregroundThread != targetThread)
        {
            attachedForeground = NativeMethods.AttachThreadInput(currentThread, foregroundThread, true);
        }

        try
        {
            NativeMethods.keybd_event(NativeMethods.VK_MENU, 0, 0, (nuint)FeedbackGuard.SyncTag);
            NativeMethods.BringWindowToTop(targetHwnd);
            NativeMethods.SetForegroundWindow(targetHwnd);
            NativeMethods.keybd_event(NativeMethods.VK_MENU, 0, NativeMethods.KEYEVENTF_KEYUP, (nuint)FeedbackGuard.SyncTag);

            for (var i = 0; i < 30; i++)
            {
                if (NativeMethods.GetForegroundWindow() == targetHwnd)
                {
                    return true;
                }
                Thread.Sleep(5);
            }

            return NativeMethods.GetForegroundWindow() == targetHwnd;
        }
        finally
        {
            if (attachedTarget)
            {
                NativeMethods.AttachThreadInput(currentThread, targetThread, false);
            }
            if (attachedForeground)
            {
                NativeMethods.AttachThreadInput(currentThread, foregroundThread, false);
            }
        }
    }

    private static INPUT CreateMouseInput(MouseEventFlags flags) => new()
    {
        type = (uint)InputType.INPUT_MOUSE,
        U = new InputUnion
        {
            mi = new MOUSEINPUT
            {
                dwFlags = (uint)flags,
                dwExtraInfo = FeedbackGuard.SyncTag
            }
        }
    };
}
