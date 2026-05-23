using SandboxSync.Interop;

namespace SandboxSync.Models;

public sealed class HotkeyBinding
{
    public uint VirtualKey { get; set; }

    public uint Modifiers { get; set; }

    public string DisplayName { get; set; } = string.Empty;
}

public sealed class SyncProfile
{
    public string Name { get; set; } = "Default";

    public WindowMatchRule? MasterRule { get; set; }

    public List<WindowMatchRule> TargetRules { get; set; } = [];

    public SyncSettings Settings { get; set; } = new();

    public List<ExcludeRegion> ExcludeRegions { get; set; } = [];

    public HotkeyBinding StartHotkey { get; set; } = new()
    {
        VirtualKey = 0x53, // S
        Modifiers = (uint)(HotKeyModifiers.MOD_CONTROL | HotKeyModifiers.MOD_SHIFT),
        DisplayName = "Ctrl+Shift+S"
    };

    public HotkeyBinding StopHotkey { get; set; } = new()
    {
        VirtualKey = 0x58, // X
        Modifiers = (uint)(HotKeyModifiers.MOD_CONTROL | HotKeyModifiers.MOD_SHIFT),
        DisplayName = "Ctrl+Shift+X"
    };

    public HotkeyBinding PauseHotkey { get; set; } = new()
    {
        VirtualKey = 0x50, // P
        Modifiers = (uint)(HotKeyModifiers.MOD_CONTROL | HotKeyModifiers.MOD_SHIFT),
        DisplayName = "Ctrl+Shift+P"
    };
}
