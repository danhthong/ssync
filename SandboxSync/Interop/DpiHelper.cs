namespace SandboxSync.Interop;

/// <summary>
/// DPI awareness helpers for multi-monitor mixed scaling.
/// </summary>
public static class DpiHelper
{
    private const double DefaultDpi = 96.0;

    public static void EnablePerMonitorV2()
    {
        try
        {
            NativeMethods.SetProcessDpiAwarenessContext((nint)DpiAwarenessContext.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        }
        catch
        {
            // Older Windows builds may not support V2; manifest still requests pm awareness.
        }
    }

    public static double GetDpiScale(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return 1.0;
        }

        var dpi = NativeMethods.GetDpiForWindow(hwnd);
        if (dpi == 0)
        {
            return 1.0;
        }

        return dpi / DefaultDpi;
    }

    public static POINT ScalePointToDpi(POINT point, double fromScale, double toScale)
    {
        if (Math.Abs(fromScale - toScale) < 0.001)
        {
            return point;
        }

        var ratio = toScale / fromScale;
        return new POINT(
            (int)Math.Round(point.X * ratio),
            (int)Math.Round(point.Y * ratio));
    }
}
