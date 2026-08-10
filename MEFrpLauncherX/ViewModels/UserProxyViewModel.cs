using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using FluentAvalonia.UI.Controls;
using MEFrpLauncherX.Controls;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Core.MEFIntegrated;
using MEFrpLauncherX.Views;
using MEFrpLauncherX.Views.ProxyMonitor;
using MsBox.Avalonia.ViewModels.Commands;
using ReactiveUI;
using ProxyFloat = MEFrpLauncherX.Views.ProxyMonitor.ProxyFloat;

namespace MEFrpLauncherX.ViewModels;

public class UserProxyViewModel : ViewModelBase
{
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
    }

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
                Growl.Error(string.Format(Languages.Text_UserProxy_LaunchCancelledErrorFormat, proxy?.proxyName, ex.Message));
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
        ProxyFloatViewModel.Instance?.Proxies.Add(proxy.proxyName);
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
                "aef147c9899db111714f60396e4b28a5|8255cc73f6ddf23be05de69e75f80aee"    //v0.67.1_20260626_af59eefd
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
        MainPageFrameViewModel.TerminalPage.CreateNewTerminalWithoutNotification(cmd, proxy.proxyName);
        ProxyFloatViewModel.Instance?.Proxies.Add(proxy.proxyName);
        IsLoading = false;
        MainPageFrameViewModel.Instance.NavigateToPage("Terminal");
        MainPageFrameViewModel.Instance.CurrentPage = MainPageFrameViewModel.TerminalPage;
        Growl.Success(string.Format(Languages.Text_UserProxy_LaunchSucceededFormat, proxy.proxyName));
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
            case IList<UserProxyViewModel> proxies when await MessageBox.ShowAsync(string.Format(Languages.Text_UserProxy_ConfirmDeleteMultipleFormat, proxies.Count),
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

    private void StopProxy(UserProxyViewModel obj)
    {
        TerminalPage.Instance.SendCtrlCCommandToSelected(obj.proxyName);
        ForceOfflineProxy(obj);
        IsLaunched = false;
    }
}