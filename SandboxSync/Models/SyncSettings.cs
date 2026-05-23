namespace SandboxSync.Models;

public enum InputReplicationMode
{
    PostMessage,
    SendInput,
    Hybrid
}

public sealed class SyncSettings
{
    public InputReplicationMode ReplicationMode { get; set; } = InputReplicationMode.Hybrid;

    public bool SyncMouseMove { get; set; }

    public bool SyncKeyboard { get; set; }

    public bool BroadcastMode { get; set; }

    public bool ShowClickOverlay { get; set; } = true;

    public bool AutoReconnect { get; set; } = true;

    public int ClickDelayMs { get; set; }

    /// <summary>
    /// Maximum mouse-move events per second when SyncMouseMove is enabled. 0 = unlimited.
    /// </summary>
    public int MoveFpsLimit { get; set; } = 120;

    public int SuppressionWindowMs { get; set; } = 50;

    public string SandboxFilter { get; set; } = string.Empty;

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
        SandboxFilter = SandboxFilter
    };
}
