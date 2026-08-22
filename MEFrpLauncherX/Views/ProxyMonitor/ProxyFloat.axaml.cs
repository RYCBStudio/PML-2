using System;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Core.WindowServices;
using MEFrpLauncherX.NetworkMonitoring;
using MEFrpLauncherX.ViewModels;
using ReactiveUI;
using SkiaSharp;

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
        ApplySettings();

        // 启动网络监控
        await _vm.StartNetworkMonitoringAsync();
    }

    /// <summary>
    ///     应用悬浮窗设置（穿透 / 不透明度 / 图表可见性），设置窗口修改后调用
    /// </summary>
    public void ApplySettings()
    {
        var pm = ConfigManager.CurrentConfig.PMSettings;
        _vm.ShowChart = pm.ShowChart;
        // 透明度统一走 VM 绑定（内容层 Border.Opacity）推送，避免直接改 Window.Opacity 与绑定双通道冲突
        // 及 Win32 上 Window.Opacity(LWA_ALPHA) 与 Transparent 透明合成叠加导致背景不透明的问题
        _vm.Opacity = pm.Opacity;
        ClickThroughHelper.SetClickThrough(this, pm.ClickThrough);
    }

    protected override void OnClosed(EventArgs e)
    {
        // 清理网络监控资源
        _vm.StopNetworkMonitoring();
        base.OnClosed(e);
    }

    // 穿透模式（ClickThrough=true）下：移入取消穿透便于操作，移出/菜单关闭恢复穿透
    private void OnPointerEnter(object? sender, PointerEventArgs e)
    {
        if (ConfigManager.CurrentConfig.PMSettings.ClickThrough)
        {
            ClickThroughHelper.SetClickThrough(this, false);
        }
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (!ConfigManager.CurrentConfig.PMSettings.ClickThrough)
        {
            return;
        }

        if (_menuFlyout is { IsOpen: false })
        {
            ClickThroughHelper.SetClickThrough(this, true);
        }
    }

    private void OnFlyoutOpened(object? sender, EventArgs e)
    {
        if (ConfigManager.CurrentConfig.PMSettings.ClickThrough)
        {
            ClickThroughHelper.SetClickThrough(this, false);
        }
    }

    private void OnFlyoutClosed(object? sender, EventArgs e)
    {
        if (ConfigManager.CurrentConfig.PMSettings.ClickThrough)
        {
            ClickThroughHelper.SetClickThrough(this, true);
        }
    }

    private void HideRequested(object? sender, RoutedEventArgs e)
    {
        this.Hide();
    }
}

public class ProxyFloatViewModel : ViewModelBase
{
    public static ProxyFloatViewModel? Instance;

    /// <summary>图表采样点数（2 秒/点 ≈ 1 分钟窗口）</summary>
    private const int HistoryCapacity = 30;

    private const double SampleIntervalSeconds = 2.0;

    private readonly INetworkMonitor _networkMonitor;
    private long _lastBytesReceived;
    private long _lastBytesSent;
    private string _monitoredInterfaceId = string.Empty;

    public ProxyFloatViewModel()
    {
        Instance = this;
        _networkMonitor = new CrossPlatformNetworkMonitor();
        _networkMonitor.TrafficUpdated += OnTrafficUpdated;

        // 26.3 M6b：悬浮窗「⋯」菜单真实命令
        RefreshTrafficCommand = ReactiveCommand.Create(RefreshTraffic);
        OpenSettingsCommand = ReactiveCommand.Create(OpenSettings);
        CloseFloatCommand = ReactiveCommand.Create(CloseFloat);

        var pm = ConfigManager.CurrentConfig.PMSettings;
        ShowChart = pm.ShowChart;
        Opacity = pm.Opacity;

        // 26.3 M6b-Extended：实时流量折线图（下载绿 / 上传蓝）
        ChartSeries = new AvaloniaList<ISeries>
        {
            new LineSeries<double>
            {
                Values = DownloadHistory,
                Name = Languages.Text_Traffic_Inbound,
                GeometrySize = 0,
                LineSmoothness = 0.65,
                Stroke = new SolidColorPaint(new SKColor(0, 184, 148), 2)
            },
            new LineSeries<double>
            {
                Values = UploadHistory,
                Name = Languages.Text_Traffic_Outbound,
                GeometrySize = 0,
                LineSmoothness = 0.65,
                Stroke = new SolidColorPaint(new SKColor(9, 132, 227), 2)
            }
        };
        ChartXAxes = [new Axis { IsVisible = false }];
        ChartYAxes = [new Axis { Labeler = ProcessFileSize, MinLimit = 0 }];
    }

    /// <summary>刷新流量</summary>
    public ReactiveCommand<Unit, Unit> RefreshTrafficCommand { get; }

