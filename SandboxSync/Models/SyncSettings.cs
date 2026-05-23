namespace SandboxSync.Models;

public enum InputReplicationMode
{
    /// <summary>
    /// Posts WM_*BUTTONDOWN/UP to the deepest child HWND at the mapped point.
    /// Real-time, no focus switching, no cursor movement. Works for browser
    /// games, Electron, web-views, normal Win32 apps. Most games ignore these.
    /// </summary>
    PostMessage,

    /// <summary>
    /// Brings each target to the foreground, moves the cursor, fires SendInput,
    /// then restores master. Required for games that read Raw Input / DirectInput.
    /// Causes brief focus flicker per target.
    /// </summary>
    SendInput,

    Hybrid
}

public sealed class SyncSettings
{
    public InputReplicationMode ReplicationMode { get; set; } = InputReplicationMode.SendInput;

    public bool SyncMouseMove { get; set; }

    public bool SyncKeyboard { get; set; }

    public bool BroadcastMode { get; set; }

    public bool ShowClickOverlay { get; set; } = true;

    public bool AutoReconnect { get; set; } = true;

    public int ClickDelayMs { get; set; }

    /// <summary>
    /// Delay between clicking each target window inside one click batch (milliseconds).
    /// </summary>
    public int InterTargetDelayMs { get; set; } = 500;

    /// <summary>
    /// Maximum mouse-move events per second when SyncMouseMove is enabled. 0 = unlimited.
    /// </summary>
    public int MoveFpsLimit { get; set; } = 120;

    public int SuppressionWindowMs { get; set; } = 250;

    public string SandboxFilter { get; set; } = string.Empty;

    public string TitleFilter { get; set; } = "Dragon Hunters";

    /// <summary>
    /// When true, briefly bring each target to the foreground before sending SendInput.
    /// Required for games that read raw input (DirectInput / Raw Input) instead of WM_* messages.
    /// </summary>
    public bool FocusTargetForSendInput { get; set; } = true;

    /// <summary>
    /// When true, the engine waits for the BUTTONUP event and sends a single DOWN+UP pair per target.
    /// Greatly reduces flicker when SendInput is the active replication mode.
    /// Disables drag-sync (hold-and-drag mouse).
    /// </summary>
    public bool CoalesceClicks { get; set; } = true;

    public SyncSettings Clone() => new()
    {
        ReplicationMode = ReplicationMode,
        SyncMouseMove = SyncMouseMove,
        SyncKeyboard = SyncKeyboard,
        BroadcastMode = BroadcastMode,
        ShowClickOverlay = ShowClickOverlay,
        AutoReconnect = AutoReconnect,
        ClickDelayMs = ClickDelayMs,
        MoveFpsLimit = MoveFpsLimit,
        SuppressionWindowMs = SuppressionWindowMs,
        SandboxFilter = SandboxFilter,
        TitleFilter = TitleFilter,
        FocusTargetForSendInput = FocusTargetForSendInput,
        CoalesceClicks = CoalesceClicks,
        InterTargetDelayMs = InterTargetDelayMs
    };
}
