using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SandboxSync.Core;
using SandboxSync.Models;
using SandboxSync.Services;

namespace SandboxSync.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly WindowScannerService _scanner;
    private readonly SandboxDetectorService _sandboxDetector;
    private readonly SyncEngine _syncEngine;
    private readonly LoggingService _logger;
    private readonly ProfileService _profiles;
    private readonly HotkeyService _hotkeys;
    [ObservableProperty]
    private WindowItemViewModel? _masterWindow;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _sandboxFilter = string.Empty;

    [ObservableProperty]
    private string _titleFilter = "Dragon Hunters";

    [ObservableProperty]
    private string _profileName = "Default";

    [ObservableProperty]
    private bool _isSyncRunning;

    [ObservableProperty]
    private bool _isSyncPaused;

    [ObservableProperty]
    private SyncSettings _settings = new();

    public ObservableCollection<WindowItemViewModel> Windows { get; } = [];

    public ObservableCollection<LogEntry> Logs => _logger.Entries;

    public MainViewModel(
        WindowScannerService scanner,
        SandboxDetectorService sandboxDetector,
        SyncEngine syncEngine,
        LoggingService logger,
        ProfileService profiles,
        HotkeyService hotkeys)
    {
        _scanner = scanner;
        _sandboxDetector = sandboxDetector;
        _syncEngine = syncEngine;
        _logger = logger;
        _profiles = profiles;
        _hotkeys = hotkeys;

        _scanner.WindowsRefreshed += (_, _) => { };
        _scanner.TargetsReconnected += OnTargetsReconnected;
        _syncEngine.StateChanged += (_, state) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsSyncRunning = state == SyncState.Running;
                IsSyncPaused = state == SyncState.Paused;
            });
        };
        _syncEngine.StatusChanged += (_, status) =>
            Application.Current.Dispatcher.Invoke(() => StatusText = status);

        _hotkeys.HotkeyPressed += OnHotkeyPressed;
        _logger.EntryAdded += (_, entry) =>
        {
            // Logs collection is updated by LoggingService on UI thread.
        };
    }

    public async Task InitializeAsync()
    {
        _logger.AttachDispatcher(Application.Current.Dispatcher);
        Settings = await _profiles.LoadSettingsAsync();
        SandboxFilter = Settings.SandboxFilter;
        if (!string.IsNullOrWhiteSpace(Settings.TitleFilter))
        {
            TitleFilter = Settings.TitleFilter;
        }
        RefreshWindowsCommand.Execute(null);
        RegisterHotkeys();
        _logger.Info("Sandbox Sync initialized.");
    }

    [RelayCommand]
    private void RefreshWindows()
    {
        Settings.SandboxFilter = SandboxFilter;
        Settings.TitleFilter = TitleFilter;

        var list = _scanner.Refresh(
            string.IsNullOrWhiteSpace(SandboxFilter) ? null : SandboxFilter,
            string.IsNullOrWhiteSpace(TitleFilter) ? null : TitleFilter);

        var selectedHandles = Windows
            .Where(w => w.IsSelected)
            .Select(w => w.Info.Handle)
            .ToHashSet();

        var masterHandle = MasterWindow?.Info.Handle ?? IntPtr.Zero;

        Windows.Clear();
        foreach (var info in list)
        {
            _sandboxDetector.Enrich(info);
            var vm = new WindowItemViewModel(info)
            {
                IsSelected = selectedHandles.Contains(info.Handle),
                IsMaster = info.Handle == masterHandle
            };
            Windows.Add(vm);
        }

        MasterWindow = Windows.FirstOrDefault(w => w.IsMaster);
        StatusText = string.IsNullOrWhiteSpace(TitleFilter)
            ? $"Found {Windows.Count} windows"
            : $"Found {Windows.Count} windows matching \"{TitleFilter}\"";
        _logger.Info($"Refreshed window list ({Windows.Count}) filter='{TitleFilter}'.");
    }

    [RelayCommand]
    private void SetMaster(WindowItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        foreach (var w in Windows)
        {
            w.IsMaster = ReferenceEquals(w, item);
        }

        MasterWindow = item;
        _logger.Info($"Master set: {item.Title} ({item.HandleText})");
    }

    [RelayCommand]
    private void StartSync()
    {
        if (MasterWindow is null)
        {
            _logger.Warning("Select a master window first.");
            StatusText = "No master selected";
            return;
        }

        var targets = Windows
            .Where(w => w.IsSelected && !w.IsMaster)
            .Select(w => w.Info.Handle)
            .ToList();

        if (targets.Count == 0)
        {
            _logger.Warning("Select at least one target window.");
            StatusText = "No targets selected";
            return;
        }

        try
        {
            _syncEngine.Configure(
                MasterWindow.Info.Handle,
                targets,
                Settings,
                CurrentProfile.ExcludeRegions);

            _scanner.ConfigureAutoReconnect(
                Settings.AutoReconnect,
                Windows
                    .Where(w => w.IsSelected && !w.IsMaster)
                    .Select(w => WindowMatchRule.FromWindow(w.Info)));

            _syncEngine.Start();
            IsSyncRunning = true;
            StatusText = $"Syncing to {targets.Count} target(s)";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to start sync");
            StatusText = "Start failed";
        }
    }

    [RelayCommand]
    private void StopSync()
    {
        _syncEngine.Stop();
        _scanner.ConfigureAutoReconnect(false, []);
        IsSyncRunning = false;
        IsSyncPaused = false;
        StatusText = "Stopped";
    }

    [RelayCommand]
    private void TogglePause()
    {
        _syncEngine.TogglePause();
        IsSyncPaused = _syncEngine.State == SyncState.Paused;
        StatusText = IsSyncPaused ? "Paused" : "Running";
    }

    [RelayCommand]
    private async Task SaveProfileAsync()
    {
        var profile = BuildCurrentProfile();
        await _profiles.SaveProfileAsync(profile);
        await _profiles.SaveSettingsAsync(Settings);
        _logger.Info($"Profile saved: {profile.Name}");
        StatusText = $"Saved {profile.Name}";
    }

    [RelayCommand]
    private async Task LoadProfileAsync()
    {
        var profile = await _profiles.LoadProfileAsync(ProfileName);
        if (profile is null)
        {
            _logger.Warning($"Profile not found: {ProfileName}");
            return;
        }

        ApplyProfile(profile);
        _logger.Info($"Profile loaded: {profile.Name}");
        StatusText = $"Loaded {profile.Name}";
    }

    [RelayCommand]
    private void RegisterHotkeys()
    {
        if (_hotkeys.Register(CurrentProfile))
        {
            _logger.Info("Hotkeys registered.");
        }
        else
        {
            _logger.Warning("Failed to register one or more hotkeys.");
        }
    }

    public SyncProfile CurrentProfile => new()
    {
        Name = ProfileName,
        MasterRule = MasterWindow is not null ? WindowMatchRule.FromWindow(MasterWindow.Info) : null,
        TargetRules = Windows
            .Where(w => w.IsSelected && !w.IsMaster)
            .Select(w => WindowMatchRule.FromWindow(w.Info))
            .ToList(),
        Settings = Settings.Clone(),
        ExcludeRegions = []
    };

    private SyncProfile BuildCurrentProfile() => CurrentProfile;

    private void ApplyProfile(SyncProfile profile)
    {
        ProfileName = profile.Name;
        Settings = profile.Settings.Clone();
        SandboxFilter = Settings.SandboxFilter;
        RefreshWindowsCommand.Execute(null);

        if (profile.MasterRule is not null)
        {
            var master = Windows.FirstOrDefault(w => profile.MasterRule.Matches(w.Info));
            if (master is not null)
            {
                SetMasterCommand.Execute(master);
            }
        }

        foreach (var rule in profile.TargetRules)
        {
            var target = Windows.FirstOrDefault(w => rule.Matches(w.Info));
            if (target is not null)
            {
                target.IsSelected = true;
            }
        }

        RegisterHotkeys();
    }

    private void OnTargetsReconnected(object? sender, IReadOnlyList<WindowInfo> windows)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            foreach (var info in windows)
            {
                var existing = Windows.FirstOrDefault(w =>
                    w.Info.ProcessName == info.ProcessName &&
                    w.Info.Title == info.Title &&
                    w.Info.SandboxName == info.SandboxName);

                if (existing is not null)
                {
                    existing.RefreshFrom(info);
                    existing.IsSelected = true;
                }
                else
                {
                    Windows.Add(new WindowItemViewModel(info) { IsSelected = true });
                }
            }

            if (_syncEngine.State == SyncState.Running)
            {
                var handles = Windows
                    .Where(w => w.IsSelected && !w.IsMaster)
                    .Select(w => w.Info.Handle)
                    .ToList();
                _syncEngine.UpdateTargets(handles);
                _logger.Info($"Auto-reconnected {windows.Count} target(s).");
            }
        });
    }

    private void OnHotkeyPressed(object? sender, HotkeyAction action)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            switch (action)
            {
                case HotkeyAction.Start:
                    StartSyncCommand.Execute(null);
                    break;
                case HotkeyAction.Stop:
                    StopSyncCommand.Execute(null);
                    break;
                case HotkeyAction.Pause:
                    TogglePauseCommand.Execute(null);
                    break;
            }
        });
    }
}
