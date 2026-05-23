using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using SandboxSync.Interop;

namespace SandboxSync.Core;

public sealed class LowLevelMouseHook : IDisposable
{
    private readonly ConcurrentQueue<MouseHookEvent> _queue = new();
    private readonly ManualResetEventSlim _stopped = new(false);
    private Thread? _hookThread;
    private Dispatcher? _hookDispatcher;
    private IntPtr _hookHandle = IntPtr.Zero;
    private NativeMethods.LowLevelHookProc? _proc;
    private volatile bool _isRunning;

    public event EventHandler<MouseHookEvent>? MouseEvent;

    public void Start()
    {
        if (_isRunning)
        {
            return;
        }

        _isRunning = true;
        _stopped.Reset();

        _hookThread = new Thread(HookThreadMain)
        {
            IsBackground = true,
            Name = "SandboxSync.MouseHook"
        };
        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.Start();

        Task.Run(ProcessQueueLoop);
    }

    public void Stop()
    {
        if (!_isRunning)
        {
            return;
        }

        _isRunning = false;
        _hookDispatcher?.BeginInvokeShutdown(DispatcherPriority.Send);
        _stopped.Wait(TimeSpan.FromSeconds(2));
    }

    private void HookThreadMain()
    {
        _hookDispatcher = Dispatcher.CurrentDispatcher;
        _proc = HookCallback;

        _hookHandle = NativeMethods.SetWindowsHookEx(
            HookType.WH_MOUSE_LL,
            _proc,
            NativeMethods.GetModuleHandle(null),
            0);

        if (_hookHandle == IntPtr.Zero)
        {
            _isRunning = false;
            _stopped.Set();
            return;
        }

        Dispatcher.Run();

        if (_hookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }

        _stopped.Set();
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= (int)HookCode.HC_ACTION && _isRunning)
        {
            var msg = (WindowMessage)(uint)wParam;
            if (IsTrackedMessage(msg))
            {
                var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                _queue.Enqueue(new MouseHookEvent
                {
                    Message = msg,
                    ScreenPoint = hookStruct.pt,
                    MouseData = hookStruct.mouseData,
                    Flags = hookStruct.flags,
                    Time = hookStruct.time,
                    ExtraInfo = hookStruct.dwExtraInfo
                });
            }
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private async Task ProcessQueueLoop()
    {
        while (_isRunning || !_queue.IsEmpty)
        {
            while (_queue.TryDequeue(out var evt))
            {
                MouseEvent?.Invoke(this, evt);
            }

            await Task.Delay(1).ConfigureAwait(false);
        }
    }

    private static bool IsTrackedMessage(WindowMessage msg) =>
        msg is WindowMessage.WM_LBUTTONDOWN
            or WindowMessage.WM_LBUTTONUP
            or WindowMessage.WM_LBUTTONDBLCLK
            or WindowMessage.WM_RBUTTONDOWN
            or WindowMessage.WM_RBUTTONUP
            or WindowMessage.WM_RBUTTONDBLCLK
            or WindowMessage.WM_MBUTTONDOWN
            or WindowMessage.WM_MBUTTONUP
            or WindowMessage.WM_MBUTTONDBLCLK
            or WindowMessage.WM_MOUSEMOVE
            or WindowMessage.WM_MOUSEWHEEL
            or WindowMessage.WM_XBUTTONDOWN
            or WindowMessage.WM_XBUTTONUP;

    public void Dispose()
    {
        Stop();
        _stopped.Dispose();
    }
}
