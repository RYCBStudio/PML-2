using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Threading;
using MEFrpLauncherX.Core.MEFIntergrated;

namespace MEFrpLauncherX.ViewModels;

public class NodesOverviewViewModel : INotifyPropertyChanged
{
    public AvaloniaList<InfoClasses.NodeStatus> AllNodes
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
            UpdateFilteredNodes();
            UpdateSummary();
        }
    } = [];

    public AvaloniaList<InfoClasses.NodeStatus> FilteredNodes
    {
        get;
    } = [];

    public string SearchText
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
            UpdateFilteredNodes();
        }
    } = "";

    public bool IsDescending
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
            UpdateFilteredNodes();
        }
    }

    public SortOption SelectedSortOption
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
            UpdateFilteredNodes();
        }
    }

    public List<SortOption> SortOptions
    {
        get;
    } =
    [
        new("节点ID", nameof(InfoClasses.NodeStatus.nodeId)),
        new("在线隧道数", nameof(InfoClasses.NodeStatus.onlineProxy)),
        new("当前连接数", nameof(InfoClasses.NodeStatus.curConns)),
        new("负载百分比", nameof(InfoClasses.NodeStatus.loadPercent)),
        new("今日总流量", "DailyTraffic"),
        new("运行时长", nameof(InfoClasses.NodeStatus.uptime))
    ];

    // 统计属性
    public int TotalNodes => AllNodes?.Count ?? 0;
    public int OnlineNodes => AllNodes?.Count(n => n.isOnline) ?? 0;
    public int TotalOnlineUsers => AllNodes?.Sum(n => n.onlineClient) ?? 0;
    public int TotalOnlineProxies => AllNodes?.Sum(n => n.onlineProxy) ?? 0;
    public long TotalInTraffic => AllNodes?.Sum(n => n.totalTrafficIn) ?? 0;
    public long TotalOutTraffic => AllNodes?.Sum(n => n.totalTrafficOut) ?? 0;
    
    public bool IsLoading
    {
        get;
        private set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public NodesOverviewViewModel()
    {
        SelectedSortOption = SortOptions[0];
        LoadData();
    }

    private async void LoadData()
    {
        IsLoading = true;
        var res = await Task.Run(MEFApiConverter.GetNodesStatusAsync);
        if (res.code != 200)
        {
            IsLoading = false;
            return;
        }
        var result = new InfoClasses.NodesStatusInfo
        {
            NodesStatus = res.data
        };

        AllNodes = new AvaloniaList<InfoClasses.NodeStatus>(result.NodesStatus);
    }

    private async void UpdateFilteredNodes()
    {
        IsLoading = true;
        if (AllNodes == null)
        {
            return;
        }

        var query = AllNodes.AsEnumerable();

        // 应用搜索过滤
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var searchText = SearchText.ToLower();
            query = query.Where(n =>
                n.nodeId.ToString().Contains(searchText) ||
                (n.name?.ToLower().Contains(searchText, StringComparison.CurrentCultureIgnoreCase) ??
                 false) ||
                (n.version?.ToLower().Contains(searchText, StringComparison.CurrentCultureIgnoreCase) ??
                 false) ||
                $"{n.totalTrafficIn + n.totalTrafficOut}".Contains(searchText));
        }

        await Task.Run(() =>
        {
            // 应用排序
            if (SelectedSortOption != null)
            {
                query = SelectedSortOption.PropertyName switch
                {
                    nameof(InfoClasses.NodeStatus.nodeId) => IsDescending
                        ? query.OrderByDescending(n => n.nodeId)
                        : query.OrderBy(n => n.nodeId),
                    nameof(InfoClasses.NodeStatus.onlineProxy) => IsDescending
                        ? query.OrderByDescending(n => n.onlineProxy)
                        : query.OrderBy(n => n.onlineProxy),
                    nameof(InfoClasses.NodeStatus.curConns) => IsDescending
                        ? query.OrderByDescending(n => n.curConns)
                        : query.OrderBy(n => n.curConns),
                    nameof(InfoClasses.NodeStatus.loadPercent) => IsDescending
                        ? query.OrderByDescending(n => n.loadPercent)
                        : query.OrderBy(n => n.loadPercent),
                    "DailyTraffic" => IsDescending
                        ? query.OrderByDescending(n => n.totalTrafficIn + n.totalTrafficOut)
                        : query.OrderBy(n => n.totalTrafficIn + n.totalTrafficOut),
                    nameof(InfoClasses.NodeStatus.uptime) => IsDescending
                        ? query.OrderByDescending(n => n.uptime)
                        : query.OrderBy(n => n.uptime),
                    _ => query
                };
            }
        });
        FilteredNodes.Clear();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var node in query)
            {
                FilteredNodes.Add(node);
            }
        });
        IsLoading = false;
    }

    private void UpdateSummary()
    {
        OnPropertyChanged(nameof(TotalNodes));
        OnPropertyChanged(nameof(OnlineNodes));
        OnPropertyChanged(nameof(TotalOnlineUsers));
        OnPropertyChanged(nameof(TotalOnlineProxies));
        OnPropertyChanged(nameof(TotalInTraffic));
        OnPropertyChanged(nameof(TotalOutTraffic));
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class SortOption
{
    public string DisplayName
    {
        get;
        set;
    }

    public string PropertyName
    {
        get;
        set;
    }

    public SortOption(string displayName, string propertyName)
    {
        DisplayName = displayName;
        PropertyName = propertyName;
    }
}