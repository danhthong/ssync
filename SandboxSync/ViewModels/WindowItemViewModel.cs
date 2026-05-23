using CommunityToolkit.Mvvm.ComponentModel;
using SandboxSync.Models;

namespace SandboxSync.ViewModels;

public partial class WindowItemViewModel : ObservableObject
{
    public WindowItemViewModel(WindowInfo info)
    {
        Info = info;
    }

    public WindowInfo Info { get; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isMaster;

    public string Title => Info.Title;

    public string ProcessName => Info.ProcessName;

    public string HandleText => $"0x{Info.Handle.ToInt64():X}";

    public string SandboxName => string.IsNullOrWhiteSpace(Info.SandboxName) ? "—" : Info.SandboxName;

    public string DisplayLabel => Info.DisplayLabel;

    public bool IsSandboxed => Info.IsSandboxed;

    public void RefreshFrom(WindowInfo info)
    {
        Info.Handle = info.Handle;
        Info.ProcessId = info.ProcessId;
        Info.Title = info.Title;
        Info.ProcessName = info.ProcessName;
        Info.ExecutablePath = info.ExecutablePath;
        Info.ClassName = info.ClassName;
        Info.SandboxName = info.SandboxName;
        Info.Left = info.Left;
        Info.Top = info.Top;
        Info.Width = info.Width;
        Info.Height = info.Height;
        Info.ClientWidth = info.ClientWidth;
        Info.ClientHeight = info.ClientHeight;
        Info.IsVisible = info.IsVisible;
        Info.IsMinimized = info.IsMinimized;

        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ProcessName));
        OnPropertyChanged(nameof(HandleText));
        OnPropertyChanged(nameof(SandboxName));
        OnPropertyChanged(nameof(DisplayLabel));
        OnPropertyChanged(nameof(IsSandboxed));
    }
}
