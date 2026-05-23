using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using SandboxSync.Models;

namespace SandboxSync.Interop;

public static class Win32Interop
{
    public static IReadOnlyList<WindowInfo> EnumerateTopLevelWindows()
    {
        var results = new List<WindowInfo>();

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!IsCandidateWindow(hWnd))
            {
                return true;
            }

            results.Add(CreateWindowInfo(hWnd));
            return true;
        }, IntPtr.Zero);

        return results
            .OrderBy(w => w.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool IsCandidateWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero || !NativeMethods.IsWindow(hWnd))
        {
            return false;
        }

        if (!NativeMethods.IsWindowVisible(hWnd))
        {
            return false;
        }

        var length = NativeMethods.GetWindowTextLength(hWnd);
        if (length <= 0)
        {
            return false;
        }

        var className = GetClassName(hWnd);
        if (className is "Shell_TrayWnd" or "Progman" or "WorkerW")
        {
            return false;
        }

        return true;
    }

    public static WindowInfo CreateWindowInfo(IntPtr hWnd)
    {
        NativeMethods.GetWindowThreadProcessId(hWnd, out var pid);
        NativeMethods.GetWindowRect(hWnd, out var windowRect);
        NativeMethods.GetClientRect(hWnd, out var clientRect);

        return new WindowInfo
        {
            Handle = hWnd,
            ProcessId = (int)pid,
            Title = GetWindowTitle(hWnd),
            ClassName = GetClassName(hWnd),
            ProcessName = GetProcessName((int)pid),
            ExecutablePath = GetProcessImagePath((int)pid),
            Left = windowRect.Left,
            Top = windowRect.Top,
            Width = windowRect.Width,
            Height = windowRect.Height,
            ClientWidth = clientRect.Width,
            ClientHeight = clientRect.Height,
            IsVisible = NativeMethods.IsWindowVisible(hWnd),
            IsMinimized = NativeMethods.IsIconic(hWnd)
        };
    }

    public static bool IsWindowAlive(IntPtr hWnd) =>
        hWnd != IntPtr.Zero && NativeMethods.IsWindow(hWnd);

    public static string GetWindowTitle(IntPtr hWnd)
    {
        var length = NativeMethods.GetWindowTextLength(hWnd);
        if (length <= 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder(length + 1);
        _ = NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public static string GetClassName(IntPtr hWnd)
    {
        var sb = new StringBuilder(256);
        _ = NativeMethods.GetClassName(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public static IntPtr GetRootWindowFromPoint(POINT screenPoint)
    {
        var hwnd = NativeMethods.WindowFromPoint(screenPoint);
        if (hwnd == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        return NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);
    }

    public static POINT ScreenToClientDpi(IntPtr hwnd, POINT screenPoint)
    {
        var client = screenPoint;
        _ = NativeMethods.ScreenToClient(hwnd, ref client);
        return client;
    }

    public static POINT ClientToScreenDpi(IntPtr hwnd, POINT clientPoint)
    {
        var screen = clientPoint;
        _ = NativeMethods.ClientToScreen(hwnd, ref screen);
        return screen;
    }

    public static RECT GetClientRect(IntPtr hwnd)
    {
        NativeMethods.GetClientRect(hwnd, out var rect);
        return rect;
    }

    public static nint PackMouseLParam(int x, int y) =>
        (nint)((y << 16) | (x & 0xFFFF));

    public static string GetProcessName(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    public static string GetProcessImagePath(int processId)
    {
        var handle = NativeMethods.OpenProcess(
            ProcessAccessFlags.PROCESS_QUERY_LIMITED_INFORMATION,
            false,
            (uint)processId);

        if (handle == IntPtr.Zero)
        {
            return string.Empty;
        }

        try
        {
            var sb = new StringBuilder(1024);
            var size = sb.Capacity;
            if (NativeMethods.QueryFullProcessImageName(handle, 0, sb, ref size))
            {
                return sb.ToString();
            }
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }

        return string.Empty;
    }

    public static int GetParentProcessId(int processId)
    {
        var handle = NativeMethods.OpenProcess(
            ProcessAccessFlags.PROCESS_QUERY_LIMITED_INFORMATION,
            false,
            (uint)processId);

        if (handle == IntPtr.Zero)
        {
            return 0;
        }

        try
        {
            var pbi = new PROCESS_BASIC_INFORMATION();
            const int ProcessBasicInformation = 0;
            var status = NativeMethods.NtQueryInformationProcess(
                handle,
                ProcessBasicInformation,
                ref pbi,
                Marshal.SizeOf<PROCESS_BASIC_INFORMATION>(),
                out _);

            if (status != 0)
            {
                return 0;
            }

            return (int)pbi.InheritedFromUniqueProcessId;
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }
    }

    public static bool ProcessHasModule(int processId, string moduleName)
    {
        var handle = NativeMethods.OpenProcess(
            ProcessAccessFlags.PROCESS_QUERY_INFORMATION | ProcessAccessFlags.PROCESS_VM_READ,
            false,
            (uint)processId);

        if (handle == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var modules = new IntPtr[512];
            if (!NativeMethods.EnumProcessModules(handle, modules, modules.Length * IntPtr.Size, out var needed))
            {
                return false;
            }

            var count = needed / IntPtr.Size;
            var sb = new StringBuilder(260);
            for (var i = 0; i < count; i++)
            {
                sb.Clear();
                if (NativeMethods.GetModuleBaseName(handle, modules[i], sb, sb.Capacity) == 0)
                {
                    continue;
                }

                if (sb.ToString().Equals(moduleName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }

        return false;
    }

    public static bool IsSameProcess(IntPtr hwnd) =>
        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid) != 0 &&
        pid == (uint)Process.GetCurrentProcess().Id;
}
