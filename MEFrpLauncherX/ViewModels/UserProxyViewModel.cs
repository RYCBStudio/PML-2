using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using MEFrpLauncherX.Controls;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Core.MEFIntegrated;
using MEFrpLauncherX.Core.Services;
using MEFrpLauncherX.Views;
using MEFrpLauncherX.Views.ProxyMonitor;
using MsBox.Avalonia.ViewModels.Commands;
using Notify.NET.Abstractions;
using Notify.NET.Builder;
using ReactiveUI;
using ProxyFloat = MEFrpLauncherX.Views.ProxyMonitor.ProxyFloat;

namespace MEFrpLauncherX.ViewModels;

/// <summary>隧道运行状态（26.3 M3）</summary>
public enum TunnelStatus
{
    Idle,
    Starting,
    Running,
    Reconnecting,
    Stopped,
    Failed
}

public class UserProxyViewModel : ViewModelBase
{
    private readonly SemaphoreSlim _signal = new SemaphoreSlim(0, 1);

    // 26.3 M3: 状态徽标颜色（与 Fluent 语义色近似）
    private static readonly IBrush _statusBrushIdle = new SolidColorBrush(Color.FromArgb(255, 138, 138, 138));
    private static readonly IBrush _statusBrushStarting = new SolidColorBrush(Color.FromArgb(255, 0, 120, 212));
    private static readonly IBrush _statusBrushRunning = new SolidColorBrush(Color.FromArgb(255, 15, 123, 15));
    private static readonly IBrush _statusBrushReconnecting = new SolidColorBrush(Color.FromArgb(255, 202, 80, 16));
    private static readonly IBrush _statusBrushStopped = new SolidColorBrush(Color.FromArgb(255, 108, 108, 108));
    private static readonly IBrush _statusBrushFailed = new SolidColorBrush(Color.FromArgb(255, 196, 43, 28));

    private CancellationTokenSource? _startupTimeoutCts;

    public UserProxyViewModel()
    {
        try
        {
            Domains = JsonSerializer.Deserialize<List<string>>(domain, App.AppJsonSerializerContext.ListString)
                ?.Distinct().ToList() ?? [];
        }
        catch
        {
            Domains = [];
        }

        StopProxyCommand = new RelayCommand<UserProxyViewModel>(StopProxy);
        LaunchProxyCommand = new RelayCommand<UserProxyViewModel>(LaunchSingleProxy);
        LaunchMultiProxyCommand = new RelayCommand<IEnumerable<UserProxyViewModel>>(LaunchProxy);
        EditProxyCommand = new RelayCommand<UserProxyViewModel>(EditProxy);
        ForceOfflineProxyCommand = new RelayCommand<UserProxyViewModel>(ForceOfflineProxy);
        DisableProxyCommand = new RelayCommand<UserProxyViewModel>(DisableProxy);
        EnableProxyCommand = new RelayCommand<UserProxyViewModel>(EnableProxy);
        DeleteProxyCommand = new RelayCommand<UserProxyViewModel>(DeleteProxy);
        GenerateLaunchConfigCommand = new RelayCommand<object>(GenerateLaunchConfig);
        ShowExtraInfoCommand = new RelayCommand<UserProxyViewModel>(ShowExtraInfo);
        LaunchProxyViaConfigCommand = new RelayCommand<UserProxyViewModel>(LaunchProxyViaConfig);
        EditSSLCommand = new RelayCommand<UserProxyViewModel>(EditSSL);
        CopyInfoCommand = new RelayCommand<UserProxyViewModel>(CopyInfo);
        CopyErrorCommand = new RelayCommand<UserProxyViewModel>(CopyError);
    }

    public UserProxyViewModel(string _domain)
    {
        try
        {
            Domains = JsonSerializer.Deserialize<List<string>>(_domain, App.AppJsonSerializerContext.ListString)
                ?.Distinct().ToList() ?? [];
        }
        catch
        {
            Domains = [];
        }
        finally
        {
            domain = _domain;
        }

        StopProxyCommand = new RelayCommand<UserProxyViewModel>(StopProxy);
        LaunchProxyCommand = new RelayCommand<UserProxyViewModel>(LaunchSingleProxy);
        LaunchMultiProxyCommand = new RelayCommand<IEnumerable<UserProxyViewModel>>(LaunchProxy);
        EditProxyCommand = new RelayCommand<UserProxyViewModel>(EditProxy);
        ForceOfflineProxyCommand = new RelayCommand<UserProxyViewModel>(ForceOfflineProxy);
        DisableProxyCommand = new RelayCommand<UserProxyViewModel>(DisableProxy);
        EnableProxyCommand = new RelayCommand<UserProxyViewModel>(EnableProxy);
        DeleteProxyCommand = new RelayCommand<UserProxyViewModel>(DeleteProxy);
        GenerateLaunchConfigCommand = new RelayCommand<object>(GenerateLaunchConfig);
        ShowExtraInfoCommand = new RelayCommand<UserProxyViewModel>(ShowExtraInfo);
        LaunchProxyViaConfigCommand = new RelayCommand<UserProxyViewModel>(LaunchProxyViaConfig);
        EditSSLCommand = new RelayCommand<UserProxyViewModel>(EditSSL);
        CopyInfoCommand = new RelayCommand<UserProxyViewModel>(CopyInfo);
        CopyErrorCommand = new RelayCommand<UserProxyViewModel>(CopyError);

        Dispatcher.UIThread.Post(async () =>
        {
            await _signal.WaitAsync();
            await Task.Delay(1000);
            await ProbeAsync();
        }, DispatcherPriority.Background);
    }

