using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SandboxSync.ViewModels;
using Wpf.Ui.Controls;

namespace SandboxSync.Views;

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;
    private SettingsFlyout? _settingsFlyout;

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        Loaded += OnLoadedAsync;
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoadedAsync;
        await _viewModel.InitializeAsync();
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        _settingsFlyout ??= App.Services.GetRequiredService<SettingsFlyout>();
        _settingsFlyout.Owner = this;
        _settingsFlyout.ShowDialog();
    }
}
