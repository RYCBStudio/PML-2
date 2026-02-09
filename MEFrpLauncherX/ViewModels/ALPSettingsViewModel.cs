using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.MEFIntergrated;
using MEFrpLauncherX.Views;
using ReactiveUI;

namespace MEFrpLauncherX.ViewModels;

public class ALPSettingsViewModel : ViewModelBase
{
    private bool _isLoadingProxies;

    public UserProxyViewModel SelectedLeft
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public UserProxyViewModel SelectedRight
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string SearchText
    {
        get;
        set
        {
            field = value;
            FilterProxies();
        }
    } = string.Empty;

    public string SearchText1
    {
        get;
        set
        {
            field = value;
            FilterProxies1();
        }
    } = string.Empty;

    public ObservableCollection<UserProxyViewModel> FilteredProxies
    {
        get;
    } = [];

    public ObservableCollection<UserProxyViewModel> AddedProxies
    {
        get;
    } = [];

    public ObservableCollection<UserProxyViewModel> AllAddedProxies
    {
        get;
    } = [];

    public ObservableCollection<UserProxyViewModel> AllFilteredProxies
    {
        get;
        set;
    } = [];

    public List<UserProxyViewModel> AllProxies
    {
        get;
        set;
    } = [];

    public ICommand TransferSelectedProxyToRightCommand
    {
        get;
    }

    public ICommand TransferSelectedProxyToLeftCommand
    {
        get;
    }

    public bool IsLoading
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public async void LoadProxies()
    {
        if (_isLoadingProxies)
        {
            Core.App.CurrentLogger.LogDebug("LoadProxies already in progress, skipping duplicate call");
            return;
        }

        _isLoadingProxies = true;
        
        IsLoading = true;
        try
        {
            Core.App.CurrentLogger.LogDebug("Starting LoadProxies");
            // Avalonia animations work differently - you might need to implement fade effects differently
            AllFilteredProxies.Clear();
            FilteredProxies.Clear();
            await Task.Run(async () =>
            {
                var autoLaunchProxies = ConfigManager.CurrentConfig.AutoLaunchProxies;
                var userProxies = (await MEFApiConverter.GetProxiesAsync()).data;
                var currentNodesListInfo = await MEFApiConverter.EnsureNodesListInfoAsync();
                InfoClasses.NodesList[] currentNodesList;

                if (currentNodesListInfo?.NodesList is null)
                {
                    currentNodesList = (await MEFApiConverter.GetNodesInfoAsync()).data;
                }
                else
                {
                    currentNodesList = currentNodesListInfo.NodesList;
                }

                Core.App.CurrentLogger.LogDebug("Loading user proxies");
                foreach (var item in userProxies)
                {
                    var alp = autoLaunchProxies.FirstOrDefault(i => i.Id == item.proxyId);
                    var info = currentNodesList.FirstOrDefault(i => i.nodeId == item.nodeId);
                    var proxy = new UserProxyViewModel
                    {
                        // Same property assignments as original
                        username = item.username,
                        proxyName = item.proxyName,
                        proxyId = item.proxyId,
                        proxyType = item.proxyType.ToUpper(),
                        node = info?.name ?? "节点不存在",
                        isBanned = item.isBanned,
                        isOnline = item.isOnline,
                        isDisabled = item.isDisabled,
                        localIp = item.localIp,
                        localPort = item.localPort,
                        remotePort = item.remotePort,
                        runId = item.runId,
                        nodeId = item.nodeId,
                        allowedProtocols = info?.allowType?.Split(';')
                            ?.Select(type => type.ToUpper())
                            ?.ToArray() ?? [],
                        domain = item.domain,
                        lastStartTime = item.lastStartTime,
                        lastCloseTime = item.lastCloseTime,
                        proxyProtocolVersion = item.proxyProtocolVersion,
                        clientVersion = item.clientVersion,
                        useEncryption = item.useEncryption,
                        useCompression = item.useCompression,
                        location = item.location,
                        accessKey = item.accessKey,
                        hostHeaderRewrite = item.hostHeaderRewrite,
                        headerXFromWhere = item.headerXFromWhere
                    };
                    AllProxies.Add(proxy);
                    if (alp is not null)
                    {
                        continue;
                    }

                    AllFilteredProxies.Add(proxy);
                }
            });
            FilterProxies();
            Core.App.CurrentLogger.LogDebug("Loading over. AllFilteredProxies: " + AllFilteredProxies.Count);
            LoadALProxies();
            IsLoading = false;
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger.Error(ex);
            Core.App.CurrentLogger.Log($"Error in LoadProxies: {ex.Message}", EnumLogType.Error);
        }
        finally
        {
            IsLoading = false;
            _isLoadingProxies = false;
        }
    }

