using System.Windows;
using System.Windows.Controls;
using SandboxSync.Models;
using SandboxSync.ViewModels;
using Wpf.Ui.Controls;

namespace SandboxSync.Views;

public partial class SettingsFlyout : FluentWindow
{
    private readonly MainViewModel _viewModel;

    public SettingsFlyout(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ReplicationModeCombo.SelectedIndex = _viewModel.Settings.ReplicationMode switch
        {
            InputReplicationMode.PostMessage => 1,
            InputReplicationMode.SendInput => 2,
            _ => 0
        };
    }

    private void ReplicationModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        _viewModel.Settings.ReplicationMode = ReplicationModeCombo.SelectedIndex switch
        {
            1 => InputReplicationMode.PostMessage,
            2 => InputReplicationMode.SendInput,
            _ => InputReplicationMode.Hybrid
        };
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
