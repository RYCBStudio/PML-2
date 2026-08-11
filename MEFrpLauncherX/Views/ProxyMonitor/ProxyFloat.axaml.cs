using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Core.WindowServices;
using MEFrpLauncherX.NetworkMonitoring;
using ReactiveUI;

namespace MEFrpLauncherX.Views.ProxyMonitor;

public partial class ProxyFloat : Window
{
    public static ProxyFloat? Instance;

    private readonly Button _menuButton;
    private readonly FlyoutBase _menuFlyout;
    private readonly ProxyFloatViewModel _vm;

    public ProxyFloat(ProxyFloatViewModel? vm = null)
    {
        InitializeComponent();
        if (vm is null)
        {
            vm = new ProxyFloatViewModel();
        }

        _vm = vm;
        DataContext = _vm;

        // Find the "..." Button and attach events
        _menuButton = this.FindControl<Button>("MenuButton");
        _menuFlyout = FlyoutBase.GetAttachedFlyout(_menuButton);

        _menuButton.PointerEntered += OnPointerEnter;
        _menuButton.PointerExited += OnPointerExited;

        if (_menuFlyout != null)
        {
            _menuFlyout.Opened += OnFlyoutOpened;
            _menuFlyout.Closed += OnFlyoutClosed;
        }

        Instance = this;
    }

    private async void Setup(object? sender, RoutedEventArgs e)
    {
        Position = WindowPositionHelper.GetPosition(this, ConfigManager.CurrentConfig.PMSettings.Position);
        Topmost = true;
        ClickThroughHelper.SetClickThrough(this, true);

        // 启动网络监控
        await _vm.StartNetworkMonitoringAsync();
    }

    protected override void OnClosed(EventArgs e)
    {
        // 清理网络监控资源
        _vm.StopNetworkMonitoring();
        base.OnClosed(e);
    }

    private void OnPointerEnter(object? sender, PointerEventArgs e) => ClickThroughHelper.SetClickThrough(this, false);

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        // Only re-enable click-through if the flyout is not open
        if (_menuFlyout is { IsOpen: false })
        {
            ClickThroughHelper.SetClickThrough(this, true);
        }
    }

    private void OnFlyoutOpened(object? sender, EventArgs e) => ClickThroughHelper.SetClickThrough(this, false);

    private void OnFlyoutClosed(object? sender, EventArgs e) => ClickThroughHelper.SetClickThrough(this, true);
}

public class ProxyFloatViewModel : ViewModelBase
{
    public static ProxyFloatViewModel? Instance;

    private readonly INetworkMonitor _networkMonitor;
    private long _lastBytesReceived;
    private long _lastBytesSent;
    private string _monitoredInterfaceId;
    private bool _safeFlag;

    public ProxyFloatViewModel()
    {
        Instance = this;
        _networkMonitor = new CrossPlatformNetworkMonitor();
        _networkMonitor.TrafficUpdated += OnTrafficUpdated;
    }

    public string WindowTitle
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = Languages.Text_ProxyFloat_WindowTitle;

    public AvaloniaList<string> Proxies
    {
        get;
    } = [];

    public int TrafficIn
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public int TrafficOut
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public async Task StartNetworkMonitoringAsync()
    {
        try
        {
            // 获取网络接口
            var interfaces = await _networkMonitor.GetNetworkInterfacesAsync();
            var primaryInterface = interfaces.FirstOrDefault(i => i.IsOperational);

            if (primaryInterface != null)
            {
                _monitoredInterfaceId = primaryInterface.Id;

                // 获取初始流量数据
                var initialTraffic = await _networkMonitor.GetTrafficDataAsync(_monitoredInterfaceId);
                _lastBytesReceived = initialTraffic.TotalBytesReceived;
                _lastBytesSent = initialTraffic.TotalBytesSent;

                // 开始监控，每2秒更新一次
                _networkMonitor.StartMonitoring(_monitoredInterfaceId, TimeSpan.FromSeconds(2), initialTraffic);
            }
        }
        catch (Exception ex)
        {
            // 可以记录日志或显示错误信息
            Core.App.CurrentLogger?.Error(ex, "网络监控启动失败");
        }
    }

    public void StopNetworkMonitoring()
    {
        if (!string.IsNullOrEmpty(_monitoredInterfaceId))
        {
            _networkMonitor.StopMonitoring(_monitoredInterfaceId);
        }

        _networkMonitor.TrafficUpdated -= OnTrafficUpdated;
        (_networkMonitor as IDisposable)?.Dispose();
    }

    private void OnTrafficUpdated(object? sender, NetworkTraffic traffic)
    {
        // 计算增量流量（从上次更新到现在的变化）
        var deltaReceived = traffic.TotalBytesReceived - _lastBytesReceived;
        var deltaSent = traffic.TotalBytesSent - _lastBytesSent;

        // 更新UI属性（在UI线程上）
        Dispatcher.UIThread.Post(() =>
        {
            TrafficIn = (int)deltaReceived;
            TrafficOut = (int)deltaSent;
        });

        // 保存当前值用于下次计算
        _lastBytesReceived = traffic.TotalBytesReceived;
        _lastBytesSent = traffic.TotalBytesSent;
    }
}

public class ProxiesToTextConverter : IValueConverter
{
    public static ProxiesToTextConverter Instance => new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is IEnumerable<string> proxies)
        {
            return string.Join("\t", proxies);
        }

        return "";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}