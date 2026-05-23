using System.Diagnostics;

namespace SandboxSync.Core;

/// <summary>
/// Prevents synchronized input from re-entering the sync pipeline.
/// </summary>
public sealed class FeedbackGuard
{
    /// <summary>
    /// Magic tag written to dwExtraInfo for synthesized events.
    /// </summary>
    public const nint SyncTag = 0x5A4EC71C;

    private long _suppressionUntilTicks;
    private int _suppressionWindowMs = 50;

    public void Configure(int suppressionWindowMs) =>
        _suppressionWindowMs = Math.Max(0, suppressionWindowMs);

    public bool IsTagged(nint dwExtraInfo) => dwExtraInfo == SyncTag;

    public bool IsSuppressed() => Stopwatch.GetTimestamp() < Interlocked.Read(ref _suppressionUntilTicks);

    public void ArmSuppression() =>
        Interlocked.Exchange(
            ref _suppressionUntilTicks,
            Stopwatch.GetTimestamp() + (long)(_suppressionWindowMs * Stopwatch.Frequency / 1000.0));

    public bool ShouldIgnore(nint dwExtraInfo) =>
        IsTagged(dwExtraInfo) || IsSuppressed();
}
