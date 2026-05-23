using SandboxSync.Interop;

namespace SandboxSync.Core;

public readonly struct MouseHookEvent
{
    public required WindowMessage Message { get; init; }

    public required POINT ScreenPoint { get; init; }

    public required uint MouseData { get; init; }

    public required uint Flags { get; init; }

    public required uint Time { get; init; }

    public required nint ExtraInfo { get; init; }
}

public readonly struct KeyboardHookEvent
{
    public required uint VirtualKey { get; init; }

    public required uint ScanCode { get; init; }

    public required uint Flags { get; init; }

    public required uint Time { get; init; }

    public required nint ExtraInfo { get; init; }

    public bool IsKeyUp => (Flags & 0x80) != 0;
}
