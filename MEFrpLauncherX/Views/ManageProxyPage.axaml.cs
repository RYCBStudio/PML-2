using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using MEFrpLauncherX.Controls;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.MEFIntergrated;
using MEFrpLauncherX.ViewModels;
using MsBox.Avalonia.ViewModels.Commands;
using Newtonsoft.Json;
using ReactiveUI;
using MessageBox = MEFrpLauncherX.Core.Controls.MessageBox;

namespace MEFrpLauncherX.Views;

public partial class ManageProxyPage : UserControl
{
    private bool _isLoadingProxies;
    private bool _isFirstLoad = true;

    public static ManageProxyPage Instance
    {
        get;
        set;
    }

    private readonly ProxyViewModel proxyViewModel;

    public ManageProxyPage()
    {
        InitializeComponent();
        proxyViewModel = new ProxyViewModel();
        DataContext = proxyViewModel;
        AttachedToVisualTree += ManageProxyPage_Loaded;
        SearchBox.ItemsSource = new List<string> { "/pid:", "/nid:", "/n:" }.OrderBy(x => x);
    }

    private void ManageProxyPage_Loaded(object sender, VisualTreeAttachmentEventArgs e)
    {
        LoadProxies();
        Instance = this;
    }

    public async void LoadProxies()
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
            proxyViewModel.AllProxies.Clear();
            proxyViewModel.FilteredProxies.Clear();
            await Task.Run(async () =>
            {
                InfoClasses.ProxyInfo userProxies = null;
                try
                {
                    userProxies = (await MEFApiConverter.GetProxiesAsync()).data;
                }
                catch
                {
                    
                }
                // var currentNodesListInfo = MEFApiConverter.CurrentNodesListInfo;
                // InfoClasses.NodesList[] currentNodesList;
                //
                // if (currentNodesListInfo?.NodesList is null)
                // {
                //     currentNodesList = (await MEFApiConverter.GetNodesInfoAsync()).data;
                // }
                // else
                // {
                //     currentNodesList = currentNodesListInfo.NodesList;
                // }

#if DEBUG
                var db_nodes = new List<InfoClasses.Nodes>
                {
                    new InfoClasses.Nodes
                    {
                        nodeId = 0,
                        name = "DEBUG",
                        hostname = "114.514.191.810"
                    }
                };
                var db_proxy = new List<InfoClasses.Proxies>
                {
                    new InfoClasses.Proxies
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

                    proxyViewModel.AllProxies.Add(new UserProxyViewModel(item.domain)
                    {
                        // Same property assignments as original
                        username = item.username,
                        proxyName = item.proxyName,
                        proxyId = item.proxyId,
                        proxyType = item.proxyType.ToUpper(),
                        node = node?.name ?? "节点不存在",
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
                        RequestHeaders = JsonConvert.DeserializeObject<Dictionary<string, string>>(item.requestHeaders),
                        ResponseHeaders =
                            JsonConvert.DeserializeObject<Dictionary<string, string>>(item.responseHeaders),
                        Locations = JsonConvert.DeserializeObject<List<string>>(item.locations),
                    });
                }
            });
            proxyViewModel.FilterProxies();
            if (OperatingSystem.IsMacOS())
            {
                // 创建原生菜单
                MainWindow.Instance.NativeMenuBar = [];

                // 添加应用程序菜单（macOS 第一个菜单）
                var appMenu = new NativeMenuItem("隧道");
                var appSubMenu = new NativeMenu();

                appSubMenu.Add(new NativeMenuItem("管理隧道")
                {
                    Gesture = KeyGesture.Parse("Ctrl+M"),
                    Command = ReactiveCommand.Create(() =>
                    {
                        MainPageFrameViewModel.Instance?.NavigateToPage("Manage");
                    })
                });
                appSubMenu.Add(new NativeMenuItemSeparator());
                appSubMenu.Add(new NativeMenuItem("创建隧道")
                {
                    Gesture = KeyGesture.Parse("Ctrl+D"),
                    Command = ReactiveCommand.Create(() =>
                    {
                        MainPageFrameViewModel.Instance?.NavigateToPage("Create");
                    })
                });
                appSubMenu.Add(new NativeMenuItemSeparator());
                var tmp_launchProxy = new NativeMenu();
                foreach (var proxy in proxyViewModel.AllProxies)
                {
                    tmp_launchProxy.Add(new NativeMenuItem(proxy.proxyName)
                    {
                        Command = proxy.LaunchProxyCommand
                    });
                }

                appSubMenu.Add(new NativeMenuItem("启动隧道")
                {
                    Menu = tmp_launchProxy
                });
                appSubMenu.Add(new NativeMenuItemSeparator());
                appSubMenu.Add(new NativeMenuItem("退出程序")
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
            }

            Core.App.CurrentLogger.LogDebug("Loading over. AllFilteredProxies: " + proxyViewModel.AllProxies.Count);
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

    private void RefreshProxies(object sender, RoutedEventArgs e)
    {
        LoadProxies();
    }

    private void Entry(object sender, RoutedEventArgs e)
    {
        // Implement animation if needed
        BatchOperationArea.IsVisible = true;
    }

    private void UnEntry(object sender, RoutedEventArgs e)
    {
        BatchOperationArea.IsVisible = false;
    }

    private void BatchOperationArea_IsVisibleChanged(object sender, RoutedEventArgs e)
    {
        // Handle visibility changes if needed
    }

    private async void DownloadMEFClient(object? sender, RoutedEventArgs e)
    {
        try
        {
            var result =
                await new DownloadHelper(this).DownloadMEFrpClient(Environment.OSVersion);
            if (result)
            {
                await MessageBox.ShowAsync("下载完成", "提示", MessageBoxIcon.Info);
            }
        }
        catch (OperationCanceledException)
        {
            await MessageBox.ShowAsync("下载取消", "提示", MessageBoxIcon.Warning);
        }
    }

    private async void OpenHelp(object? sender, RoutedEventArgs e)
    {
        var cd = new ContentDialog
        {
            Title = "帮助",
            PrimaryButtonText = "确定",
            DefaultButton = ContentDialogButton.Primary,
            IsSecondaryButtonEnabled = false,
            CloseButtonText = "关闭"
        };
        cd.Content = new ManageProxyHelp();
        await cd.ShowAsync(Core.App.MainWindow);
    }
}

public sealed class ProxyViewModel : ViewModelBase
{
    public string SearchText
    {
        get;
        set
        {
            field = value;
            FilterProxies();
        }
    } = string.Empty;

