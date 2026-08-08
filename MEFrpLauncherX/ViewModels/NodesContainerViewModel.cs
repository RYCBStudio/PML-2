using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.MEFIntegrated;
using MEFrpLauncherX.ViewModels.Controls;
using ReactiveUI;

namespace MEFrpLauncherX.ViewModels;

public class NodesContainerViewModel : ViewModelBase
{
    private const int DEBOUNCE_DELAY_MS = 300;
    private readonly DispatcherTimer _debounceTimer;
    private DispatcherTimer _loadingDebounceTimer;

// 在构造函数中初始化
    public NodesContainerViewModel()
    {
        // 初始化防抖定时器
        _debounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(DEBOUNCE_DELAY_MS)
        };
        _debounceTimer.Tick += (_, _) =>
        {
            _debounceTimer.Stop();
            FilterNodes();
        };
    }

    public bool IsLoading
    {
        get;
        set
        {
            if (field != value)
            {
                this.RaiseAndSetIfChanged(ref field, value);

                // 添加防抖，避免频繁切换
                _loadingDebounceTimer?.Stop();
                if (value)
                {
                    // 立即显示
                    UpdateLoadingUI(true);
                }
                else
                {
                    // 延迟隐藏
                    _loadingDebounceTimer ??= new DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(100)
                    };
                    _loadingDebounceTimer.Tick += (_, _) =>
                    {
                        _loadingDebounceTimer.Stop();
                        UpdateLoadingUI(false);
                    };
                    _loadingDebounceTimer.Start();
                }
            }
        }
    }

    public List<TunnelNodeViewModel> AllNodes
    {
        get;
        set;
    } = [];

    public AvaloniaList<TunnelNodeViewModel> FilteredNodes
    {
        get;
    } = [];

    public Dictionary<string, string> RegionOptions
    {
        get;
    } = new()
    {
        { "all", "全部地区" },
        { "cn", "中国大陆" },
        { "cnos", "港澳台" },
        { "oversea", "海外" }
    };

    private string _selectedRegionString = "all";

    public object SelectedRegion
    {
        get;
        set
        {
            if (field != value)
            {
                this.RaiseAndSetIfChanged(ref field, value);
                // Update the string representation
                if (value is TabStripItem item)
                {
                    _selectedRegionString = item.Tag?.ToString() ?? "all";
                }
                else
                {
                    _selectedRegionString = value?.ToString() ?? "all";
                }

                TriggerFilterWithDebounce();
            }
        }
    } = "all";

    public string? SearchText
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            TriggerFilterWithDebounce();
            // field = value;
            // OnPropertyChanged();
            //FilterNodes();
        }
    }

    public bool FilterCanBuildSite
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            TriggerFilterWithDebounce();
            // field = value;
            // OnPropertyChanged();
            //FilterNodes();
        }
    }

    public bool FilterAllowHighTraffic
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            TriggerFilterWithDebounce();
            // field = value;
            // OnPropertyChanged();
            //FilterNodes();
        }
    }

    public bool FilterNotOverLoaded
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            TriggerFilterWithDebounce();
            // field = value;
            // OnPropertyChanged();
            //FilterNodes();
        }
    }

    public TunnelNodeViewModel? SelectedNode
    {
        get;
        set
        {
            field?.IsSelected = false;

            this.RaiseAndSetIfChanged(ref field, value);
            // if (field is { IsOverloaded: true })
            // {
            //     field.IsSelected = false;
            //     var tmplsi = (NodesContainer.Instance.Nodes.ContainerFromItem(field) as
            //         ListBoxItem)!;
            //     if (tmplsi != null)
            //     {
            //         tmplsi.IsEnabled = false;
            //     }
            //
            //     NodesContainer.Instance.Nodes.SelectedIndex = -1;
            //     return;
            // }

            field?.IsSelected = true;

            NodeSelected?.Invoke(field);
        }
    }

