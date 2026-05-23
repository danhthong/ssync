using System.Collections.Concurrent;
using SandboxSync.Interop;
using SandboxSync.Models;
using SandboxSync.Services;

namespace SandboxSync.Core;

public sealed class InputReplicator
{
    private readonly LoggingService _logger;
    private readonly ConcurrentDictionary<IntPtr, InputReplicationMode> _perTargetMode = new();

    public InputReplicator(LoggingService logger) => _logger = logger;

    public void ResetTargetModes() => _perTargetMode.Clear();

    public bool ReplicateMouse(
        IntPtr targetHwnd,
        WindowMessage message,
        MappedPoint mapped,
        InputReplicationMode preferredMode,
        uint mouseData = 0)
    {
        if (!Win32Interop.IsWindowAlive(targetHwnd))
        {
            return false;
        }

        var mode = _perTargetMode.GetOrAdd(targetHwnd, preferredMode);

        var success = mode switch
        {
            InputReplicationMode.PostMessage => TryPostMessage(targetHwnd, message, mapped, mouseData),
            InputReplicationMode.SendInput => TrySendInput(targetHwnd, message, mapped, mouseData),
            _ => TryPostMessage(targetHwnd, message, mapped, mouseData) ||
                 TrySendInput(targetHwnd, message, mapped, mouseData)
        };

        if (!success && mode == InputReplicationMode.Hybrid)
        {
            if (TrySendInput(targetHwnd, message, mapped, mouseData))
            {
                _perTargetMode[targetHwnd] = InputReplicationMode.SendInput;
                _logger.Warning($"Target {targetHwnd} downgraded to SendInput for this session.");
                return true;
            }
        }

        if (!success && mode == InputReplicationMode.PostMessage)
        {
            if (TrySendInput(targetHwnd, message, mapped, mouseData))
            {
                _perTargetMode[targetHwnd] = InputReplicationMode.SendInput;
                return true;
            }
        }

        return success;
    }

    public bool ReplicateKey(IntPtr targetHwnd, uint virtualKey, bool keyUp, InputReplicationMode mode)
    {
        if (!Win32Interop.IsWindowAlive(targetHwnd))
        {
            return false;
        }

        if (mode == InputReplicationMode.SendInput)
        {
            return SendKeyViaInput(virtualKey, keyUp);
        }

        var msg = keyUp ? WindowMessage.WM_KEYUP : WindowMessage.WM_KEYDOWN;
        var lParam = BuildKeyLParam(virtualKey, keyUp);
        var posted = NativeMethods.PostMessage(targetHwnd, (uint)msg, (nint)virtualKey, lParam);
        if (posted)
        {
            return true;
        }

        return SendKeyViaInput(virtualKey, keyUp);
    }

    private bool TryPostMessage(IntPtr targetHwnd, WindowMessage message, MappedPoint mapped, uint mouseData)
    {
        var clientMsg = message;
        if (message == WindowMessage.WM_MOUSEWHEEL || message == WindowMessage.WM_XBUTTONDOWN || message == WindowMessage.WM_XBUTTONUP)
        {
            clientMsg = message;
        }

        var wParam = BuildMouseWParam(message, mouseData);
        var lParam = Win32Interop.PackMouseLParam(mapped.TargetClientPoint.X, mapped.TargetClientPoint.Y);
        return NativeMethods.PostMessage(targetHwnd, (uint)clientMsg, wParam, lParam);
    }

