using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using SandboxSync.Interop;

namespace SandboxSync.Core;

public sealed class LowLevelKeyboardHook : IDisposable
{
    private readonly ConcurrentQueue<KeyboardHookEvent> _queue = new();
    private readonly ManualResetEventSlim _stopped = new(false);
    private Thread? _hookThread;
    private Dispatcher? _hookDispatcher;
    private IntPtr _hookHandle = IntPtr.Zero;
    private NativeMethods.LowLevelHookProc? _proc;
    private volatile bool _isRunning;

    public event EventHandler<KeyboardHookEvent>? KeyboardEvent;

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
            Name = "SandboxSync.KeyboardHook"
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
            HookType.WH_KEYBOARD_LL,
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
            var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            _queue.Enqueue(new KeyboardHookEvent
            {
                VirtualKey = hookStruct.vkCode,
                ScanCode = hookStruct.scanCode,
                Flags = hookStruct.flags,
                Time = hookStruct.time,
                ExtraInfo = hookStruct.dwExtraInfo
            });
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private async Task ProcessQueueLoop()
    {
        while (_isRunning || !_queue.IsEmpty)
        {
            while (_queue.TryDequeue(out var evt))
            {
                KeyboardEvent?.Invoke(this, evt);
            }

            await Task.Delay(1).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        Stop();
        _stopped.Dispose();
    }
}
