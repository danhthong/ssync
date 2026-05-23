using CommunityToolkit.Mvvm.ComponentModel;
using SandboxSync.Models;

namespace SandboxSync.ViewModels;

public partial class LogItemViewModel : ObservableObject
{
    public LogItemViewModel(LogEntry entry) => Entry = entry;

    public LogEntry Entry { get; }

    public string TimestampText => Entry.Timestamp.ToString("HH:mm:ss.fff");

    public LogLevel Level => Entry.Level;

    public string Message => Entry.Message;

    public string FullText => Entry.ToString();
}