    private bool TrySendInput(IntPtr targetHwnd, WindowMessage message, MappedPoint mapped, uint mouseData)
    {
        NativeMethods.GetCursorPos(out var savedPos);

        try
        {
            NativeMethods.SetCursorPos(mapped.TargetScreenPoint.X, mapped.TargetScreenPoint.Y);

            var inputs = BuildMouseInputs(message, mouseData);
            if (inputs.Length == 0)
            {
                return true;
            }

            var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, System.Runtime.InteropServices.Marshal.SizeOf<INPUT>());
            return sent == inputs.Length;
        }
        finally
        {
            NativeMethods.SetCursorPos(savedPos.X, savedPos.Y);
        }
    }

    private static INPUT[] BuildMouseInputs(WindowMessage message, uint mouseData)
    {
        return message switch
        {
            WindowMessage.WM_LBUTTONDOWN => [CreateMouseInput(MouseEventFlags.MOUSEEVENTF_LEFTDOWN)],
            WindowMessage.WM_LBUTTONUP => [CreateMouseInput(MouseEventFlags.MOUSEEVENTF_LEFTUP)],
            WindowMessage.WM_LBUTTONDBLCLK =>
            [
                CreateMouseInput(MouseEventFlags.MOUSEEVENTF_LEFTDOWN),
                CreateMouseInput(MouseEventFlags.MOUSEEVENTF_LEFTUP),
                CreateMouseInput(MouseEventFlags.MOUSEEVENTF_LEFTDOWN),
                CreateMouseInput(MouseEventFlags.MOUSEEVENTF_LEFTUP)
            ],
            WindowMessage.WM_RBUTTONDOWN => [CreateMouseInput(MouseEventFlags.MOUSEEVENTF_RIGHTDOWN)],
            WindowMessage.WM_RBUTTONUP => [CreateMouseInput(MouseEventFlags.MOUSEEVENTF_RIGHTUP)],
            WindowMessage.WM_RBUTTONDBLCLK =>
            [
                CreateMouseInput(MouseEventFlags.MOUSEEVENTF_RIGHTDOWN),
                CreateMouseInput(MouseEventFlags.MOUSEEVENTF_RIGHTUP),
                CreateMouseInput(MouseEventFlags.MOUSEEVENTF_RIGHTDOWN),
                CreateMouseInput(MouseEventFlags.MOUSEEVENTF_RIGHTUP)
            ],
            WindowMessage.WM_MBUTTONDOWN => [CreateMouseInput(MouseEventFlags.MOUSEEVENTF_MIDDLEDOWN)],
            WindowMessage.WM_MBUTTONUP => [CreateMouseInput(MouseEventFlags.MOUSEEVENTF_MIDDLEUP)],
            WindowMessage.WM_MBUTTONDBLCLK =>
            [
                CreateMouseInput(MouseEventFlags.MOUSEEVENTF_MIDDLEDOWN),
                CreateMouseInput(MouseEventFlags.MOUSEEVENTF_MIDDLEUP),
                CreateMouseInput(MouseEventFlags.MOUSEEVENTF_MIDDLEDOWN),
                CreateMouseInput(MouseEventFlags.MOUSEEVENTF_MIDDLEUP)
            ],
            WindowMessage.WM_MOUSEMOVE => [CreateMouseInput(MouseEventFlags.MOUSEEVENTF_MOVE)],
            WindowMessage.WM_MOUSEWHEEL => [CreateMouseInput(MouseEventFlags.MOUSEEVENTF_WHEEL, mouseData)],
            _ => []
        };
    }

    private static INPUT CreateMouseInput(MouseEventFlags flags, uint mouseData = 0) => new()
    {
        type = (uint)InputType.INPUT_MOUSE,
        U = new InputUnion
        {
            mi = new MOUSEINPUT
            {
                dwFlags = (uint)flags,
                mouseData = mouseData,
                dwExtraInfo = FeedbackGuard.SyncTag
            }
        }
    };

    private static bool SendKeyViaInput(uint virtualKey, bool keyUp)
    {
        var inputs = new[]
        {
            new INPUT
            {
                type = (uint)InputType.INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = (ushort)virtualKey,
                        dwFlags = keyUp ? (uint)KeyboardEventFlags.KEYEVENTF_KEYUP : 0,
                        dwExtraInfo = FeedbackGuard.SyncTag
                    }
                }
            }
        };

        return NativeMethods.SendInput(1, inputs, System.Runtime.InteropServices.Marshal.SizeOf<INPUT>()) == 1;
    }

    private static nint BuildMouseWParam(WindowMessage message, uint mouseData) =>
        message switch
        {
            WindowMessage.WM_LBUTTONDOWN or WindowMessage.WM_LBUTTONUP or WindowMessage.WM_LBUTTONDBLCLK => 1,
            WindowMessage.WM_RBUTTONDOWN or WindowMessage.WM_RBUTTONUP or WindowMessage.WM_RBUTTONDBLCLK => 2,
            WindowMessage.WM_MBUTTONDOWN or WindowMessage.WM_MBUTTONUP or WindowMessage.WM_MBUTTONDBLCLK => 0x10,
            WindowMessage.WM_MOUSEWHEEL => (nint)(((int)mouseData << 16)),
            _ => 0
        };

    private static nint BuildKeyLParam(uint vk, bool keyUp)
    {
        var repeat = 1;
        var scan = 0;
        var flags = keyUp ? 1 << 31 | 1 << 30 : 0;
        return (nint)(repeat | (scan << 16) | flags);
    }
}
