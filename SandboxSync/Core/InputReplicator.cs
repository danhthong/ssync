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

        // Alt-key trick to satisfy Win10/11 SetForegroundWindow restrictions.
        NativeMethods.keybd_event(NativeMethods.VK_MENU, 0, 0, (nuint)FeedbackGuard.SyncTag);
        NativeMethods.SetForegroundWindow(targetHwnd);
        NativeMethods.keybd_event(NativeMethods.VK_MENU, 0, NativeMethods.KEYEVENTF_KEYUP, (nuint)FeedbackGuard.SyncTag);

        NativeMethods.SetCursorPos(mapped.TargetScreenPoint.X, mapped.TargetScreenPoint.Y);

        // Send DOWN and UP atomically so the OS delivers them as one fast click,
        // not a hold-then-release. Any gap risks the target registering a drag.
        var inputs = new[]
        {
            CreateMouseInput(downFlag),
            CreateMouseInput(upFlag)
        };

        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        return sent == inputs.Length;
    }

    /// <summary>
    /// Called once after a click batch to restore the master as foreground
    /// and put the cursor back. Doing this once per batch (rather than per
    /// target) keeps flicker minimal even with many targets.
    /// </summary>
    public void RestoreForeground(IntPtr masterHwnd, POINT cursorPos)
    {
        if (masterHwnd != IntPtr.Zero && NativeMethods.GetForegroundWindow() != masterHwnd)
        {
            NativeMethods.keybd_event(NativeMethods.VK_MENU, 0, 0, (nuint)FeedbackGuard.SyncTag);
            NativeMethods.SetForegroundWindow(masterHwnd);
            NativeMethods.keybd_event(NativeMethods.VK_MENU, 0, NativeMethods.KEYEVENTF_KEYUP, (nuint)FeedbackGuard.SyncTag);
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