    public List<string>? Locations
    {
        get;
        init;
    }

    public string Config
    {
        get;
        set;
    }

    public bool UseConfig
    {
        get;
        set;
    }

    public int proxyId
    {
        get;
        set;
    }

    public string username
    {
        get;
        init;
    }

    public string proxyName
    {
        get;
        set;
    }

    public string proxyType
    {
        get;
        set;
    }

    public bool isBanned
    {
        get;
        set;
    }

    public bool isDisabled
    {
        get;
        set;
    }

    public string localIp
    {
        get;
        set;
    }

    public int localPort
    {
        get;
        set;
    }

    public int remotePort
    {
        get;
        set;
    }

    public string node
    {
        get;
        set;
    }

    public int nodeId
    {
        get;
        set;
    }

    public IEnumerable<string> allowedProtocols
    {
        get;
        set;
    }

    public string runId
    {
        get;
        init;
    }

    public bool isOnline
    {
        get => field;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            this.RaisePropertyChanged();
            RefreshTunnelStatus();
        }
    }

    public string domain
    {
        get;
        init;
    }

    public List<string> Domains
    {
        get;
        private set;
    }

    public Dictionary<string, string>? RequestHeaders
    {
        get;
        init;
    }

    public Dictionary<string, string>? ResponseHeaders
    {
        get;
        init;
    }

    public int lastStartTime
    {
        get;
        init;
    }

    public int lastCloseTime
    {
        get;
        init;
    }

    public string clientVersion
    {
        get;
        init;
    }

    public string proxyProtocolVersion
    {
        get;
        init;
    }

    public bool useEncryption
    {
        get;
        init;
    }

    public bool useCompression
    {
        get;
        init;
    }

    public string location
    {
        get;
        init;
    }

    public string accessKey
    {
        get;
        init;
    }

    public string hostHeaderRewrite
    {
        get;
        init;
    }

    public string headerXFromWhere
    {
        get;
        init;
    }

    public string transportProtocol
    {
        get;
        init;
    }

    public string httpUser
    {
        get;
        init;
    }

    public string httpPassword
    {
        get;
        init;
    }

    public string crtPath
    {
        get;
        set;
    }

    public string keyPath
    {
        get;
        set;
    }

    public bool IsSelected
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ICommand LaunchProxyCommand
    {
        get;
    }

    public ICommand StopProxyCommand
    {
        get;
    }

    public ICommand LaunchMultiProxyCommand
    {
        get;
    }

    public ICommand EditProxyCommand
    {
        get;
    }

    public ICommand ForceOfflineProxyCommand
    {
        get;
    }

    public ICommand DisableProxyCommand
    {
        get;
    }

    public ICommand EnableProxyCommand
    {
        get;
    }

    public ICommand DeleteProxyCommand
    {
        get;
    }

    public ICommand GenerateLaunchConfigCommand
    {
        get;
    }

    public ICommand ShowExtraInfoCommand
    {
        get;
    }

    public ICommand LaunchProxyViaConfigCommand
    {
        get;
    }

    public bool IsLaunched
    {
        get => field;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            this.RaisePropertyChanged();
            RefreshTunnelStatus();
        }
    }

    public bool IsLoading
    {
        get => field;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            this.RaisePropertyChanged();
            RefreshTunnelStatus();
        }
    }

    public bool Detailed
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>统一隧道运行状态（26.3 M3）</summary>
    public TunnelStatus TunnelStatus
    {
        get => field;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(StatusText));
            this.RaisePropertyChanged(nameof(StatusBrush));
            this.RaisePropertyChanged(nameof(HasError));

            // 26.3 M6b-Extended：与流量悬浮窗同步状态细节（Running/Reconnecting/Failed）
            if (value is TunnelStatus.Running or TunnelStatus.Reconnecting or TunnelStatus.Failed)
            {
                ProxyFloatViewModel.ReportTunnelStatus(proxyName, value, LastErrorSummary);
            }
        }
    }

    /// <summary>状态徽标文案；Idle 返回空串（不显示）</summary>
    public string StatusText => TunnelStatus switch
    {
        TunnelStatus.Starting => Languages.Text_TunnelStatus_Starting,
        TunnelStatus.Running => Languages.Text_TunnelStatus_Running,
        TunnelStatus.Reconnecting => Languages.Text_TunnelStatus_Reconnecting,
        TunnelStatus.Stopped => Languages.Text_TunnelStatus_Stopped,
        TunnelStatus.Failed => Languages.Text_TunnelStatus_Failed,
        _ => string.Empty
    };

    /// <summary>状态徽标颜色</summary>
    public IBrush StatusBrush => TunnelStatus switch
    {
        TunnelStatus.Starting => _statusBrushStarting,
        TunnelStatus.Running => _statusBrushRunning,
        TunnelStatus.Reconnecting => _statusBrushReconnecting,
        TunnelStatus.Stopped => _statusBrushStopped,
        TunnelStatus.Failed => _statusBrushFailed,
        _ => _statusBrushIdle
    };

    /// <summary>是否处于失败态且存在可复制的原因摘要</summary>
    public bool HasError => TunnelStatus == TunnelStatus.Failed && !string.IsNullOrEmpty(LastErrorSummary);

    /// <summary>最近终端输出（滚动保留最近 20 行）</summary>
    public string LastOutputBuffer
    {
        get => field;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>最近一次失败原因（映射后的可读文案，非原始输出）</summary>
    public string LastErrorSummary
    {
        get => field;
        private set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            this.RaisePropertyChanged();
            this.RaisePropertyChanged(nameof(HasError));
        }
    }

    /// <summary>节点延迟（毫秒），探测失败为 null</summary>
    public long? LatencyMs
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(ProbeStatusText));
        }
    }

    /// <summary>最近一次探测状态</summary>
    public ProbeStatus ProbeStatus
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(ProbeStatusText));
        }
    }

    /// <summary>最近一次探测时间；null 表示尚未探测</summary>
    public DateTimeOffset? MeasuredAt
    {
        get => field;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(ProbeStatusText));
        }
    }

    /// <summary>是否正在探测</summary>
    public bool IsProbing
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>
    ///     探测状态展示文案：
    ///     Ok 显示延迟毫秒；Timeout/Error/NotProbeable 显示对应徽标；未探测返回空串。
    /// </summary>
    public string ProbeStatusText => MeasuredAt == null
        ? string.Empty
        : ProbeStatus switch
        {
            ProbeStatus.Ok => LatencyMs.HasValue
                ? LatencyMs <= 0
                    ? Languages.Text_Nodes_ProbeFailed
                    : string.Format(Languages.Text_Nodes_LatencyMsFormat, LatencyMs.Value)
                : Languages.Text_Nodes_ProbeFailed,
            ProbeStatus.Timeout => Languages.Text_Nodes_ProbeTimeout,
            ProbeStatus.Error => Languages.Text_Nodes_ProbeFailed,
            ProbeStatus.NotProbeable => Languages.Text_Nodes_ProbeNotAvailable,
            _ => string.Empty
        };

    public ICommand EditSSLCommand
    {
        get;
        set;
    }

    public ICommand CopyInfoCommand
    {
        get;
        set;
    }

    public ICommand CopyErrorCommand
    {
        get;
        set;
    }

    public InfoClasses.Nodes? Node
    {
        get;
        init
        {
            field = value;
            if (value?.hostname.IsNullOrEmpty() == false)
            {
                _signal.Release();
            }
        }
    }

    public bool ShowExtraMenu
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string httpPlugin
    {
        get;
        init;
    }

    private async void LaunchProxyViaConfig(object parameter)
    {
        Core.App.CurrentLogger.Log("使用配置文件启动单个隧道操作", port: EnumLogPort.Client, module: EnumLogModule.Main);
        var proxy = parameter as UserProxyViewModel;
        List<string> configFiles =
        [
            .. Directory
                .EnumerateFileSystemEntries(Path.Combine(Core.App.StartupPath, "Config", "frp"), "*.*",
                    SearchOption.TopDirectoryOnly)
                .Where(fs => fs.EndsWithEx(".ini,.json,.toml,.yaml,.yml") && Path.GetFileNameWithoutExtension(fs)
                    .Contains(proxy.proxyName, StringComparison.OrdinalIgnoreCase))
        ];

        var configFile = string.Empty;
        IReadOnlyList<IStorageFile>? cfg = [];
        if (configFiles.Count != 0)
        {
            var cs = new ConfigSelect(configFiles);
            var cd = new ContentDialog
            {
                Title = Languages.Text_ALPControl_SelectConfigTitle,
                Content = cs,
                PrimaryButtonText = Languages.Text_Global_Confirm,
                CloseButtonText = Languages.Text_Global_Cancel
            };
            ShowExtraMenu = false;
            if (await cd.ShowAsync(TopLevel.GetTopLevel(Core.App.MainWindow)) == ContentDialogResult.Primary)
            {
                configFile = cs.SelectedPath;
            }
        }
        else
        {
            cfg = await Core.App.MainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Languages.Text_ALPControl_SelectConfigTitle,
                AllowMultiple = false,
                FileTypeFilter = [FilePickerFileTypes.All]
            });
        }

        if (cfg is not null)
        {
            try
            {
                configFile = configFile.IsNullOrEmpty() ? cfg?[0].Path.AbsolutePath : configFile;
            }
            catch (ArgumentOutOfRangeException)
            {
                Growl.Warning(string.Format(Languages.Text_UserProxy_LaunchCancelledFormat, proxy?.proxyName));
                return;
            }
            catch (Exception ex)
            {
                Core.App.CurrentLogger.Log($"使用配置文件启动单个隧道操作失败: {ex.Message}", port: EnumLogPort.Client,
                    module: EnumLogModule.Main);
                Core.App.CurrentLogger.Error(ex);
                Growl.Error(string.Format(Languages.Text_UserProxy_LaunchCancelledErrorFormat, proxy?.proxyName,
                    ex.Message));
                return;
            }
        }

        if (configFile.IsNullOrEmpty())
        {
            Growl.Warning(string.Format(Languages.Text_UserProxy_LaunchCancelledFormat, proxy?.proxyName));
            return;
        }

        var _res = await new ConfigEditor(configFile).ShowDialog<bool>(Core.App.MainWindow);
        if (!_res)
        {
            Growl.Error(string.Format(Languages.Text_UserProxy_LaunchCancelledFormat, proxy?.proxyName));
            return;
        }

        LaunchViaConfigImpl(proxy, configFile);
    }

    private async void LaunchViaConfigImpl(UserProxyViewModel proxy, string configFile)
    {
        Core.App.CurrentLogger.Log($"正在使用指定配置启动隧道 {proxy?.proxyName}", port: EnumLogPort.Client,
            module: EnumLogModule.Main);


        var cmd = $"{{mefrpc}} -c \"{configFile}\"";
        if (await IsClientFileValidAsync() == 0) // 用户取消启动
        {
            IsLoading = false;
            return;
        }

        MainPageFrameViewModel.TerminalPage ??= new TerminalPage();
        MainPageFrameViewModel.TerminalPage.CreateNewTerminalWithoutNotification(cmd, proxy.proxyName);
        // 26.3 M6b-Extended：隧道启动 → 悬浮窗状态同步（TerminalPage 内也会报告，此处保持既有语义）
        ProxyFloatViewModel.ReportTunnelStarted(proxy.proxyName);
        IsLoading = false;
        MainPageFrameViewModel.Instance.NavigateToPage("Terminal");
        MainPageFrameViewModel.Instance.CurrentPage = MainPageFrameViewModel.TerminalPage;
        if (ConfigManager.CurrentConfig.PMSettings.Enabled)
        {
            ProxyFloat.Instance?.Show();
        }

        Growl.Success(string.Format(Languages.Text_UserProxy_LaunchSucceededFormat, proxy?.proxyName));
        IsLaunched = true;
    }

    /// <summary>
    ///     检查客户端文件是否存在且校验通过，如果不通过则提示下载
    /// </summary>
    /// <returns>如果同意下载, 返回下载后的重新检验值; 如果取消启动, 返回 0; 如果直接启动, 返回 -1.</returns>
    private async Task<int> IsClientFileValidAsync()
    {
        // 定义不同平台的客户端配置
        var clientConfig = OperatingSystem.IsWindows()
            ? (FileName: "mefrpc.exe",
                Md5Hash:
                "3b667ad96332c3ded5f53fd0f3a35d07|7877aebbb5d28b075fe6ff5f823863ce|" + //v0.67.0_20260214_7d549bc1
                "e2d4e8cd4fbd4f14d8101aaf4baaacec|a2b4fa6b50b05c3ebf5b888e2e07590c|" + //v0.67.0_20260302_f1907e56
                "aef147c9899db111714f60396e4b28a5|8255cc73f6ddf23be05de69e75f80aee" //v0.67.1_20260626_af59eefd
            )
            : OperatingSystem.IsLinux()
                ? (FileName: "mefrpc.tar",
                    Md5Hash:
                    "e402ab9d90ce932339d920a398480ab9|ad07416756ca770ca1bb85463d782737" //v0.67.0_20260214_7d549bc1
                    + "|" +
                    "f5236d0899b118a66df5f62548c4d4b8|98948bb0b2adfefc65a3fca19c41b8a6" //v0.67.0_20260302_f1907e56
                    + "|" +
                    "5807ad402baa8ce8d81189d59af3caf1|1d2bd98a2195dfc70578636f01fb07a8" //v0.67.1_20260626_af59eefd
                )
                : (FileName: "mefrpc.tar",
                    Md5Hash:
                    "36020a261e451e1031d0f76f89627ac0|081271cc3cdd7c6b48b8660a6ecddf73" //v0.67.0_20260214_7d549bc1
                    + "|" +
                    "02a4520ebbf57f7e641585e4654b8237|817ea7509443af93092ae74495669ee2" //v0.67.0_20260302_f1907e56
                    + "|" +
                    "dfc656a83be01e772770b24a9e5447f6|5fd47701afa5c9fd2c1978a58dadc12c" //v0.67.1_20260626_af59eefd 
                );
        // 不支持的平台直接返回
        if (string.IsNullOrEmpty(clientConfig.FileName))
        {
            return -1;
        }

        var filePath = Path.Combine(Core.App.StartupPath, "bin", clientConfig.FileName);

        // 文件校验通过，直接启动
        if (DownloadHelper.ValidateFileSimple(filePath, clientConfig.Md5Hash))
        {
            return -1;
        }

        // 文件校验失败，提示用户
        var res = await MessageBox.ShowAsync(
            string.Format(Languages.Text_UserProxy_ClientFileCheckFailedFormat, clientConfig.FileName),
            Languages.Caption_Warning,
            "",
            MessageBoxIcon.Warning,
            [
                new TaskDialogButton(Languages.Text_UserProxy_Download, TaskDialogStandardResult.Yes),
                new TaskDialogButton(Languages.Text_Global_No, TaskDialogStandardResult.No)
            ]);

        return res switch
        {
            MessageBoxResult.No => 0, // 取消启动
            MessageBoxResult.Yes => await DownloadAndRevalidateAsync(), // 下载后重新验证
            _ => -1 // 其他情况直接启动
        };
    }

    /// <summary>
    ///     下载客户端并重新验证
    /// </summary>
    private async Task<int> DownloadAndRevalidateAsync()
    {
        await new DownloadHelper(Core.App.MainWindow).DownloadMEFrpClient(Environment.OSVersion);
        return await IsClientFileValidAsync();
    }

    private void LaunchProxy(object parameter)
    {
        var proxies = parameter as IEnumerable<UserProxyViewModel>;
        Core.App.CurrentLogger.Log("启动多个隧道操作", port: EnumLogPort.Client, module: EnumLogModule.Main);
        // 批量启动逻辑
        foreach (var p in proxies)
        {
            Core.App.CurrentLogger.Log($"准备启动隧道 {p.proxyName}", port: EnumLogPort.Client, module: EnumLogModule.Main);
            LaunchSingleProxy(p);
        }
    }

    private async void LaunchSingleProxy(object para)
    {
        IsLoading = true;
        Core.App.CurrentLogger.Log("启动单个隧道操作", port: EnumLogPort.Client, module: EnumLogModule.Main);
        var proxy = para as UserProxyViewModel;
        Core.App.CurrentLogger.Log($"正在启动隧道 {proxy.proxyName}", port: EnumLogPort.Client, module: EnumLogModule.Main);
        try
        {
            // 检查客户端文件有效性
            var validationResult = await IsClientFileValidAsync();
            if (validationResult == 0) // 用户取消启动
            {
                IsLoading = false;
                return;
            }

            var frpt = await MEFrpApiConverter.GetFrpTokenAsync();

            if (ConfigManager.CurrentConfig.PMSettings.Enabled)
            {
                ProxyFloat.Instance ??= new ProxyFloat();
                ProxyFloat.Instance?.Show();
            }

            var cmd = $"{{mefrpc}} -t {frpt.data?.token} -p {proxy.proxyId}";
            MainPageFrameViewModel.TerminalPage ??= new TerminalPage();
            // 26.3 M3: 订阅终端输出，滚动保留最近 20 行并做错误特征检测
            MainPageFrameViewModel.TerminalPage.CreateNewTerminalWithoutNotification(cmd, proxy.proxyName,
                OnTerminalOutputAsync);
            // 26.3 M6b-Extended：隧道启动 → 悬浮窗状态同步（TerminalPage 内也会报告，此处保持既有语义）
            ProxyFloatViewModel.ReportTunnelStarted(proxy.proxyName);
            IsLoading = false;
            MainPageFrameViewModel.Instance.NavigateToPage("Terminal");
            MainPageFrameViewModel.Instance.CurrentPage = MainPageFrameViewModel.TerminalPage;
            Growl.Success(string.Format(Languages.Text_UserProxy_LaunchSucceededFormat, proxy.proxyName));
            IsLaunched = true;
            TunnelStatus = TunnelStatus.Running;
            ScheduleStartupTimeoutCheck();
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger.Error(ex);
            LastErrorSummary = TunnelErrorMapper.Map(apiError: ex.Message).Summary;
            TunnelStatus = TunnelStatus.Failed;
            IsLoading = false;
            Growl.Error(string.Format(Languages.Text_UserProxy_LaunchFailedFormat, proxy?.proxyName, LastErrorSummary));
        }
    }