// 添加防抖触发方法
    private void TriggerFilterWithDebounce()
    {
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private void UpdateLoadingUI(bool _)
    {
        // 分别更新各个loading状态
        this.RaisePropertyChanged(nameof(IsLoading));
    }

    public async Task LoadNodesAsync(InfoClasses.NodesListInfo listInfo, InfoClasses.NodesStatusInfo statusInfo)
    {
        try
        {
            IsLoading = true;
            AllNodes.Clear();
            FilteredNodes.Clear();

            var statusArray = statusInfo.NodesStatus;
            if (statusArray is null || statusArray.Length < 1)
            {
                statusArray = (await MEFrpApiConverter.GetNodesStatusAsync()).data;
            }

            // 预构建状态字典，O(1) 查找
            var statusDict = statusArray?.ToDictionary(s => s.nodeId) ?? [];

            var nodesList = await Task.Run(() =>
            {
                var list = new List<TunnelNodeViewModel>(listInfo.NodesList.Length);
                foreach (var node in listInfo.NodesList)
                {
                    statusDict.TryGetValue(node.nodeId, out var status);
                    var allowTypes = node.allowType?.Split(';')
                        ?.Select(t => t.ToUpperInvariant())
                        .ToList() ?? [];

                    var vm = new TunnelNodeViewModel
                    {
                        NodeId = node.nodeId,
                        Name = node.name,
                        Description = node.description,
                        AllowTypes = allowTypes,
                        Bandwidth = node.bandwidth,
                        LoadPercent = status?.loadPercent ?? 0,
                        IsOnline = status?.isOnline ?? false,
                        AllowPorts = node.allowPort,
                        CanBuildSite = allowTypes.Any(t =>
                            t is "HTTP" or "HTTPS"),
                        Region = node.region,
                        AllowGroup = node.allowGroup?.Split(';') ?? []
                    };
                    vm.AllowHighTraffic = CalculateAllowHighTraffic(vm.Bandwidth);
                    list.Add(vm);
                }

                return list;
            });

            AllNodes = nodesList;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                FilterNodes(true);
            }, DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger.Log($"加载节点时出错: {ex.Message}");
            Core.App.CurrentLogger.Error(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    // 提取高流量计算逻辑
    private static bool CalculateAllowHighTraffic(string bandwidth)
    {
        if (string.IsNullOrEmpty(bandwidth))
        {
            return false;
        }

        var lowerBandwidth = bandwidth.ToLower();

        if (lowerBandwidth.Contains("gbps"))
        {
            if (double.TryParse(lowerBandwidth.Replace("gbps", ""), out var gbpsValue))
            {
                return gbpsValue >= 0.07;
            }
        }
        else if (lowerBandwidth.Contains("mbps"))
        {
            if (double.TryParse(lowerBandwidth.Replace("mbps", ""), out var mbpsValue))
            {
                return mbpsValue >= 70;
            }
        }

        return false;
    }

    public event Action<TunnelNodeViewModel> NodeSelected;

    private async void FilterNodes(bool force = false)
    {
        if (IsLoading && !force)
        {
            return;
        }

        try
        {
            // 只在初次加载时显示 loading
            if (force)
                IsLoading = true;

            var allNodes = AllNodes;
            var searchText = SearchText;
            var regionString = _selectedRegionString;
            var filterSite = FilterCanBuildSite;
            var filterTraffic = FilterAllowHighTraffic;
            var filterOverload = FilterNotOverLoaded;

            var filteredNodes = await Task.Run(() =>
            {
                return allNodes.Where(node =>
                        MeetsSearchCriteriaCore(node, searchText) &&
                        IsRegionMeetsCore(node, regionString) &&
                        (!filterSite || node.CanBuildSite) &&
                        (!filterTraffic || node.AllowHighTraffic) &&
                        (!filterOverload || node.IsNotOverloaded))
                    .ToList();
            });

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                FilteredNodes.Clear();
                FilteredNodes.AddRange(filteredNodes);
            }, DispatcherPriority.Background);

            Core.App.CurrentLogger.Log($"{FilteredNodes.Count} nodes added.", EnumLogType.Debug);
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger.Error(ex);
        }
        finally
        {
            if (force)
                IsLoading = false;
        }

        NodesChanged?.Invoke();
    }

    public event Action? NodesChanged;

// 提取搜索逻辑到单独方法
    public bool MeetsSearchCriteria(TunnelNodeViewModel node)
        => MeetsSearchCriteriaCore(node, SearchText);

    private static bool MeetsSearchCriteriaCore(TunnelNodeViewModel node, string? searchText)
    {
        if (string.IsNullOrEmpty(searchText))
        {
            return true;
        }

        return searchText.StartsWith("/d:")
            ? node.Description.Contains(searchText.Remove(0, 3), StringComparison.OrdinalIgnoreCase)
            : searchText.StartsWith("/pd:")
                ? PinYinHelper.ConvertToAllSpellWithCache(node.Description).Contains(
                    PinYinHelper.ConvertToAllSpellWithCache(searchText.Remove(0, 4)),
                    StringComparison.OrdinalIgnoreCase)
                : searchText.StartsWith("/pn:")
                    ? PinYinHelper.ConvertToAllSpellWithCache(node.Name).Contains(
                        PinYinHelper.ConvertToAllSpellWithCache(searchText.Remove(0, 4)),
                        StringComparison.OrdinalIgnoreCase)
                    : node.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                      node.NodeId.ToString().Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }


    public bool IsRegionMeets(TunnelNodeViewModel vm)
        => IsRegionMeetsCore(vm, _selectedRegionString);

    private static bool IsRegionMeetsCore(TunnelNodeViewModel vm, string regionString)
    {
        return regionString == "all" || vm.Region == regionString;
    }
}