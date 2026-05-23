using SandboxSync.Interop;
using SandboxSync.Models;

namespace SandboxSync.Services;

public sealed class WindowScannerService : IDisposable
{
    private readonly SandboxDetectorService _sandboxDetector;
    private readonly System.Timers.Timer _pollTimer;
    private List<WindowMatchRule> _targetRules = [];
    private bool _autoReconnect;

    public event EventHandler<IReadOnlyList<WindowInfo>>? WindowsRefreshed;
    public event EventHandler<IReadOnlyList<WindowInfo>>? TargetsReconnected;

    public WindowScannerService(SandboxDetectorService sandboxDetector)
    {
        _sandboxDetector = sandboxDetector;
        _pollTimer = new System.Timers.Timer(500);
        _pollTimer.Elapsed += (_, _) => PollAutoReconnect();
        _pollTimer.AutoReset = true;
    }

    public IReadOnlyList<WindowInfo> Refresh(string? sandboxFilter = null, string? titleFilter = null)
    {
        IReadOnlyList<WindowInfo> windows = Win32Interop.EnumerateTopLevelWindows();

        foreach (var window in windows)
        {
            _sandboxDetector.Enrich(window);
        }

        if (!string.IsNullOrWhiteSpace(titleFilter))
        {
            windows = windows
                .Where(w => w.Title.Contains(titleFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(sandboxFilter))
        {
            windows = windows
                .Where(w => w.SandboxName.Contains(sandboxFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        WindowsRefreshed?.Invoke(this, windows);
        return windows;
    }

    public void ConfigureAutoReconnect(bool enabled, IEnumerable<WindowMatchRule> targetRules)
    {
        _autoReconnect = enabled;
        _targetRules = targetRules.ToList();
        _pollTimer.Enabled = enabled && _targetRules.Count > 0;
    }

    public IReadOnlyList<WindowInfo> ResolveTargets(IEnumerable<WindowMatchRule> rules)
    {
        var all = Win32Interop.EnumerateTopLevelWindows();
        foreach (var w in all)
        {
            _sandboxDetector.Enrich(w);
        }

        return rules
            .Select(rule => all.FirstOrDefault(w => rule.Matches(w)))
            .Where(w => w is not null)
            .Cast<WindowInfo>()
            .ToList();
    }

    private void PollAutoReconnect()
    {
        if (!_autoReconnect || _targetRules.Count == 0)
        {
            return;
        }

        var matched = ResolveTargets(_targetRules);
        if (matched.Count > 0)
        {
            TargetsReconnected?.Invoke(this, matched);
        }
    }

    public void Dispose() => _pollTimer.Dispose();
}
