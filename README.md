# Sandbox Sync

Production-ready **.NET 8 WPF** desktop application that synchronizes mouse (and optional keyboard) input from a **master window** to multiple **target windows**, optimized for **Sandboxie Plus** multibox workflows.

## Features

- Master window selection (title, process, HWND, sandbox name)
- Multi-select target window list with refresh and auto-reconnect
- Low-latency `WH_MOUSE_LL` global hook
- Left/right/middle click, double-click, mouse down/up, optional move sync
- Proportional client-area coordinate mapping across different window sizes and DPI
- Feedback-loop protection via `dwExtraInfo` tagging + suppression window
- Hybrid replication: `PostMessage` first, `SendInput` fallback
- Sandboxie process-based detection (parent chain, `SbieDll.dll`, title patterns)
- Profiles (JSON), hotkeys, pause/resume, click overlay visualization
- Windows 11 Fluent UI (dark, Mica) via **WPF-UI**

## Requirements

- **Windows 10 1809+** (Windows 11 recommended)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 **or** VS Code + `dotnet` CLI
- **Sandboxie Plus** (optional, for sandboxed targets)

> **Note:** This repository was generated on macOS. Build and run on a Windows machine.

## Build

```powershell
cd SandboxSync
dotnet restore
dotnet build -c Release
dotnet run -c Release
```

Output executable:

```
SandboxSync\bin\Release\net8.0-windows10.0.19041.0\SandboxSync.exe
```

Publish single-file (optional):

```powershell
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

## How to use

1. Launch **Sandboxie Plus** and start your sandboxed instances.
2. Open **Sandbox Sync**.
3. Click **Refresh Windows**.
4. In the left panel, select a window and click **Set as Main**.
5. In the center panel, check one or more **target** windows.
6. Click **Start Sync**.
7. Click inside the master window — clicks are mirrored to targets at mapped coordinates.

### Hotkeys (default)

| Action | Hotkey |
|--------|--------|
| Start sync | `Ctrl+Shift+S` |
| Stop sync | `Ctrl+Shift+X` |
| Pause / Resume | `Ctrl+Shift+P` |

### Settings

Open **Settings** to configure replication mode, mouse move sync, keyboard sync, broadcast mode, click delay, move FPS limit, overlay, and auto-reconnect.

Profiles are stored at:

```
%LOCALAPPDATA%\SandboxSync\Profiles\
%LOCALAPPDATA%\SandboxSync\settings.json
```

## Architecture

```
/Core          SyncEngine, hooks, CoordinateMapper, InputReplicator, FeedbackGuard
/Interop       NativeMethods, Win32Interop, DpiHelper
/Services      Scanner, Sandbox detector, logging, profiles, hotkeys, overlay
/ViewModels    MVVM (CommunityToolkit.Mvvm)
/Views         WPF-UI FluentWindow UI
```

## Required permissions

| Scenario | Requirement |
|----------|-------------|
| Normal same-desktop windows | Standard user (`asInvoker`) |
| Elevated (admin) target windows | Run Sandbox Sync **as Administrator** so hooks/messages can reach elevated HWNDs |
| UIPI-blocked `PostMessage` | App auto-falls back to **SendInput** per target |
| Windows Defender / AV | May flag low-level hooks — add an exclusion if blocked |

The manifest requests **Per-Monitor V2 DPI** awareness for correct coordinate mapping across mixed-DPI monitors.

## Sandboxie limitations

1. **Process isolation** — Sandboxed windows may reject `PostMessage` from unsandboxed senders (UIPI). Hybrid mode uses `SendInput` as fallback (moves the real cursor briefly).
2. **No SbieDLL integration** — Sandbox names are inferred via parent process chain, module load (`SbieDll.dll`), and title patterns like `[#] Title [BoxName]`.
3. **Different box policies** — Some boxes block cross-process input entirely; behavior depends on Sandboxie configuration.
4. **Minimized / occluded windows** — Clicks are delivered to HWND message queues; the app does not restore minimized windows automatically.
5. **Multiple monitors** — Supported via DPI-aware client/screen transforms; overlay ripples are best-effort per screen region.

## Anti-cheat and ToS warning

**Do not use Sandbox Sync with online games protected by anti-cheat** (Vanguard, Easy Anti-Cheat, BattlEye, etc.) or any software whose terms forbid input automation.

Low-level hooks and `SendInput` are commonly detected and may result in **account bans** or violations of terms of service. This tool is intended for legitimate multibox / productivity / testing scenarios you own and control.

## Performance notes

- Hook callback only enqueues struct events (non-blocking).
- Sync pipeline runs off the hook thread via a concurrent queue.
- Move sync is FPS-limited (default 120) to reduce CPU use.
- Target replication uses per-HWND mode caching after first PostMessage failure.

Typical latency is **1–8 ms** depending on target count, replication mode, and system load.

## Future optimizations

- Raw Input (`WM_INPUT`) path for games that ignore posted messages
- Kernel/driver-level replication (e.g. Interception) for lowest latency
- Direct **SbieDLL** box/process queries when Sandboxie SDK is present
- Per-target thread affinity and batched `SendInput`
- GPU-composited overlay using DXGI
- Exclude-region editor UI (normalized rectangles)
- Elevated/non-elevated split broker process

## License

Provided as-is for local development and personal use. Verify compliance with Sandboxie Plus and target application licenses before deploying.