    public async void LoadALProxies()
    {
        IsLoading = true;
        try
        {
            Core.App.CurrentLogger.LogDebug("Starting LoadALProxies");
            AllAddedProxies.Clear();
            AddedProxies.Clear();
            await Task.Run(() =>
            {
                var userProxies = AllProxies;
                var autoLaunchProxies = ConfigManager.CurrentConfig.AutoLaunchProxies;
                var currentNodesListInfo = MEFApiConverter.CurrentNodesListInfo;
                //var currentNodesStatusInfo = MEFApiConverter.CurrentNodesStatusInfo;
                var currentNodesList = currentNodesListInfo.NodesList;


                Core.App.CurrentLogger.LogDebug("Loading user proxies");
                foreach (var proxy in autoLaunchProxies)
                {
                    var userProxy = userProxies.FirstOrDefault(i => i.proxyId == proxy.Id);
                    var info = currentNodesList.FirstOrDefault(i => i.nodeId == userProxy?.nodeId);
                    //var status = currentNodesStatusList.FirstOrDefault(i => i.nodeId == userProxy?.nodeId);
                    if (userProxy is null)
                    {
                        continue;
                    }

                    AllAddedProxies.Add(new UserProxyViewModel
                    {
                        // Same property assignments as original
                        username = userProxy.username,
                        proxyName = userProxy.proxyName,
                        proxyId = userProxy.proxyId,
                        proxyType = userProxy.proxyType.ToUpper(),
                        node = info?.name ?? "节点不存在",
                        isBanned = userProxy.isBanned,
                        isOnline = userProxy.isOnline,
                        isDisabled = userProxy.isDisabled,
                        localIp = userProxy.localIp,
                        localPort = userProxy.localPort,
                        remotePort = userProxy.remotePort,
                        runId = userProxy.runId,
                        nodeId = userProxy.nodeId,
                        allowedProtocols = info?.allowType?.Split(';')
                            ?.Select(type => type.ToUpper())
                            ?.ToArray() ?? [],
                        domain = userProxy.domain,
                        lastStartTime = userProxy.lastStartTime,
                        lastCloseTime = userProxy.lastCloseTime,
                        proxyProtocolVersion = userProxy.proxyProtocolVersion,
                        clientVersion = userProxy.clientVersion,
                        useEncryption = userProxy.useEncryption,
                        useCompression = userProxy.useCompression,
                        location = userProxy.location,
                        accessKey = userProxy.accessKey,
                        hostHeaderRewrite = userProxy.hostHeaderRewrite,
                        headerXFromWhere = userProxy.headerXFromWhere,
                        UseConfig = proxy.UseConfig,
                        Config = proxy.Config,
                    });
                }
            });
            FilterProxies1();
            Core.App.CurrentLogger.LogDebug("Loading over. AllFilteredProxies: " + AllFilteredProxies.Count);
            IsLoading = false;
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger.Error(ex);
            Core.App.CurrentLogger.Log($"Error in LoadALProxies: {ex.Message}", EnumLogType.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public ALPSettingsViewModel()
    {
        TransferSelectedProxyToRightCommand = new RelayCommand<UserProxyViewModel>(TransferSelectedProxyToRight);
        TransferSelectedProxyToLeftCommand = new RelayCommand<UserProxyViewModel>(TransferSelectedProxyToLeft);
        LoadProxies();
    }

    private void TransferSelectedProxyToRight(UserProxyViewModel proxy)
    {
        if (proxy is null)
        {
            return;
        }

        AllAddedProxies.Add(proxy);
        FilterProxies1();
        AllFilteredProxies.Remove(proxy);
        FilterProxies();
    }

    private void TransferSelectedProxyToLeft(UserProxyViewModel proxy)
    {
        if (proxy is null)
        {
            return;
        }

        AllAddedProxies.Remove(proxy);
        FilterProxies1();
        AllFilteredProxies.Add(proxy);
        FilterProxies();
    }

    public async void FilterProxies()
    {
        IsLoading = true;
        Core.App.CurrentLogger.LogDebug("开始筛选隧道");
        FilteredProxies.Clear();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var filtered = AllFilteredProxies.Where(proxy =>
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

        IsLoading = false;
        Core.App.CurrentLogger.LogDebug("筛选完成，数量: " + FilteredProxies.Count);
    }

    public async void FilterProxies1()
    {
        IsLoading = true;
        Core.App.CurrentLogger.LogDebug("开始筛选隧道");
        AddedProxies.Clear();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var filtered = AllAddedProxies.Where(proxy =>
                string.IsNullOrEmpty(SearchText1) ||
                proxy.proxyName.Contains(SearchText1, StringComparison.OrdinalIgnoreCase) ||
                PinYinHelper.ConvertToAllSpell(proxy.proxyName)
                    .Contains(SearchText1, StringComparison.OrdinalIgnoreCase) ||
                (SearchText1.Replace(" ", string.Empty).StartsWith("/pid:") &&
                 proxy.proxyId.ToString().Contains(SearchText1[5..])) ||
                (SearchText1.Replace(" ", string.Empty).StartsWith("/n:") &&
                 (proxy.node.Contains(SearchText1[3..]) ||
                  PinYinHelper.ConvertToAllSpell(proxy.node)
                      .Contains(SearchText1[3..], StringComparison.OrdinalIgnoreCase))) ||
                (SearchText1.Replace(" ", string.Empty).StartsWith("/nid:") &&
                 proxy.nodeId.ToString().Contains(SearchText1[5..])));
            foreach (var proxy in filtered)
            {
                AddedProxies.Add(proxy);
            }
        });

        IsLoading = false;
        Core.App.CurrentLogger.LogDebug("筛选完成，数量: " + FilteredProxies.Count);
    }
}