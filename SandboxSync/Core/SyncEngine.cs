using System.Diagnostics;
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
    private IntPtr _masterHwnd;
    private List<IntPtr> _targetHwnds = [];
    private List<ExcludeRegion> _excludeRegions = [];
    private SyncSettings _settings = new();
    private SyncState _state = SyncState.Stopped;
    private long _lastMoveTicks;
    private long _moveIntervalTicks;

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
        _keyboardHook.KeyboardEvent += OnKeyboardEvent;
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
            _inputReplicator.ResetTargetModes();
            _inputReplicator.Configure(_settings.FocusTargetForSendInput);

            _moveIntervalTicks = _settings.MoveFpsLimit > 0
                ? (long)(Stopwatch.Frequency / (double)_settings.MoveFpsLimit)
                : 0;
        }

        _logger.Info($"Configured master=0x{masterHwnd.ToInt64():X}, targets={_targetHwnds.Count}, mode={settings.ReplicationMode}");
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
        if (_settings.SyncKeyboard)
        {
            _keyboardHook.Start();
        }

        SetState(SyncState.Running);
        _logger.Info("Synchronization started.");
        StatusChanged?.Invoke(this, "Running");
    }

    public void Stop()
    {
        _mouseHook.Stop();
        _keyboardHook.Stop();
        SetState(SyncState.Stopped);
        _logger.Info("Synchronization stopped.");
        StatusChanged?.Invoke(this, "Stopped");
    }

    public void Pause()
    {
        if (_state != SyncState.Running)
        {
            return;
        }

        SetState(SyncState.Paused);
        _logger.Info("Synchronization paused.");
        StatusChanged?.Invoke(this, "Paused");
    }

    public void Resume()
    {
        if (_state != SyncState.Paused)
        {
            return;
        }

        SetState(SyncState.Running);
        _logger.Info("Synchronization resumed.");
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

        IntPtr master;
        List<IntPtr> targets;
        SyncSettings settings;
        List<ExcludeRegion> excludes;

        lock (_gate)
        {
            master = _masterHwnd;
            targets = _targetHwnds.ToList();
            settings = _settings;
            excludes = _excludeRegions;
        }

        if (!Win32Interop.IsWindowAlive(master))
        {
            return;
        }

        if (e.Message == WindowMessage.WM_MOUSEMOVE && !settings.SyncMouseMove)
        {
            return;
        }

        if (e.Message == WindowMessage.WM_MOUSEMOVE && !CanProcessMove())
        {
            return;
        }

        if (!IsClickInMasterArea(master, e.ScreenPoint, settings.BroadcastMode, targets))
        {
            return;
        }

        if (settings.ClickDelayMs > 0 && IsClickMessage(e.Message))
        {
            Thread.Sleep(settings.ClickDelayMs);
        }

        _feedbackGuard.ArmSuppression();

        if (IsClickMessage(e.Message))
        {
            _logger.Info($"Click {e.Message} @ ({e.ScreenPoint.X},{e.ScreenPoint.Y}) → {targets.Count} target(s)");
        }

        var successCount = 0;
        foreach (var target in targets)
        {
            if (!Win32Interop.IsWindowAlive(target))
            {
                _logger.Warning($"Target 0x{target.ToInt64():X} no longer alive.");
                continue;
            }

            if (!_coordinateMapper.TryMap(master, e.ScreenPoint, target, excludes, out var mapped))
            {
                continue;
            }

            var ok = _inputReplicator.ReplicateMouse(
                target,
                e.Message,
                mapped,
                settings.ReplicationMode,
                e.MouseData);

            if (ok)
            {
                successCount++;
            }

            if (settings.ShowClickOverlay && IsClickMessage(e.Message))
            {
                _overlay.ShowRipple(mapped.TargetScreenPoint);
            }
        }

        if (IsClickMessage(e.Message) && successCount != targets.Count)
        {
            _logger.Warning($"Click replicated {successCount}/{targets.Count} targets.");
        }
    }

    private static bool IsClickInMasterArea(IntPtr master, POINT screenPt, bool broadcast, List<IntPtr> targets)
    {
        var rootUnderCursor = Win32Interop.GetRootWindowFromPoint(screenPt);
        if (rootUnderCursor == master)
        {
            return true;
        }

        if (broadcast && targets.Contains(rootUnderCursor))
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

    private void OnKeyboardEvent(object? sender, KeyboardHookEvent e)
    {
        if (_state != SyncState.Running)
        {
            return;
        }

        if (_feedbackGuard.ShouldIgnore(e.ExtraInfo))
        {
            return;
        }

        SyncSettings settings;
        IntPtr master;
        List<IntPtr> targets;

        lock (_gate)
        {
            settings = _settings;
            master = _masterHwnd;
            targets = _targetHwnds.ToList();
        }

        if (!settings.SyncKeyboard)
        {
            return;
        }

        NativeMethods.GetCursorPos(out var cursor);
        if (Win32Interop.GetRootWindowFromPoint(cursor) != master)
        {
            return;
        }

        _feedbackGuard.ArmSuppression();

        foreach (var target in targets)
        {
            _inputReplicator.ReplicateKey(target, e.VirtualKey, e.IsKeyUp, settings.ReplicationMode);
        }
    }

    private bool CanProcessMove()
    {
        if (_moveIntervalTicks <= 0)
        {
            return true;
        }

        var now = Stopwatch.GetTimestamp();
        var last = Interlocked.Read(ref _lastMoveTicks);
        if (now - last < _moveIntervalTicks)
        {
            return false;
        }

        Interlocked.Exchange(ref _lastMoveTicks, now);
        return true;
    }

    private static bool IsClickMessage(WindowMessage msg) =>
        msg is WindowMessage.WM_LBUTTONDOWN
            or WindowMessage.WM_LBUTTONUP
            or WindowMessage.WM_LBUTTONDBLCLK
            or WindowMessage.WM_RBUTTONDOWN
            or WindowMessage.WM_RBUTTONUP
            or WindowMessage.WM_RBUTTONDBLCLK
            or WindowMessage.WM_MBUTTONDOWN
            or WindowMessage.WM_MBUTTONUP
            or WindowMessage.WM_MBUTTONDBLCLK;

    private void SetState(SyncState state)
    {
        _state = state;
        StateChanged?.Invoke(this, state);
    }
}
