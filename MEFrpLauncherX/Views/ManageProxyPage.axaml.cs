using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using MEFrpLauncherX.Controls;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Core.MEFIntegrated;
using MEFrpLauncherX.ViewModels;
using ReactiveUI;
using MessageBox = MEFrpLauncherX.Core.Controls.MessageBox;

namespace MEFrpLauncherX.Views;

public partial class ManageProxyPage : UserControl
{
    private readonly ManageProxyViewModel _manageProxyViewModel;
    private bool _isLoadingProxies;

    public ManageProxyPage()
    {
        InitializeComponent();
        _manageProxyViewModel = new ManageProxyViewModel();
        DataContext = _manageProxyViewModel;
        AttachedToVisualTree += ManageProxyPage_Loaded;
        SearchBox.ItemsSource = new List<string> { "/pid:", "/nid:", "/n:" }.OrderBy(x => x);
    }

    public static ManageProxyPage Instance
    {
        get;
        private set;
    }

    private async void ManageProxyPage_Loaded(object? sender, VisualTreeAttachmentEventArgs e)
    {
        try
        {
            await LoadProxies();
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger.Error(ex, "LoadProxies failed");
        }

        Instance = this;
    }

    public async Task LoadProxies()
    {
        if (_isLoadingProxies)
        {
            Core.App.CurrentLogger.LogDebug("LoadProxies already in progress, skipping duplicate call");
            return;
        }

        _isLoadingProxies = true;
        await Dispatcher.UIThread.InvokeAsync(() =>
            MainPageFrameViewModel.Instance?.IsLoading = true);
        try
        {
            Core.App.CurrentLogger.LogDebug("Starting LoadProxies");
            // Avalonia animations work differently - you might need to implement fade effects differently
            _manageProxyViewModel.AllProxies.Clear();
            _manageProxyViewModel.FilteredProxies.Clear();
            await Task.Run(async () =>
            {
                var userProxies =
                    (await MEFrpApiConverter.GetProxiesAsync()).data ?? new InfoClasses.ProxyInfo();
                // var currentNodesListInfo = MEFrpApiConverter.CurrentNodesListInfo;
                // InfoClasses.NodesList[] currentNodesList;
                //
                // if (currentNodesListInfo?.NodesList is null)
                // {
                //     currentNodesList = (await MEFrpApiConverter.GetNodesInfoAsync()).data;
                // }
                // else
                // {
                //     currentNodesList = currentNodesListInfo.NodesList;
                // }

#if DEBUG
                var db_nodes = new List<InfoClasses.Nodes>
                {
                    new()
                    {
                        nodeId = 0,
                        name = "DEBUG",
                        hostname = "114.514.191.810"
                    }
                };
                var db_proxy = new List<InfoClasses.Proxies>
                {
                    new()
                    {
                        proxyId = -114,
                        username = "111",
                        proxyName = "DEBUG_Proxy1",
                        proxyType = "tcp",
                        isBanned = false,
                        isDisabled = false,
                        localIp = "127.0.0.1",
                        localPort = 1145,
                        remotePort = 11451,
                        nodeId = 0,
                        runId = "",
                        isOnline = false,
                        domain = "",
                        lastStartTime = 0,
                        lastCloseTime = 0,
                        clientVersion = "",
                        proxyProtocolVersion = "",
                        useEncryption = false,
                        useCompression = false,
                        locations = "",
                        accessKey = "",
                        hostHeaderRewrite = "",
                        httpPlugin = "",
                        crtPath = "",
                        keyPath = "",
                        requestHeaders = "",
                        responseHeaders = "",
                        httpUser = "",
                        httpPassword = "",
                        transportProtocol = ""
                    }
                };
                userProxies ??= new InfoClasses.ProxyInfo
                {
                    nodes = db_nodes.ToArray(),
                    proxies = db_proxy.ToArray()
                };
#endif

                Core.App.CurrentLogger.LogDebug("Loading user proxies");
                foreach (var item in userProxies.proxies)
                {
                    var node = userProxies.nodes.FirstOrDefault(n => n.nodeId == item.nodeId);

                    _manageProxyViewModel.AllProxies.Add(new UserProxyViewModel(item.domain)
                    {
                        // Same property assignments as original
                        username = item.username,
                        proxyName = item.proxyName,
                        proxyId = item.proxyId,
                        proxyType = item.proxyType.ToUpper(),
                        node = node?.name ?? Languages.Text_Nodes_NodeNotFound,
                        isBanned = item.isBanned,
                        isOnline = item.isOnline,
                        isDisabled = item.isDisabled,
                        localIp = item.localIp,
                        localPort = item.localPort,
                        remotePort = item.remotePort,
                        runId = item.runId,
                        nodeId = item.nodeId,
                        //allowedProtocols = node?.allowType?.Split(';')
                        //  ?.Select(type => type.ToUpper())
                        //  ?.ToArray() ?? [],
                        domain = item.domain,
                        lastStartTime = item.lastStartTime,
                        lastCloseTime = item.lastCloseTime,
                        proxyProtocolVersion = item.proxyProtocolVersion,
                        clientVersion = item.clientVersion,
                        useEncryption = item.useEncryption,
                        useCompression = item.useCompression,
                        location = $"{node?.hostname}:{item.remotePort}",
                        accessKey = item.accessKey,
                        hostHeaderRewrite = item.hostHeaderRewrite,
                        Node = node,
                        transportProtocol = item.transportProtocol,
                        httpPlugin = item.httpPlugin,
                        httpUser = item.httpUser,
                        httpPassword = item.httpPassword,
                        RequestHeaders = item.requestHeaders.IsNullOrEmpty()
                            ? null
                            : JsonSerializer.Deserialize<Dictionary<string, string>>(item.requestHeaders,
                                App.AppJsonSerializerContext.DictionaryStringString),
                        ResponseHeaders = item.responseHeaders.IsNullOrEmpty()
                            ? null
                            : JsonSerializer.Deserialize<Dictionary<string, string>>(item.responseHeaders,
                                App.AppJsonSerializerContext.DictionaryStringString),
                        Locations = item.locations.IsNullOrEmpty()
                            ? null
                            : JsonSerializer.Deserialize<List<string>>(item.locations,
                                App.AppJsonSerializerContext.ListString)
                    });
                }
            });
            _manageProxyViewModel.FilterProxies();
            if (OperatingSystem.IsMacOS())
            {
                // 创建原生菜单
                MainWindow.Instance.NativeMenuBar = [];
                MainWindow.Instance.NativeMenuBar.NeedsUpdate += (sender, args) =>
                {
                    // 添加应用程序菜单（macOS 第一个菜单）
                    var appMenu = new NativeMenuItem(Languages.Text_ManageProxy_MenuTunnels);
                    var appSubMenu = new NativeMenu
                    {
                        new NativeMenuItem(Languages.Text_ManageProxy_ManageTunnels)
                        {
                            Gesture = KeyGesture.Parse("Ctrl+M"),
                            Command = ReactiveCommand.Create(() =>
                            {
                                MainPageFrameViewModel.Instance?.NavigateToPage("Manage");
                            })
                        },
                        new NativeMenuItemSeparator(),
                        new NativeMenuItem(Languages.Text_ManageProxy_CreateTunnel)
                        {
                            Gesture = KeyGesture.Parse("Ctrl+D"),
                            Command = ReactiveCommand.Create(() =>
                            {
                                MainPageFrameViewModel.Instance?.NavigateToPage("Create");
                            })
                        },
                        new NativeMenuItemSeparator()
                    };

                    var tmp_launchProxy = new NativeMenu();
                    foreach (var proxy in _manageProxyViewModel.AllProxies)
                    {
                        tmp_launchProxy.Add(new NativeMenuItem(proxy.proxyName)
                        {
                            Command = proxy.LaunchProxyCommand
                        });
                    }

                    appSubMenu.Add(new NativeMenuItem(Languages.Text_ManageProxy_LaunchTunnel)
                    {
                        Menu = tmp_launchProxy
                    });
                    appSubMenu.Add(new NativeMenuItemSeparator());
                    appSubMenu.Add(new NativeMenuItem(Languages.Text_ManageProxy_ExitApp)
                    {
                        Gesture = KeyGesture.Parse("Ctrl+Q"),
                        Command = ReactiveCommand.Create(() =>
                        {
                            App.Desktop.Shutdown();
                        })
                    });

                    appMenu.Menu = appSubMenu;
                    MainWindow.Instance.NativeMenuBar.Add(appMenu);

                    // 设置菜单栏
                    NativeMenu.SetMenu(MainWindow.Instance, MainWindow.Instance.NativeMenuBar);
                };
            }

            Core.App.CurrentLogger.LogDebug("Loading over. AllFilteredProxies: " +
                                            _manageProxyViewModel.AllProxies.Count);
            await Dispatcher.UIThread.InvokeAsync(() =>
                MainPageFrameViewModel.Instance?.IsLoading = false);
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger.Error(ex);
            Core.App.CurrentLogger.Log($"Error in LoadProxies: {ex.Message}", EnumLogType.Error);
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                MainPageFrameViewModel.Instance?.IsLoading = false);
            _isLoadingProxies = false;
        }
    }

    private async void RefreshProxies(object sender, RoutedEventArgs e) => await LoadProxies();

    private void Entry(object sender, RoutedEventArgs e)
    {
        // Implement animation if needed
        BatchOperationArea.IsVisible = true;
    }

    private async void DownloadMEFClient(object? sender, RoutedEventArgs e)
    {
        try
        {
            var result =
                await new DownloadHelper(this).DownloadMEFrpClient(Environment.OSVersion);
            if (result)
            {
                await MessageBox.ShowAsync(Languages.Text_ManageProxy_DownloadCompleted, Languages.Caption_Hint,
                    MessageBoxIcon.Info);
            }
        }
        catch (OperationCanceledException)
        {
            await MessageBox.ShowAsync(Languages.Text_ManageProxy_DownloadCancelled, Languages.Caption_Hint,
                MessageBoxIcon.Warning);
        }
    }

    private async void OpenHelp(object? sender, RoutedEventArgs e)
    {
        var cd = new ContentDialog
        {
            Title = Languages.Text_ManageProxy_HelpTitle,
            PrimaryButtonText = Languages.Text_Global_Confirm,
            DefaultButton = ContentDialogButton.Primary,
            IsSecondaryButtonEnabled = false,
            CloseButtonText = Languages.Text_Global_Close,
            Content = new ManageProxyHelp()
        };
        await cd.ShowAsync(Core.App.MainWindow);
    }
}

public class NotifyingCollection<T> : ObservableCollection<T>
{
    public event EventHandler CollectionChangedWithNotification;

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnCollectionChanged(e);
        CollectionChangedWithNotification?.Invoke(this, EventArgs.Empty);
    }
}

public enum ViewMode
{
    Grid,
    List
}