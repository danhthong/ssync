using System.Text.RegularExpressions;
using SandboxSync.Interop;
using SandboxSync.Models;

namespace SandboxSync.Services;

/// <summary>
/// Process-based Sandboxie Plus detection (no SbieDLL dependency).
/// </summary>
public sealed class SandboxDetectorService
{
    private static readonly string[] SandboxParentNames =
    [
        "SbieSvc",
        "Start",
        "SandboxieDcomLaunch",
        "SandboxieRpcSs"
    ];

    private static readonly string[] SandboxModules =
    [
        "SbieDll.dll",
        "SbieDll"
    ];

    // Sandboxie title pattern: [#] Title [BoxName]
    private static readonly Regex TitleBoxRegex = new(
        @"\[#\]\s*.+?\s*\[(?<box>[^\]]+)\]\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string? DetectSandboxName(WindowInfo window)
    {
        if (!string.IsNullOrWhiteSpace(window.SandboxName))
        {
            return window.SandboxName;
        }

        var fromTitle = ParseSandboxFromTitle(window.Title);
        if (!string.IsNullOrWhiteSpace(fromTitle))
        {
            return fromTitle;
        }

        if (HasSandboxModule(window.ProcessId))
        {
            return fromTitle ?? "Sandboxed";
        }

        if (IsSandboxedByParentChain(window.ProcessId, out var inferred))
        {
            return inferred ?? "Sandboxed";
        }

        return null;
    }

    public void Enrich(WindowInfo window)
    {
        window.SandboxName = DetectSandboxName(window) ?? string.Empty;
    }

    private static string? ParseSandboxFromTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var match = TitleBoxRegex.Match(title);
        if (match.Success)
        {
            return match.Groups["box"].Value.Trim();
        }

        // Alternate: trailing [BoxName] without [#] prefix
        var lastBracket = title.LastIndexOf('[');
        var lastClose = title.LastIndexOf(']');
        if (lastBracket >= 0 && lastClose > lastBracket)
        {
            var candidate = title.Substring(lastBracket + 1, lastClose - lastBracket - 1).Trim();
            if (!string.IsNullOrWhiteSpace(candidate) && candidate.Length <= 64)
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool HasSandboxModule(int processId)
    {
        foreach (var module in SandboxModules)
        {
            if (Win32Interop.ProcessHasModule(processId, module))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSandboxedByParentChain(int processId, out string? boxName)
    {
        boxName = null;
        var visited = new HashSet<int>();
        var current = processId;
        var depth = 0;

        while (current > 0 && depth < 32 && visited.Add(current))
        {
            var processName = Win32Interop.GetProcessName(current);
            if (SandboxParentNames.Any(p =>
                    processName.Equals(p, StringComparison.OrdinalIgnoreCase) ||
                    processName.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (HasSandboxModule(current))
            {
                return true;
            }

            current = Win32Interop.GetParentProcessId(current);
            depth++;
        }

        return false;
    }
}
