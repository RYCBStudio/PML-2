using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using FluentAvalonia.UI.Controls;
using MEFrpLauncherX.Controls;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.MEFIntergrated;
using MEFrpLauncherX.Views;
using MsBox.Avalonia.ViewModels.Commands;
using Newtonsoft.Json;
using ReactiveUI;

namespace MEFrpLauncherX.ViewModels;

public class UserProxyViewModel : ViewModelBase
{
    public List<string> Locations
    {
        get;
        set;
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
        set;
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
        set;
    }

    public bool isOnline
    {
        get;
        set;
    }

    public string domain
    {
        get;
        set;
    }

    public List<string> Domains
    {
        get;
        private set;
    }

    public Dictionary<string, string> RequestHeaders
    {
        get;
        set;
    }

    public Dictionary<string, string> ResponseHeaders
    {
        get;
        set;
    }

    public int lastStartTime
    {
        get;
        set;
    }

    public int lastCloseTime
    {
        get;
        set;
    }

    public string clientVersion
    {
        get;
        set;
    }

    public string proxyProtocolVersion
    {
        get;
        set;
    }

    public bool useEncryption
    {
        get;
        set;
    }

    public bool useCompression
    {
        get;
        set;
    }

    public string location
    {
        get;
        set;
    }

    public string accessKey
    {
        get;
        set;
    }

    public string hostHeaderRewrite
    {
        get;
        set;
    }

    public string headerXFromWhere
    {
        get;
        set;
    }

    public string transportProtocol
    {
        get;
        set;
    }
    
    public string httpUser
    {
        get;
        set;
    }

    public string httpPassword
    {
        get;
        set;
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

    private async void LaunchProxyViaConfig(object parameter)
    {
        Core.App.CurrentLogger.Log("使用配置文件启动单个隧道操作", port: EnumLogPort.Client, module: EnumLogModule.Main);
        var proxy = parameter as UserProxyViewModel;
        List<string> configFiles = [];
        configFiles.AddRange(Directory
            .EnumerateFileSystemEntries(Path.Combine(Core.App.StartupPath, "Config", "frp"), "*.*",
                SearchOption.TopDirectoryOnly)
            .Where(fs => fs.EndsWithEx(".ini,.json,.toml,.yaml,.yml") && Path.GetFileNameWithoutExtension(fs)
                .Contains(proxy.proxyName, StringComparison.OrdinalIgnoreCase)));

        var configFile = string.Empty;
        var cs = new ConfigSelect(configFiles);
        var cd = new ContentDialog()
        {
            Title = "请选择配置文件",
            Content = cs,
            PrimaryButtonText = "确定",
            CloseButtonText = "取消"
        };
        ShowExtraMenu = false;
        IReadOnlyList<IStorageFile>? cfg = [];
        if (await cd.ShowAsync(Core.App.MainWindow) == ContentDialogResult.Primary)
        {
            configFile = cs.SelectedPath;
        }
        else
        {
            cfg = await Core.App.MainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "请选择配置文件",
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
                Growl.Warning($"取消启动隧道: {proxy?.proxyName}");
                return;
            }
            catch (Exception ex)
            {
                Core.App.CurrentLogger.Log($"使用配置文件启动单个隧道操作失败: {ex.Message}", port: EnumLogPort.Client,
                    module: EnumLogModule.Main);
                Core.App.CurrentLogger.Error(ex);
                Growl.Error($"由于内部错误, 取消启动隧道: {proxy?.proxyName}\n详细信息: {ex.Message}");
                return;
            }
        }

        if (configFile.IsNullOrEmpty())
        {
            Growl.Warning($"取消启动隧道: {proxy?.proxyName}");
            return;
        }

        var _res = await new ConfigEditor(configFile).ShowDialog<bool>(Core.App.MainWindow);
        if (!_res)
        {
            Growl.Error($"取消启动隧道: {proxy?.proxyName}");
            return;
        }

        LaunchViaConfigImpl(proxy, configFile);
    }

