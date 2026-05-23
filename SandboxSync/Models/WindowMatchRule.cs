using System.Text.RegularExpressions;

namespace SandboxSync.Models;

/// <summary>
/// Used to re-match windows after HWND changes (auto-reconnect).
/// </summary>
public sealed class WindowMatchRule
{
    public string TitlePattern { get; set; } = string.Empty;

    public string ProcessName { get; set; } = string.Empty;

    public string SandboxName { get; set; } = string.Empty;

    public string ClassName { get; set; } = string.Empty;

    public string ExecutablePath { get; set; } = string.Empty;

    public static WindowMatchRule FromWindow(WindowInfo window) => new()
    {
        TitlePattern = Regex.Escape(window.Title),
        ProcessName = window.ProcessName,
        SandboxName = window.SandboxName,
        ClassName = window.ClassName,
        ExecutablePath = window.ExecutablePath
    };

    public bool Matches(WindowInfo candidate)
    {
        if (!string.IsNullOrWhiteSpace(ProcessName) &&
            !string.Equals(candidate.ProcessName, ProcessName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(SandboxName) &&
            !string.Equals(candidate.SandboxName, SandboxName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(ClassName) &&
            !string.Equals(candidate.ClassName, ClassName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(ExecutablePath) &&
            !string.Equals(candidate.ExecutablePath, ExecutablePath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(TitlePattern))
        {
            try
            {
                if (!Regex.IsMatch(candidate.Title, TitlePattern, RegexOptions.IgnoreCase))
                {
                    return false;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                return candidate.Title.Contains(TitlePattern, StringComparison.OrdinalIgnoreCase);
            }
        }

        return true;
    }
}