// 类似地修改其他命令方法(DisableProxy, EnableProxy, DeleteProxy等)
    private async void DeleteProxy(object parameter)
    {
        switch (parameter)
        {
            case UserProxyViewModel proxy:
                DeleteSingleProxy(proxy);
                break;
            case IList<UserProxyViewModel> proxies when await MessageBox.ShowAsync(
                                                            string.Format(
                                                                Languages.Text_UserProxy_ConfirmDeleteMultipleFormat,
                                                                proxies.Count),
                                                            Languages.Text_UserProxy_ConfirmDeleteTitle,
                                                            [
                                                                TaskDialogButton.YesButton,
                                                                TaskDialogButton.NoButton
                                                            ]) !=
                                                        MessageBoxResult.Yes:
                return;
            case IList<UserProxyViewModel> proxies:
            {
                foreach (var p in proxies)
                {
                    DeleteSingleProxy(p);
                }

                break;
            }
        }
    }

    private async void DeleteSingleProxy(UserProxyViewModel proxy)
    {
        Core.App.CurrentLogger.Log($"正在删除隧道 {proxy.proxyName}", port: EnumLogPort.Client, module: EnumLogModule.Main);
        IsLoading = true;
        await Task.Run(() => MEFrpApiConverter.DeleteProxy(proxy.proxyId));
        Growl.Success(string.Format(Languages.Text_UserProxy_DeleteSucceededFormat, proxy.proxyName));
        ManageProxyPage.Instance.LoadProxies();
        IsLoading = false;
    }

    private async void EditProxy(UserProxyViewModel proxy)
    {
        Core.App.CurrentLogger.Log($"正在编辑隧道 {proxy.proxyName}", port: EnumLogPort.Client, module: EnumLogModule.Main);
        // 编辑隧道逻辑
        await new EditProxyWindow(proxy).ShowDialog(Core.App.MainWindow);
        ManageProxyPage.Instance.LoadProxies();
    }

    private async void ForceOfflineProxy(UserProxyViewModel proxy)
    {
        IsLoading = true;
        Core.App.CurrentLogger.Log($"正在强制下线隧道 {proxy.proxyName}", port: EnumLogPort.Client, module: EnumLogModule.Main);
        // 强制下线隧道逻辑
        if (OperatingSystem.IsWindows())
        {
            using var killProcess = new Process();
            killProcess.StartInfo.FileName = "taskkill";
            killProcess.StartInfo.Arguments = "/im mefrpc.exe /T /F";
            killProcess.StartInfo.UseShellExecute = false;
            killProcess.StartInfo.CreateNoWindow = true;
            killProcess.Start();
        }
        else if (OperatingSystem.IsLinux())
        {
            Process.Start("pkill", "mefrpc")?.WaitForExit(1000);
        }

        await Task.Run(() =>
        {
            MEFrpApiConverter.KickProxy(proxy.proxyId);
            if (ConfigManager.CurrentConfig.KickWithoutDisable)
            {
                MEFrpApiConverter.ToggleProxyStatus(proxy.proxyId, false);
            }

            ManageProxyPage.Instance.LoadProxies();
        });
        IsLoading = false;
    }

    private async void DisableProxy(UserProxyViewModel proxy)
    {
        IsLoading = true;
        Core.App.CurrentLogger.Log($"正在禁用隧道 {proxy.proxyName}", port: EnumLogPort.Client, module: EnumLogModule.Main);
        // 禁用隧道逻辑
        await Task.Run(() =>
            MEFrpApiConverter.ToggleProxyStatus(proxy.proxyId, true));
        ManageProxyPage.Instance.LoadProxies();
        IsLoading = false;
    }

    private async void EnableProxy(UserProxyViewModel proxy)
    {
        IsLoading = true;
        Core.App.CurrentLogger.Log($"正在启用隧道 {proxy.proxyName}", port: EnumLogPort.Client, module: EnumLogModule.Main);
        // 启用隧道逻辑
        await Task.Run(() =>
            MEFrpApiConverter.ToggleProxyStatus(proxy.proxyId, false));
        ManageProxyPage.Instance.LoadProxies();
        IsLoading = false;
    }

    private async void GenerateLaunchConfig(object obj)
    {
        if (obj is not IList<object?> list)
        {
            return;
        }

        IsLoading = true;
        var proxy = list[0] as UserProxyViewModel;
        var s = list[1] as ComboBox;
        var type = s.SelectionBoxItem.ToString().ToLower();
        Core.App.CurrentLogger.Log($"正在生成隧道 {proxy.proxyName} 的{type}启动配置", port: EnumLogPort.Client,
            module: EnumLogModule.Main);
        var res = await Task.Run(() => MEFrpApiConverter.GetLaunchConfig(proxy.proxyId, type));
        var cfg = res.data.config;
        Growl.Success(string.Format(Languages.Text_UserProxy_ConfigGeneratedFormat, proxy.proxyName));
        IsLoading = false;
        await new ConfigPreviewer(type, cfg, proxy.proxyName).ShowDialog(Core.App.MainWindow);
    }

    private async void ShowExtraInfo(UserProxyViewModel proxy)
    {
        var td = new TaskDialog
        {
            // Title property only applies on Windowed dialogs
            Title = Languages.Text_UserProxy_TunnelDetails,
            Header = $"{proxy.proxyName}",
            SubHeader = string.Format(Languages.Text_UserProxy_CreatedFromNodeFormat, proxy.node),
            Content = new SelectableTextBlock
            {
                Text =
                    $"""
                     {Languages.Text_UserProxy_DetailProtocolType}: {proxy.proxyType}
                     {Languages.Text_UserProxy_DetailLocalPort}: {proxy.localPort}
                     {Languages.Text_UserProxy_DetailLocalAddress}: {proxy.localIp}
                     {Languages.Text_UserProxy_DetailTransportProtocol}: {proxy.transportProtocol.ToUpper()}{(proxy.proxyType.ToLower() is "tcp" or "udp" ? "\n" + Languages.Text_UserProxy_DetailLinkAddress + ": " + proxy.location : $"\n{Languages.Text_UserProxy_DetailHttpMappingType}: {proxy.httpPlugin.ToUpper()}\n{Languages.Text_UserProxy_DetailSecurityOption}: {GetSecurityOption()}")}
                     {Languages.Text_UserProxy_DetailLastStartTime}: {new UnixTimeToDateTimeConverter()
                         .Convert(proxy.lastStartTime, null, null, null)}
                     {Languages.Text_UserProxy_DetailLastCloseTime}: {new UnixTimeToDateTimeConverter()
                         .Convert(proxy.lastCloseTime, null, null, null)}{(proxy.proxyType.ToUpper() is "HTTP" or "HTTPS" ? "\n" + Languages.Text_UserProxy_DetailMoreInfoWebsite : "")}
                     """
            },
            Buttons =
            {
                TaskDialogButton.OKButton
            },
            XamlRoot = UserProxyControl.Instance
        };

        await td.ShowAsync(true);
        return;

        string GetSecurityOption()
        {
            if (proxy.httpUser.IsNullOrEmpty() && proxy.httpPassword.IsNullOrEmpty() && proxy.accessKey.IsNullOrEmpty())
            {
                return Languages.Text_Global_Disable;
            }

            if (proxy.httpUser.IsNullOrEmpty() && proxy.httpPassword.IsNullOrEmpty())
            {
                return Languages.Text_CreateProxy_AccessKey;
            }

            return "HTTP Basic Auth";
        }
    }

    private async void EditSSL(UserProxyViewModel obj)
    {
        var pss = new ProxySSLSettings(obj);
        var cd = new ContentDialog
        {
            Title = Languages.Text_UserProxy_SslCertConfig,
            Content = pss,
            PrimaryButtonText = Languages.Text_Global_Confirm,
            PrimaryButtonCommand = new RelayCommand(_obj =>
            {
            }),
            CloseButtonText = Languages.Text_Global_Cancel
        };
        if (await cd.ShowAsync() != ContentDialogResult.Primary || !pss.Finished)
        {
            return;
        }

        if (!pss.Check())
        {
            return;
        }

        var sSlConfig = pss.GetSSlConfig();
        var cfgService = new FrpConfigService();
        var config = cfgService.LoadConfig(pss.Config);
        cfgService.AddHttpsProxy(config, sSlConfig.GetValueOrDefault("name", ""),
            sSlConfig.GetValueOrDefault("domain", ""),
            sSlConfig.GetValueOrDefault("localIp", ""),
            sSlConfig.GetValueOrDefault("cert", ""),
            sSlConfig.GetValueOrDefault("key", ""));
        var content = cfgService.SaveConfig(config, Path.GetExtension(pss.Config).Replace(".", ""));
        await File.WriteAllTextAsync(pss.Config, content);
        LaunchViaConfigImpl(obj, pss.Config);
    }

    private async void CopyInfo(UserProxyViewModel obj)
    {
        var clipboard = Core.App.MainWindow.Clipboard;
        //var nameList = await MEFrpApiConverter.GetNodesNameListAsync();
        await clipboard.SetTextAsync(obj.proxyType.Equals("http", StringComparison.OrdinalIgnoreCase) ||
                                     obj.proxyType.Equals("https", StringComparison.OrdinalIgnoreCase)
            ? obj.domain
            : Node?.hostname +
              $":{obj.remotePort}");
        Growl.Success(Languages.Text_UserProxy_TunnelInfoCopied);
    }

    private async void StopProxy(UserProxyViewModel obj)
    {
        await TerminalPage.Instance.SendCtrlCCommandToSelected(obj.proxyName);
        // 26.3 M6b：停止 → 悬浮窗移除对应隧道项（与关闭标签页一致；TerminalPage 内也会报告，此处保证链路完整）
        ProxyFloatViewModel.ReportTunnelRemoved(obj.proxyName);
        ForceOfflineProxy(obj);
        IsLaunched = false;
        TunnelStatus = TunnelStatus.Stopped;
    }

    /// <summary>
    ///     探测本隧道所在节点的连通性与延迟。
    ///     探测目标：<see cref="Node" />.hostname + <c>remotePort</c>（与 location 展示一致）；
    ///     hostname 为空或 remotePort 非法时直接置 NotProbeable，不发网络请求。
    /// </summary>
    public async Task ProbeAsync(CancellationToken ct = default)
    {
        var hostname = Node?.hostname;
        if (string.IsNullOrWhiteSpace(hostname) || remotePort <= 0)
        {
            LatencyMs = null;
            ProbeStatus = ProbeStatus.NotProbeable;
            MeasuredAt = DateTimeOffset.Now;
            return;
        }

        IsProbing = true;
        try
        {
            var result = await Core.App.NodeProbeService.ProbeAsync(hostname, remotePort, ct);
            LatencyMs = result.LatencyMs;
            ProbeStatus = result.Status;
            MeasuredAt = result.MeasuredAt;
        }
        finally
        {
            IsProbing = false;
        }
    }

    /// <summary>
    ///     根据 IsLoading / IsLaunched / isOnline 推导运行状态；
    ///     Failed / Stopped 为显式终态，不在此覆盖。
    /// </summary>
    private void RefreshTunnelStatus()
    {
        if (TunnelStatus is TunnelStatus.Failed or TunnelStatus.Stopped)
        {
            return;
        }

        if (IsLoading && !IsLaunched)
        {
            TunnelStatus = TunnelStatus.Starting;
        }
        else if (IsLaunched && isOnline)
        {
            TunnelStatus = TunnelStatus.Running;
        }
        else if (IsLaunched && !isOnline)
        {
            TunnelStatus = TunnelStatus.Reconnecting;
        }
        else
        {
            TunnelStatus = TunnelStatus.Idle;
        }
    }

    /// <summary>终端输出回调（PTY 读取线程触发，回 UI 线程更新状态与错误检测）</summary>
    private void OnTerminalOutputAsync(string output)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            // 已停止 / 已失败：不再更新状态
            if (TunnelStatus is TunnelStatus.Stopped or TunnelStatus.Failed)
            {
                return;
            }

            // 有输出说明进程存活，取消启动超时检查
            CancelStartupTimeout();

            // 滚动保留最近 20 行
            var merged = LastOutputBuffer + output;
            var lines = merged.Replace("\r", string.Empty)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries);
            LastOutputBuffer = string.Join('\n', lines.TakeLast(20));

            // 错误特征检测（认证失败 / 端口占用 / 节点不可达 / 进程崩溃）
            var info = TunnelErrorMapper.Map(LastOutputBuffer);
            if (info.Category != TunnelErrorCategory.Unknown)
            {
                LastErrorSummary = info.Summary;
                TunnelStatus = TunnelStatus.Failed;
                IsLoading = false;
                var request = NotificationBuilder
                    .Create(string.Format(Languages.Text_ProxyStart_StartFailed, proxyName))
                    .WithBody(LastErrorSummary)
                    .AddButton(Languages.Text_UserProxy_CopyErrorInfo, _ =>
                    {
                        CopyError(this);
                    })
                    .AddButton(Languages.Text_Global_Dismiss)
                    .WithUrgency(NotificationUrgency.Critical)
                    .WithExpiration(TimeSpan.FromSeconds(2))
                    .OnActivated(id => Program.ActivateExistingInstance())
                    .Build();
                if (Core.App.NotificationService.IsSupported)
                {
                    await Core.App.NotificationService.ShowAsync(request);
                }
            }
        });
    }

    /// <summary>启动后超时检测：30 秒内无输出且未确认在线 → 节点不可达</summary>
    private void ScheduleStartupTimeoutCheck()
    {
        CancelStartupTimeout();
        _startupTimeoutCts = new CancellationTokenSource();
        var ct = _startupTimeoutCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (TunnelStatus is TunnelStatus.Starting or TunnelStatus.Reconnecting)
                    {
                        LastErrorSummary = TunnelErrorMapper.MapTimeout().Summary;
                        TunnelStatus = TunnelStatus.Failed;
                        IsLoading = false;
                    }
                });
            }
            catch (OperationCanceledException)
            {
                // 正常取消
            }
        }, ct);
    }

    private void CancelStartupTimeout()
    {
        try
        {
            _startupTimeoutCts?.Cancel();
            _startupTimeoutCts?.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // 并发取消时忽略
        }

        _startupTimeoutCts = null;
    }

    /// <summary>复制错误信息（应用版本 + mefrpc 版本 + 失败摘要），便于反馈排查</summary>
    private async void CopyError(UserProxyViewModel obj)
    {
        var clipboard = Core.App.MainWindow.Clipboard;
        await clipboard.SetTextAsync(
            $"PML2 {Core.App.Version} / mefrpc {Core.App.MEFrpVersion}\n{obj.LastErrorSummary}");
        Growl.Success(Languages.Text_UserProxy_ErrorCopied);
    }
}