using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SandboxSync.Core;
using SandboxSync.Interop;
using SandboxSync.Services;
using SandboxSync.ViewModels;
using SandboxSync.Views;
using Wpf.Ui.Appearance;

namespace SandboxSync;

public partial class App : Application
{
    private IHost? _host;

    public static IServiceProvider Services =>
        ((App)Current)._host?.Services
        ?? throw new InvalidOperationException("Application host is not initialized.");

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DpiHelper.EnablePerMonitorV2();

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(ConfigureServices)
            .Build();

        await _host.StartAsync().ConfigureAwait(true);

        ApplicationThemeManager.Apply(ApplicationTheme.Dark);

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<LoggingService>();
        services.AddSingleton<ProfileService>();
        services.AddSingleton<SandboxDetectorService>();
        services.AddSingleton<WindowScannerService>();
        services.AddSingleton<HotkeyService>();
        services.AddSingleton<ClickOverlayService>();
        services.AddSingleton<FeedbackGuard>();
        services.AddSingleton<CoordinateMapper>();
        services.AddSingleton<InputReplicator>();
        services.AddSingleton<LowLevelMouseHook>();
        services.AddSingleton<LowLevelKeyboardHook>();
        services.AddSingleton<SyncEngine>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        services.AddTransient<SettingsFlyout>();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            var syncEngine = _host.Services.GetService<SyncEngine>();
            syncEngine?.Stop();

            var hotkeyService = _host.Services.GetService<HotkeyService>();
            hotkeyService?.Dispose();

            var mouseHook = _host.Services.GetService<LowLevelMouseHook>();
            mouseHook?.Dispose();

            var keyboardHook = _host.Services.GetService<LowLevelKeyboardHook>();
            keyboardHook?.Dispose();

            var overlay = _host.Services.GetService<ClickOverlayService>();
            overlay?.Dispose();

            await _host.StopAsync().ConfigureAwait(true);
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