    private async void LaunchViaConfigImpl(UserProxyViewModel proxy, string configFile)
    {
        Core.App.CurrentLogger.Log($"正在使用指定配置启动隧道 {proxy?.proxyName}", port: EnumLogPort.Client,
            module: EnumLogModule.Main);
        if (OperatingSystem.IsWindows())
        {
            if (!DownloadHelper.ValidateFileSimple(Path.Combine(Core.App.StartupPath, "bin", "mefrpc.exe"),
                    "1cc4bb63ff49a578938862a8e1541ec2|7877aebbb5d28b075fe6ff5f823863ce"))
            {
                var res = await MessageBox.ShowAsync("mefrpc.exe 文件校验失败，需要重新下载客户端。关闭此窗口以取消启动; 点击“否”尝试直接启动。" +
                                                     "\n请注意: 我们不对任何非官方(与我们提供的文件校验值不同)的文件运行所造成的任何后果负责。", "警告", "",
                    MessageBoxIcon.Warning, buttons:
                    [
                        new TaskDialogButton("下载", TaskDialogStandardResult.Yes),
                        new TaskDialogButton("否", TaskDialogStandardResult.No)
                    ]);
                switch (res)
                {
                    case MessageBoxResult.No:
                        break;
                    case MessageBoxResult.Yes:
                        _ = await new DownloadHelper(Core.App.MainWindow).DownloadMEFrpClient(Environment.OSVersion);
                        break;
                    default:
                        return;
                }
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            if (!DownloadHelper.ValidateFileSimple(Path.Combine(Core.App.StartupPath, "bin", "mefrpc.tar"),
                    "e402ab9d90ce932339d920a398480ab9"))
            {
                var res = await MessageBox.ShowAsync("mefrpc 文件校验失败，需要重新下载客户端。关闭此窗口以取消启动; 点击“否”尝试直接启动。" +
                                                     "\n请注意: 我们不对任何非官方(与我们提供的文件校验值不同)的文件运行所造成的任何后果负责。", "警告", "",
                    MessageBoxIcon.Warning, buttons:
                    [
                        new TaskDialogButton("下载", TaskDialogStandardResult.Yes),
                        new TaskDialogButton("否", TaskDialogStandardResult.No)
                    ]);
                switch (res)
                {
                    case MessageBoxResult.No:
                        break;
                    case MessageBoxResult.Yes:
                        _ = await new DownloadHelper(Core.App.MainWindow).DownloadMEFrpClient(Environment.OSVersion);
                        break;
                    default:
                        return;
                }
            }
        }

        var cmd = $"{{mefrpc}} -c {configFile}";
        MainPageFrameViewModel.TerminalPage ??= new TerminalPage();
        MainPageFrameViewModel.TerminalPage.CreateNewTerminalWithoutNotification(cmd, proxy.proxyName);
        ProxyFloatViewModel.Instance?.Proxies.Add(proxy.proxyName);
        IsLoading = false;
        MainPageFrameViewModel.Instance.NavigateToPage("Terminal");
        MainPageFrameViewModel.Instance.CurrentPage = MainPageFrameViewModel.TerminalPage;
        if (ConfigManager.CurrentConfig.PMSettings.Enabled)
        {
            ProxyFloat.Instance.Show();
        }

        Growl.Success($"启动隧道: {proxy?.proxyName} 成功");
        IsLaunched = true;
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
        var frpt = await Task.Run(() => MEFApiConverter.GetFrpToken().data);
        if (OperatingSystem.IsWindows())
        {
            if (!DownloadHelper.ValidateFileSimple(Path.Combine(Core.App.StartupPath, "bin", "mefrpc.exe"),
                    "3b667ad96332c3ded5f53fd0f3a35d07|7877aebbb5d28b075fe6ff5f823863ce"))
            {
                var res = await MessageBox.ShowAsync("mefrpc.exe 文件校验失败，需要重新下载客户端。关闭此窗口以取消启动; 点击“否”尝试直接启动。" +
                                                     "\n请注意: 我们不对任何非官方(与我们提供的文件校验值不同)的文件运行所造成的任何后果负责。", "警告", "",
                    MessageBoxIcon.Warning, buttons:
                    [
                        new TaskDialogButton("下载", TaskDialogStandardResult.Yes),
                        new TaskDialogButton("否", TaskDialogStandardResult.No)
                    ]);
                switch (res)
                {
                    case MessageBoxResult.No:
                        break;
                    case MessageBoxResult.Yes:
                        _ = await new DownloadHelper(Core.App.MainWindow).DownloadMEFrpClient(Environment.OSVersion);
                        break;
                    default:
                        return;
                }
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            if (!DownloadHelper.ValidateFileSimple(Path.Combine(Core.App.StartupPath, "bin", "mefrpc.tar"),
                    "e402ab9d90ce932339d920a398480ab9|ad07416756ca770ca1bb85463d782737"))
            {
                var res = await MessageBox.ShowAsync("mefrpc.tar 文件校验失败，需要重新下载客户端。关闭此窗口以取消启动; 点击“否”尝试直接启动。" +
                                                     "\n请注意: 我们不对任何非官方(与我们提供的文件校验值不同)的文件运行所造成的任何后果负责。", "警告", "",
                    MessageBoxIcon.Warning, buttons:
                    [
                        new TaskDialogButton("下载", TaskDialogStandardResult.Yes),
                        new TaskDialogButton("否", TaskDialogStandardResult.No)
                    ]);
                switch (res)
                {
                    case MessageBoxResult.No:
                        break;
                    case MessageBoxResult.Yes:
                        _ = await new DownloadHelper(Core.App.MainWindow).DownloadMEFrpClient(Environment.OSVersion);
                        break;
                    default:
                        return;
                }
            }
        }

        if (ConfigManager.CurrentConfig.PMSettings.Enabled)
        {
            ProxyFloat.Instance ??= new ProxyFloat();
            ProxyFloat.Instance?.Show();
        }

        var cmd = $"{{mefrpc}} -t {frpt.token} -p {proxy.proxyId}";
        MainPageFrameViewModel.TerminalPage ??= new TerminalPage();
        MainPageFrameViewModel.TerminalPage.CreateNewTerminalWithoutNotification(cmd, proxy.proxyName);
        ProxyFloatViewModel.Instance?.Proxies.Add(proxy.proxyName);
        IsLoading = false;
        MainPageFrameViewModel.Instance.NavigateToPage("Terminal");
        MainPageFrameViewModel.Instance.CurrentPage = MainPageFrameViewModel.TerminalPage;
        Growl.Success($"启动隧道: {proxy.proxyName} 成功");
        IsLaunched = true;
    }

// 类似地修改其他命令方法(DisableProxy, EnableProxy, DeleteProxy等)
    private async void DeleteProxy(object parameter)
    {
        switch (parameter)
        {
            case UserProxyViewModel proxy:
                DeleteSingleProxy(proxy);
                break;
            case IList<UserProxyViewModel> proxies when await MessageBox.ShowAsync($"确定要删除这 {proxies.Count} 个隧道吗？",
                                                            "确认删除",
                                                            [TaskDialogButton.YesButton, TaskDialogButton.NoButton]) !=
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
        await Task.Run(() => MEFApiConverter.DeleteProxy(proxy.proxyId));
        Growl.Success($"删除隧道: {proxy.proxyName} 成功");
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
            MEFApiConverter.KickProxy(proxy.proxyId);
            if (ConfigManager.CurrentConfig.KickWithoutDisable)
            {
                MEFApiConverter.ToggleProxyStatus(proxy.proxyId, false);
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
            MEFApiConverter.ToggleProxyStatus(proxy.proxyId, true));
        ManageProxyPage.Instance.LoadProxies();
        IsLoading = false;
    }

    private async void EnableProxy(UserProxyViewModel proxy)
    {
        IsLoading = true;
        Core.App.CurrentLogger.Log($"正在启用隧道 {proxy.proxyName}", port: EnumLogPort.Client, module: EnumLogModule.Main);
        // 启用隧道逻辑
        await Task.Run(() =>
            MEFApiConverter.ToggleProxyStatus(proxy.proxyId, false));
        ManageProxyPage.Instance.LoadProxies();
        IsLoading = false;
    }

    private async void GenerateLaunchConfig(object obj)
    {
        if (obj is not IList<object?>)
        {
            return;
        }

        IsLoading = true;
        var _obj = obj as IList<object?>;
        var proxy = _obj[0] as UserProxyViewModel;
        var s = _obj[1] as ComboBox;
        var type = s.SelectionBoxItem.ToString().ToLower();
        Core.App.CurrentLogger.Log($"正在生成隧道 {proxy.proxyName} 的{type}启动配置", port: EnumLogPort.Client,
            module: EnumLogModule.Main);
        var res = await Task.Run(() => MEFApiConverter.GetLaunchConfig(proxy.proxyId, type));
        var cfg = res.data.config;
        Growl.Success($"已为隧道 {proxy.proxyName} 生成启动配置");
        IsLoading = false;
        await new ConfigPreviewer(type, cfg, proxy.proxyName).ShowDialog(Core.App.MainWindow);
    }

    private async void ShowExtraInfo(UserProxyViewModel proxy)
    {
        var td = new TaskDialog
        {
            // Title property only applies on Windowed dialogs
            Title = "隧道详情",
            Header = $"{proxy.proxyName}",
            SubHeader = $"创建自节点: {proxy.node}",
            Content = new SelectableTextBlock
            {
                Text =
                    $"""
                     协议类型: {proxy.proxyType}
                     本地端口: {proxy.localPort}
                     本地地址: {proxy.localIp}
                     链接地址: {proxy.location}
                     上次启动时间: {new UnixTimeToDateTimeConverter()
                         .Convert(proxy.lastStartTime, null, null, null)}
                     上次关闭时间: {new UnixTimeToDateTimeConverter()
                         .Convert(proxy.lastCloseTime, null, null, null)}{(proxy.proxyType.ToUpper() is "HTTP" or "HTTPS" ? "\n* 更多信息(域名解析等)请前往官网查看。" : "")}
                     """
            },
            Buttons =
            {
                TaskDialogButton.OKButton
            },
            XamlRoot = UserProxyControl.Instance
        };

        await td.ShowAsync(true);
    }

    public UserProxyViewModel()
    {
        try
        {
            Domains = JsonConvert.DeserializeObject<List<string>>(domain).Distinct().ToList();
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
    }

    public UserProxyViewModel(string _domain)
    {
        try
        {
            Domains = JsonConvert.DeserializeObject<List<string>>(_domain).Distinct().ToList();
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
    }

    private async void EditSSL(UserProxyViewModel obj)
    {
        var pss = new ProxySSLSettings(obj);
        var cd = new ContentDialog
        {
            Title = "SSL证书配置",
            Content = pss,
            PrimaryButtonText = "确定",
            PrimaryButtonCommand = new RelayCommand((_obj) =>
            {
                return;
            }),
            CloseButtonText = "取消"
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
        //var nameList = await MEFApiConverter.GetNodesNameListAsync();
        await clipboard.SetTextAsync(obj.proxyType.Equals("http", StringComparison.OrdinalIgnoreCase) ||
                                     obj.proxyType.Equals("https", StringComparison.OrdinalIgnoreCase)
            ? obj.domain
            : Node?.hostname +
              $":{obj.remotePort}");
        Growl.Success("已复制隧道信息");
    }

    private void StopProxy(UserProxyViewModel obj)
    {
        TerminalPage.Instance.SendCtrlCCommandToSelected(obj.proxyName);
        ForceOfflineProxy(obj);
        IsLaunched = false;
    }

    public bool IsLaunched
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsLoading
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool Detailed
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

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

    public InfoClasses.Nodes? Node
    {
        get;
        set;
    }

    public bool ShowExtraMenu
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string httpPlugin
    {
        get;
        set;
    }
}