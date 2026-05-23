using System.Runtime.InteropServices;
using SandboxSync.Interop;
using SandboxSync.Services;

namespace SandboxSync.Core;

/// <summary>
/// Replicates a click into each target via SendInput. For apps that read
/// Raw Input / DirectInput (most games) PostMessage is silently ignored —
/// SendInput is the only user-mode path that actually works, and it requires
/// the target window to be the foreground window when the input is delivered.
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
        // Tiny breathing room for the OS to update hit-testing state after a focus change.
        Thread.Sleep(5);

        var inputs = new[]
        {
            CreateMouseInput(downFlag),
            CreateMouseInput(upFlag)
        };

        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        return sent == inputs.Length;
    }

    /// <summary>
    /// Robustly bring a window to the foreground across UAC / integrity levels.
    /// Combines AttachThreadInput, BringWindowToTop, SetForegroundWindow, and SetFocus,
    /// then actively waits until GetForegroundWindow == target.
    /// </summary>
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
            // Alt-trick still required for SetForegroundWindow on Win10/11.
            NativeMethods.keybd_event(NativeMethods.VK_MENU, 0, 0, (nuint)FeedbackGuard.SyncTag);
            NativeMethods.BringWindowToTop(targetHwnd);
            NativeMethods.SetForegroundWindow(targetHwnd);
            NativeMethods.keybd_event(NativeMethods.VK_MENU, 0, NativeMethods.KEYEVENTF_KEYUP, (nuint)FeedbackGuard.SyncTag);

            // Wait for the foreground swap to actually complete (up to ~150 ms).
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

    /// <summary>
    /// Called once after a click batch to restore the master as foreground
    /// and put the cursor back. Doing this once per batch (rather than per
    /// target) keeps flicker minimal even with many targets.
    /// </summary>
    public void RestoreForeground(IntPtr masterHwnd, POINT cursorPos)
    {
        if (masterHwnd != IntPtr.Zero)
        {
            ForceForeground(masterHwnd);
        }

        NativeMethods.SetCursorPos(cursorPos.X, cursorPos.Y);
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