    /// <summary>打开悬浮窗设置</summary>
    public ReactiveCommand<Unit, Unit> OpenSettingsCommand { get; }

    /// <summary>关闭悬浮窗（不退出应用）</summary>
    public ReactiveCommand<Unit, Unit> CloseFloatCommand { get; }

    public string WindowTitle
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = Languages.Text_ProxyFloat_WindowTitle;

    /// <summary>运行中的隧道列表（与终端页启停真实同步）</summary>
    public AvaloniaList<FloatTunnelItem> TunnelItems
    {
        get;
    } = [];

    /// <summary>暂无运行中的隧道（空态提示）</summary>
    public bool HasNoTunnels
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = true;

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

    /// <summary>是否显示流量折线图（由设置驱动）</summary>
    public bool ShowChart
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>窗口不透明度（由设置驱动）</summary>
    public double Opacity
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    // ===== 图表 =====

    public AvaloniaList<double> DownloadHistory
    {
        get;
    } = [];

    public AvaloniaList<double> UploadHistory
    {
        get;
    } = [];

    public AvaloniaList<ISeries> ChartSeries
    {
        get;
        private set;
    }

    public AvaloniaList<Axis> ChartXAxes
    {
        get;
        private set;
    }

    public AvaloniaList<Axis> ChartYAxes
    {
        get;
        private set;
    }

    public LiveChartsCore.Measure.Margin ChartDrawMargin
    {
        get;
    } = new(2); 

    // ===== 隧道状态同步（M6b-Extended）=====

    /// <summary>隧道启动（终端 Tab 创建）→ Starting</summary>
    public static void ReportTunnelStarted(string name) => UpdateTunnel(name, TunnelStatus.Starting, null);

    /// <summary>隧道停止（Ctrl+C）→ Stopped</summary>
    public static void ReportTunnelStopped(string name) => UpdateTunnel(name, TunnelStatus.Stopped, null);

    /// <summary>隧道标签关闭 → 从悬浮窗移除</summary>
    public static void ReportTunnelRemoved(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        OnUi(() =>
        {
            if (Instance is null)
            {
                return;
            }

            var item = Instance.TunnelItems.FirstOrDefault(t => t.Name == name);
            if (item != null)
            {
                Instance.TunnelItems.Remove(item);
                Instance.HasNoTunnels = Instance.TunnelItems.Count == 0;
            }
        });
    }

    /// <summary>
    ///     隧道状态细节同步（Running / Reconnecting / Failed，来自管理页 VM 的状态流转）
    /// </summary>
    public static void ReportTunnelStatus(string name, TunnelStatus status, string? errorSummary)
    {
        if (status is not (TunnelStatus.Running or TunnelStatus.Reconnecting or TunnelStatus.Failed))
        {
            return;
        }

        UpdateTunnel(name, status, errorSummary);
    }

    private static void UpdateTunnel(string name, TunnelStatus status, string? errorSummary)
    {
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        OnUi(() =>
        {
            if (Instance is null)
            {
                return;
            }

            var item = Instance.TunnelItems.FirstOrDefault(t => t.Name == name);
            if (item is null)
            {
                Instance.TunnelItems.Add(new FloatTunnelItem(name, status) { ErrorSummary = errorSummary });
                Instance.HasNoTunnels = false;
            }
            else
            {
                item.ErrorSummary = errorSummary;
                item.Status = status;
            }
        });
    }

