using System.Windows;
using System.Windows.Threading;
using SandboxSync.Interop;
using SandboxSync.Views;

namespace SandboxSync.Services;

public sealed class ClickOverlayService : IDisposable
{
    private readonly Dictionary<string, ClickOverlayWindow> _overlays = new();
    private readonly object _gate = new();

    public void ShowRipple(POINT screenPoint)
    {
        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            var monitorKey = GetMonitorKey(screenPoint);
            var overlay = GetOrCreateOverlay(monitorKey);
            overlay.ShowRipple(screenPoint.X, screenPoint.Y);
        }, DispatcherPriority.Send);
    }

    private ClickOverlayWindow GetOrCreateOverlay(string key)
    {
        lock (_gate)
        {
            if (_overlays.TryGetValue(key, out var existing))
            {
                return existing;
            }

            var overlay = new ClickOverlayWindow();
            overlay.Show();
            _overlays[key] = overlay;
            return overlay;
        }
    }

    private static string GetMonitorKey(POINT point) =>
        $"{point.X / 1000}_{point.Y / 1000}";

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var overlay in _overlays.Values)
            {
                overlay.Close();
            }

            _overlays.Clear();
        }
    }
}
