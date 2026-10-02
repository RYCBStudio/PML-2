using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.ViewModels;
using MEFrpLauncherX.ViewModels.Controls;

namespace MEFrpLauncherX.Views;

/// <summary>
///     「迁移隧道」目标节点选择对话框（26.4）。
///     只负责收集用户选择的目标节点，不发起任何删除/创建请求；
///     ShowDialog 返回值为选中的 <see cref="TunnelNodeViewModel" />，取消或未选择时为 null。
/// </summary>
public partial class TunnelMigrateWindow : Window
{
    private readonly TunnelMigrateViewModel _viewModel;

    public TunnelMigrateWindow(string proxyName, int sourceNodeId, string sourceNodeName, string sourceProxyType)
    {
        InitializeComponent();
        _viewModel = new TunnelMigrateViewModel(proxyName, sourceNodeId, sourceNodeName, sourceProxyType);
        DataContext = _viewModel;
        Opened += OnWindowOpened;
        Closed += OnWindowClosed;
    }

    private async void OnWindowOpened(object? sender, EventArgs e) => await _viewModel.LoadNodesAsync();

    private void OnWindowClosed(object? sender, EventArgs e) => _viewModel.CancelProbe();

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedNode is null)
        {
            Growl.Warning(Languages.Text_UserProxy_MigrateSelectNodeRequired);
            return;
        }

        Close(_viewModel.SelectedNode);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);

    private async void OnProbeLatency(object? sender, RoutedEventArgs e) => await _viewModel.ProbeLatencyAsync();
}