    private static void OnUi(Action action)
    {
        if (Instance is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(action);
    }

    // ===== 网络监控 =====

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

    /// <summary>
    ///     刷新流量统计：重置基准并重新开始监控（悬浮窗「刷新流量」菜单）
    /// </summary>
    public void RefreshTraffic() => _ = RefreshTrafficAsync();

    private async Task RefreshTrafficAsync()
    {
        try
        {
            if (!string.IsNullOrEmpty(_monitoredInterfaceId))
            {
                _networkMonitor.StopMonitoring(_monitoredInterfaceId);
            }

            // 重新获取网络接口并重置流量基准
            var interfaces = await _networkMonitor.GetNetworkInterfacesAsync();
            var primaryInterface = interfaces.FirstOrDefault(i => i.IsOperational);
            if (primaryInterface != null)
            {
                _monitoredInterfaceId = primaryInterface.Id;
                var initialTraffic = await _networkMonitor.GetTrafficDataAsync(_monitoredInterfaceId);
                _lastBytesReceived = initialTraffic.TotalBytesReceived;
                _lastBytesSent = initialTraffic.TotalBytesSent;
                _networkMonitor.StartMonitoring(_monitoredInterfaceId, TimeSpan.FromSeconds(2), initialTraffic);

                // 清空历史曲线，从新基准重新绘制
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    DownloadHistory.Clear();
                    UploadHistory.Clear();
                });
            }
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger?.Error(ex, "刷新悬浮窗流量失败");
        }
    }

    private void OpenSettings() => new ProxyFloatSettings().Show();

    private void CloseFloat() => ProxyFloat.Instance?.Close();

    private void OnTrafficUpdated(object? sender, NetworkTraffic traffic)
    {
        // 计算增量流量（从上次更新到现在的变化）
        var deltaReceived = traffic.TotalBytesReceived - _lastBytesReceived;
        var deltaSent = traffic.TotalBytesSent - _lastBytesSent;

        // 保存当前值用于下次计算
        _lastBytesReceived = traffic.TotalBytesReceived;
        _lastBytesSent = traffic.TotalBytesSent;

        // 更新UI属性（在UI线程上）
        Dispatcher.UIThread.Post(() =>
        {
            TrafficIn = (int)Math.Max(0, deltaReceived);
            TrafficOut = (int)Math.Max(0, deltaSent);

            // 追加到历史曲线（换算为 B/s），并截断到容量上限
            AppendHistory(DownloadHistory, deltaReceived / SampleIntervalSeconds);
            AppendHistory(UploadHistory, deltaSent / SampleIntervalSeconds);
        });
    }

    private static void AppendHistory(AvaloniaList<double> history, double value)
    {
        history.Add(Math.Max(0, value));
        while (history.Count > HistoryCapacity)
        {
            history.RemoveAt(0);
        }
    }

    /// <summary>
    ///     根据<paramref name="fileSize" />的大小自动返回对应的文件大小值。
    /// </summary>
    private static string ProcessFileSize(double fileSize)
    {
        string[] sizeUnits = ["B", "KB", "MB", "GB", "TB"];
        var size = fileSize;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < sizeUnits.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{Math.Round(size, 2)}{sizeUnits[unitIndex]}";
    }
}

/// <summary>
///     悬浮窗中的隧道状态项：名称 + 运行状态（颜色/文案与隧道管理页一致）
/// </summary>
public class FloatTunnelItem : ViewModelBase
{
    private static readonly IBrush _statusBrushIdle = new SolidColorBrush(Color.FromArgb(255, 138, 138, 138));
    private static readonly IBrush _statusBrushStarting = new SolidColorBrush(Color.FromArgb(255, 0, 120, 212));
    private static readonly IBrush _statusBrushRunning = new SolidColorBrush(Color.FromArgb(255, 15, 123, 15));
    private static readonly IBrush _statusBrushReconnecting = new SolidColorBrush(Color.FromArgb(255, 202, 80, 16));
    private static readonly IBrush _statusBrushStopped = new SolidColorBrush(Color.FromArgb(255, 108, 108, 108));
    private static readonly IBrush _statusBrushFailed = new SolidColorBrush(Color.FromArgb(255, 196, 43, 28));

    private TunnelStatus _status;

    public FloatTunnelItem(string name, TunnelStatus status)
    {
        Name = name;
        _status = status;
    }

    public string Name { get; }

    public TunnelStatus Status
    {
        get => _status;
        set
        {
            if (_status == value)
            {
                return;
            }

            _status = value;
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(StatusText));
            this.RaisePropertyChanged(nameof(StatusBrush));
            this.RaisePropertyChanged(nameof(StatusToolTip));
        }
    }

    /// <summary>失败摘要（Failed 时有值）</summary>
    public string? ErrorSummary { get; set; }

    /// <summary>状态徽标文案</summary>
    public string StatusText => Status switch
    {
        TunnelStatus.Starting => Languages.Text_TunnelStatus_Starting,
        TunnelStatus.Running => Languages.Text_TunnelStatus_Running,
        TunnelStatus.Reconnecting => Languages.Text_TunnelStatus_Reconnecting,
        TunnelStatus.Stopped => Languages.Text_TunnelStatus_Stopped,
        TunnelStatus.Failed => Languages.Text_TunnelStatus_Failed,
        _ => string.Empty
    };

    /// <summary>状态徽标颜色（与隧道管理页一致）</summary>
    public IBrush StatusBrush => Status switch
    {
        TunnelStatus.Starting => _statusBrushStarting,
        TunnelStatus.Running => _statusBrushRunning,
        TunnelStatus.Reconnecting => _statusBrushReconnecting,
        TunnelStatus.Stopped => _statusBrushStopped,
        TunnelStatus.Failed => _statusBrushFailed,
        _ => _statusBrushIdle
    };

    /// <summary>悬停提示：状态 + 失败原因</summary>
    public string StatusToolTip => string.IsNullOrEmpty(ErrorSummary)
        ? StatusText
        : $"{StatusText}：{ErrorSummary}";
}
