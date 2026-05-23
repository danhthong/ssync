using SandboxSync.Interop;
using SandboxSync.Models;

namespace SandboxSync.Core;

public sealed class MappedPoint
{
    public required IntPtr TargetHwnd { get; init; }

    public required POINT TargetClientPoint { get; init; }

    public required POINT TargetScreenPoint { get; init; }

    public required double NormalizedX { get; init; }

    public required double NormalizedY { get; init; }
}

public sealed class CoordinateMapper
{
    public bool TryMap(
        IntPtr masterHwnd,
        POINT screenPoint,
        IntPtr targetHwnd,
        IReadOnlyList<ExcludeRegion> excludeRegions,
        out MappedPoint mapped)
    {
        mapped = null!;

        if (!Win32Interop.IsWindowAlive(masterHwnd) || !Win32Interop.IsWindowAlive(targetHwnd))
        {
            return false;
        }

        var masterClient = Win32Interop.ScreenToClientDpi(masterHwnd, screenPoint);
        var masterRect = Win32Interop.GetClientRect(masterHwnd);

        if (masterRect.Width <= 0 || masterRect.Height <= 0)
        {
            return false;
        }

        var nx = (double)masterClient.X / masterRect.Width;
        var ny = (double)masterClient.Y / masterRect.Height;

        if (excludeRegions.Any(r => r.ContainsNormalized(nx, ny)))
        {
            return false;
        }

        var targetRect = Win32Interop.GetClientRect(targetHwnd);
        if (targetRect.Width <= 0 || targetRect.Height <= 0)
        {
            return false;
        }

        var targetClient = new POINT
        {
            X = (int)Math.Round(nx * targetRect.Width),
            Y = (int)Math.Round(ny * targetRect.Height)
        };

        var targetScreen = Win32Interop.ClientToScreenDpi(targetHwnd, targetClient);

        mapped = new MappedPoint
        {
            TargetHwnd = targetHwnd,
            TargetClientPoint = targetClient,
            TargetScreenPoint = targetScreen,
            NormalizedX = nx,
            NormalizedY = ny
        };

        return true;
    }
}
