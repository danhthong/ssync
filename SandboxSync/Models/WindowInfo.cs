using System.Text.Json.Serialization;

namespace SandboxSync.Models;

/// <summary>
/// Describes a top-level window discovered by the scanner.
/// </summary>
public sealed class WindowInfo
{
    public IntPtr Handle { get; set; }

    public int ProcessId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string ProcessName { get; set; } = string.Empty;

    public string ExecutablePath { get; set; } = string.Empty;

    public string ClassName { get; set; } = string.Empty;

    /// <summary>
    /// Sandboxie box name when detectable; otherwise empty.
    /// </summary>
    public string SandboxName { get; set; } = string.Empty;

    public int Left { get; set; }

    public int Top { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public int ClientWidth { get; set; }

    public int ClientHeight { get; set; }

    public bool IsVisible { get; set; }

    public bool IsMinimized { get; set; }

    [JsonIgnore]
    public bool IsSandboxed => !string.IsNullOrWhiteSpace(SandboxName);

    [JsonIgnore]
    public string DisplayLabel =>
        string.IsNullOrWhiteSpace(SandboxName)
            ? $"{Title} ({ProcessName})"
            : $"{Title} [{SandboxName}] ({ProcessName})";

    public WindowInfo Clone() => new()
    {
        Handle = Handle,
        ProcessId = ProcessId,
        Title = Title,
        ProcessName = ProcessName,
        ExecutablePath = ExecutablePath,
        ClassName = ClassName,
        SandboxName = SandboxName,
        Left = Left,
        Top = Top,
        Width = Width,
        Height = Height,
        ClientWidth = ClientWidth,
        ClientHeight = ClientHeight,
        IsVisible = IsVisible,
        IsMinimized = IsMinimized
    };
}
