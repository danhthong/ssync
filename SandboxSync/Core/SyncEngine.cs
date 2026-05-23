using SandboxSync.Interop;
using SandboxSync.Models;
using SandboxSync.Services;

namespace SandboxSync.Core;

public enum SyncState
{
    Stopped,
    Running,
    Paused
}

public sealed class SyncEngine
{
    private readonly LowLevelMouseHook _mouseHook;
    private readonly LowLevelKeyboardHook _keyboardHook;
    private readonly CoordinateMapper _coordinateMapper;
    private readonly InputReplicator _inputReplicator;
    private readonly FeedbackGuard _feedbackGuard;
    private readonly LoggingService _logger;
    private readonly ClickOverlayService _overlay;

    private readonly object _gate = new();
    private readonly SemaphoreSlim _batchGate = new(1, 1);
    private IntPtr _masterHwnd;
    private List<IntPtr> _targetHwnds = [];
    private List<ExcludeRegion> _excludeRegions = [];
    private SyncSettings _settings = new();
    private SyncState _state = SyncState.Stopped;

    public event EventHandler<SyncState>? StateChanged;
    public event EventHandler<string>? StatusChanged;

    public SyncState State => _state;

    public SyncEngine(
        LowLevelMouseHook mouseHook,
        LowLevelKeyboardHook keyboardHook,
        CoordinateMapper coordinateMapper,
        InputReplicator inputReplicator,
        FeedbackGuard feedbackGuard,
        LoggingService logger,
        ClickOverlayService overlay)
    {
        _mouseHook = mouseHook;
        _keyboardHook = keyboardHook;
        _coordinateMapper = coordinateMapper;
        _inputReplicator = inputReplicator;
        _feedbackGuard = feedbackGuard;
        _logger = logger;
        _overlay = overlay;

        _mouseHook.MouseEvent += OnMouseEvent;
    }

    public void Configure(
        IntPtr masterHwnd,
        IEnumerable<IntPtr> targetHwnds,
        SyncSettings settings,
        IEnumerable<ExcludeRegion>? excludeRegions = null)
    {
        lock (_gate)
        {
            _masterHwnd = masterHwnd;
            _targetHwnds = targetHwnds
                .Where(h => h != IntPtr.Zero && h != masterHwnd)
                .Distinct()
                .ToList();
            _settings = settings.Clone();
            _excludeRegions = excludeRegions?.ToList() ?? [];
            _feedbackGuard.Configure(_settings.SuppressionWindowMs);
        }
    }

    public void UpdateTargets(IEnumerable<IntPtr> targetHwnds)
    {
        lock (_gate)
        {
            _targetHwnds = targetHwnds
                .Where(h => h != IntPtr.Zero && h != _masterHwnd)
                .Distinct()
                .ToList();
        }
    }

    public void Start()
    {
        if (_state == SyncState.Running)
        {
            return;
        }

        lock (_gate)
        {
            if (_masterHwnd == IntPtr.Zero || _targetHwnds.Count == 0)
            {
                throw new InvalidOperationException("Master window and at least one target are required.");
            }
        }

        _mouseHook.Start();
        SetState(SyncState.Running);
        StatusChanged?.Invoke(this, "Running");
    }

    public void Stop()
    {
        _mouseHook.Stop();
        _keyboardHook.Stop();
        SetState(SyncState.Stopped);
        StatusChanged?.Invoke(this, "Stopped");
    }

    public void Pause()
    {
        if (_state != SyncState.Running)
        {
            return;
        }

        SetState(SyncState.Paused);
        StatusChanged?.Invoke(this, "Paused");
    }

    public void Resume()
    {
        if (_state != SyncState.Paused)
        {
            return;
        }

        SetState(SyncState.Running);
        StatusChanged?.Invoke(this, "Running");
    }

    public void TogglePause()
    {
        if (_state == SyncState.Paused)
        {
            Resume();
        }
        else if (_state == SyncState.Running)
        {
            Pause();
        }
    }

    private void OnMouseEvent(object? sender, MouseHookEvent e)
    {
        if (_state != SyncState.Running)
        {
            return;
        }

        if (_feedbackGuard.ShouldIgnore(e.ExtraInfo))
        {
            return;
        }

        // Only act on the UP edge of a click; that triggers a full DOWN+UP
        // to each target. Everything else (DOWN, MOVE, DBLCLK, wheel) is ignored.
        if (e.Message is not WindowMessage.WM_LBUTTONUP
            and not WindowMessage.WM_RBUTTONUP
            and not WindowMessage.WM_MBUTTONUP)
        {
            return;
        }

        IntPtr master;
        List<IntPtr> targets;
        List<ExcludeRegion> excludes;
        bool showOverlay;
        int interTargetDelayMs;

        lock (_gate)
        {
            master = _masterHwnd;
            targets = _targetHwnds.ToList();
            excludes = _excludeRegions;
            showOverlay = _settings.ShowClickOverlay;
            interTargetDelayMs = Math.Max(0, _settings.InterTargetDelayMs);
        }

        if (!Win32Interop.IsWindowAlive(master))
        {
            return;
        }

        if (!IsClickInMasterArea(master, e.ScreenPoint))
        {
            return;
        }

        NativeMethods.GetCursorPos(out var savedCursor);
        var screenPoint = e.ScreenPoint;
        var message = e.Message;

        // Run the click batch off the hook consumer thread so the app stays
        // responsive and the OS keeps delivering hook events while we click.
        _ = Task.Run(() => RunClickBatchAsync(
            master,
            targets,
            excludes,
            message,
            screenPoint,
            savedCursor,
            showOverlay,
            interTargetDelayMs));
    }

    private async Task RunClickBatchAsync(
        IntPtr master,
        List<IntPtr> targets,
        List<ExcludeRegion> excludes,
        WindowMessage message,
        POINT screenPoint,
        POINT savedCursor,
        bool showOverlay,
        int interTargetDelayMs)
    {
        await _batchGate.WaitAsync().ConfigureAwait(false);
        try
        {
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];

                if (!Win32Interop.IsWindowAlive(target))
                {
                    continue;
                }

                if (!_coordinateMapper.TryMap(master, screenPoint, target, excludes, out var mapped))
                {
                    continue;
                }

                _feedbackGuard.ArmSuppression();
                _inputReplicator.ReplicateClickPair(target, message, mapped);

                if (showOverlay)
                {
                    _overlay.ShowRipple(mapped.TargetScreenPoint);
                }

                if (i < targets.Count - 1 && interTargetDelayMs > 0)
                {
                    await Task.Delay(interTargetDelayMs).ConfigureAwait(false);
                }
            }

            _feedbackGuard.ArmSuppression();
            _inputReplicator.RestoreForeground(master, savedCursor);
        }
        finally
        {
            _batchGate.Release();
        }
    }

    private static bool IsClickInMasterArea(IntPtr master, POINT screenPt)
    {
        var rootUnderCursor = Win32Interop.GetRootWindowFromPoint(screenPt);
        if (rootUnderCursor == master)
        {
            return true;
        }

        if (NativeMethods.GetWindowRect(master, out var rect))
        {
            if (screenPt.X >= rect.Left && screenPt.X <= rect.Right &&
                screenPt.Y >= rect.Top && screenPt.Y <= rect.Bottom)
            {
                return true;
            }
        }

        return false;
    }

    private void SetState(SyncState state)
    {
        _state = state;
        StateChanged?.Invoke(this, state);
    }
}
