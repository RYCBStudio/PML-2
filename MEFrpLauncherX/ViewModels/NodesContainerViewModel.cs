using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using MEFrpLauncherX.Controls;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.MEFIntergrated;
using ReactiveUI;

namespace MEFrpLauncherX.ViewModels;

public class NodesContainerViewModel : ViewModelBase, INotifyPropertyChanged
{
    private const int DEBOUNCE_DELAY_MS = 300;
    private readonly DispatcherTimer _debounceTimer;
    private bool _isLoading;
    private DispatcherTimer _loadingDebounceTimer;

// 在构造函数中初始化
    public NodesContainerViewModel()
    {
        // 初始化防抖定时器
        _debounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(DEBOUNCE_DELAY_MS)
        };
        _debounceTimer.Tick += (s, e) =>
        {
            _debounceTimer.Stop();
            FilterNodes();
        };
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                OnPropertyChanged();

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
                    _loadingDebounceTimer.Tick += (s, e) =>
                    {
                        _loadingDebounceTimer.Stop();
                        UpdateLoadingUI(false);
                    };
                    _loadingDebounceTimer.Start();
                }
            }
        }
    }

    public AvaloniaList<TunnelNodeViewModel> AllNodes
    {
        get;
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

    public object SelectedRegion
    {
        get;
        set
        {
            if (field != value)
            {
                this.RaiseAndSetIfChanged(ref field, value);
                TriggerFilterWithDebounce();
                // field = value;
                // OnPropertyChanged();
                //FilterNodes();
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

    public TunnelNodeViewModel SelectedNode
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

    public event PropertyChangedEventHandler PropertyChanged;

// 添加防抖触发方法
    private void TriggerFilterWithDebounce()
    {
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private void UpdateLoadingUI(bool isLoading)
    {
        // 分别更新各个loading状态
        OnPropertyChanged(nameof(IsLoading));
    }

    public async Task LoadNodesAsync(InfoClasses.NodesListInfo listInfo, InfoClasses.NodesStatusInfo statusInfo)
    {
        try
        {
            IsLoading = true;
            AllNodes.Clear();
            FilteredNodes.Clear(); // 提前清空

            await Task.Run(async () =>
            {
                var s = statusInfo.NodesStatus;
                if (s is null || s.Length < 1)
                {
                    s = (await MEpiConverter.GetNodesStatusAsync()).data;
                }

                foreach (var node in listInfo.NodesList)
                {
                    var status = s?.FirstOrDefault(s => s.nodeId == node.nodeId);
                    var vm = new TunnelNodeViewModel
                    {
                        NodeId = node.nodeId,
                        Name = node.name,
                        Description = node.description,
                        AllowTypes = node.allowType?.Split(';')
                            ?.Select(type => type.ToUpper())
                            ?.ToArray() ?? [],
                        Bandwidth = node.bandwidth,
                        LoadPercent = status?.loadPercent ?? 0,
                        IsOnline = status?.isOnline ?? false,
                        AllowPorts = node.allowPort,
                        CanBuildSite = (node.allowType?.Split(';')
                            ?.Select(type => type.ToUpper())
                            ?.ToArray() ?? []).Any(s =>
                            s.Equals("http", StringComparison.OrdinalIgnoreCase) ||
                            s.Equals("https", StringComparison.OrdinalIgnoreCase)),
                        Region = node.region,
                        AllowGroup = node.allowGroup.Split(';')
                    };
                    vm.AllowHighTraffic = CalculateAllowHighTraffic(vm.Bandwidth);
                    AllNodes.Add(vm);
                }
            });
            await Dispatcher.UIThread.InvokeAsync(() => FilterNodes(true), DispatcherPriority.Background);
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

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public event Action<TunnelNodeViewModel> NodeSelected;

    private async void FilterNodes(bool force = false)
    {
        if (IsLoading && !force)
        {
            return;
        }

        IsLoading = true;
        try
        {
            await Task.Run(async () =>
            {
                var realRegion = "all";
                var filteredNodes = AllNodes.Where(node =>
                        MeetsSearchCriteria(node) &&
                        IsRegionMeets(node, out realRegion) &&
                        (!FilterCanBuildSite || node.CanBuildSite) &&
                        (!FilterAllowHighTraffic || node.AllowHighTraffic) &&
                        (!FilterNotOverLoaded || node.IsNotOverloaded))
                    .ToList();

                // 在UI线程上更新集合
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // 一次性替换整个集合并暂停通知
                    FilteredNodes.Clear();
                    foreach (var node in filteredNodes)
                    {
                        FilteredNodes.Add(node);
                    }
                }, DispatcherPriority.Background);

                Core.App.CurrentLogger.Log($"{FilteredNodes.Count} nodes added.", EnumLogType.Debug);
            });
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger.Error(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

// 提取搜索逻辑到单独方法
    private bool MeetsSearchCriteria(TunnelNodeViewModel node)
    {
        if (string.IsNullOrEmpty(SearchText))
        {
            return true;
        }

        return SearchText.StartsWith("/d:")
            ? node.Description.Contains(SearchText.Remove(0, 3), StringComparison.OrdinalIgnoreCase)
            : SearchText.StartsWith("/pd:")
                ? PinYinHelper.ConvertToAllSpellWithCache(node.Description).Contains(
                    PinYinHelper.ConvertToAllSpellWithCache(SearchText.Remove(0, 4)),
                    StringComparison.OrdinalIgnoreCase)
                : SearchText.StartsWith("/pn:")
                    ? PinYinHelper.ConvertToAllSpellWithCache(node.Name).Contains(
                        PinYinHelper.ConvertToAllSpellWithCache(SearchText.Remove(0, 4)),
                        StringComparison.OrdinalIgnoreCase)
                    : node.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                      node.NodeId.ToString().Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }


    private bool IsRegionMeets(TunnelNodeViewModel vm, out string region)
    {
        var _tmpRegion = "all";
        if (SelectedRegion is TabStripItem item)
        {
            Dispatcher.UIThread.Invoke(() =>
                _tmpRegion = item.Tag.ToString());
        }
        else
        {
            _tmpRegion = SelectedRegion.ToString();
        }

        region = _tmpRegion;
        return _tmpRegion == "all" || vm.Region == _tmpRegion;
    }
}