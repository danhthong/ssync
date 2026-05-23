namespace SandboxSync.Models;

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

public sealed class LogEntry
{
    public DateTime Timestamp { get; init; } = DateTime.Now;

    public LogLevel Level { get; init; }

    public string Message { get; init; } = string.Empty;

    public override string ToString() =>
        $"[{Timestamp:HH:mm:ss.fff}] [{Level}] {Message}";
}