    public async void FilterProxies()
    {
        MainPageFrameViewModel.Instance.IsLoading = true;
        Core.App.CurrentLogger.LogDebug("开始筛选隧道");
        FilteredProxies.Clear();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var filtered = AllProxies.Where(proxy =>
                string.IsNullOrEmpty(SearchText) ||
                proxy.proxyName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                PinYinHelper.ConvertToAllSpell(proxy.proxyName)
                    .Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                (SearchText.Replace(" ", string.Empty).StartsWith("/pid:") &&
                 proxy.proxyId.ToString().Contains(SearchText[5..])) ||
                (SearchText.Replace(" ", string.Empty).StartsWith("/n:") &&
                 (proxy.node.Contains(SearchText[3..]) ||
                  PinYinHelper.ConvertToAllSpell(proxy.node)
                      .Contains(SearchText[3..], StringComparison.OrdinalIgnoreCase))) ||
                (SearchText.Replace(" ", string.Empty).StartsWith("/nid:") &&
                 proxy.nodeId.ToString().Contains(SearchText[5..])));
            foreach (var proxy in filtered)
            {
                FilteredProxies.Add(proxy);
            }
        });

        MainPageFrameViewModel.Instance.IsLoading = false;
        Core.App.CurrentLogger.LogDebug("筛选完成，数量: " + FilteredProxies.Count);
    }

    public bool IsDetailedMode
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            // 当详细模式改变时，更新所有代理的详细状态
            UpdateAllProxiesDetailedStatus(value);
        }
    }

    private void UpdateAllProxiesDetailedStatus(bool isDetailed)
    {
        foreach (var proxy in FilteredProxies)
        {
            proxy.Detailed = isDetailed;
        }
    }


    public ObservableCollection<UserProxyViewModel> FilteredProxies
    {
        get;
    } = [];

    public ObservableCollection<UserProxyViewModel> AllProxies
    {
        get;
        set;
    } = [];

    public ICommand LaunchProxyCommand
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

    public event PropertyChangedEventHandler PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public ViewMode CurrentViewMode
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = ViewMode.Grid;

    public ICommand SwitchViewCommand
    {
        get;
    }

    public ICommand SelectProxyCommand
    {
        get;
    }

    public ICommand DeselectProxyCommand
    {
        get;
    }

    public ICommand ToggleSelectProxyCommand
    {
        get;
    }

    public ICommand ClearSelectionCommand
    {
        get;
    }

    public ProxyViewModel()
    {
        SelectedProxies = [];
        SwitchViewCommand = new RelayCommand<ViewMode>(mode => CurrentViewMode = mode);
        SelectProxyCommand = new RelayCommand<UserProxyViewModel>(SelectProxy);
        DeselectProxyCommand = new RelayCommand<UserProxyViewModel>(DeselectProxy);
        ToggleSelectProxyCommand = new RelayCommand<UserProxyViewModel>(ToggleSelectProxy);
        ClearSelectionCommand = new RelayCommand(ClearSelection);
    }

    private void SelectProxy(UserProxyViewModel proxy)
    {
        if (proxy == null || proxy.IsSelected)
        {
            return;
        }

        proxy.IsSelected = true;
        SelectedProxies.Add(proxy);
    }

    private void DeselectProxy(UserProxyViewModel proxy)
    {
        if (proxy == null || !proxy.IsSelected)
        {
            return;
        }

        proxy.IsSelected = false;
        SelectedProxies.Remove(proxy);
    }

    private void ToggleSelectProxy(UserProxyViewModel proxy)
    {
        if (proxy == null)
        {
            return;
        }

        proxy.IsSelected = !proxy.IsSelected;

        if (proxy.IsSelected)
        {
            if (!SelectedProxies.Contains(proxy))
            {
                SelectedProxies.Add(proxy); // 现在这会自动触发通知
            }
        }
        else
        {
            SelectedProxies.Remove(proxy); // 现在这会自动触发通知
        }
    }

    // 添加清除选择的方法
    public void ClearSelection(object s)
    {
        foreach (var proxy in SelectedProxies.ToList())
        {
            proxy.IsSelected = false;
        }

        SelectedProxies.Clear();
    }

    public NotifyingCollection<UserProxyViewModel> SelectedProxies
    {
        get;
        set
        {
            if (field != null)
            {
                field.CollectionChangedWithNotification -= OnSelectionChanged;
            }

            field = value;

            if (field != null)
            {
                field.CollectionChangedWithNotification += OnSelectionChanged;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(IsAnyProxySelected));
        }
    }

    private void OnSelectionChanged(object sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsAnyProxySelected));
    }

    public bool IsAnyProxySelected => SelectedProxies?.Count > 1;

    public bool IsDark => ConfigManager.CurrentConfig.Theme.Equals("dark", StringComparison.OrdinalIgnoreCase);
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

public class RelayCommand<T> : ICommand
{
    private readonly Action<T> _execute;
    private readonly Predicate<T> _canExecute;

    public RelayCommand(Action<T> execute, Predicate<T> canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object parameter) => _canExecute?.Invoke((T)parameter) ?? true;

    public void Execute(object parameter) => _execute((T)parameter);

    public event EventHandler CanExecuteChanged
    {
        add
        {
        }
        remove
        {
        }
    }
}

public enum ViewMode
{
    Grid,
    List
}