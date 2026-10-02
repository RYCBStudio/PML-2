using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Collections;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Core.MEFIntegrated;
using MEFrpLauncherX.Core.Services;
using MEFrpLauncherX.ViewModels.Controls;
using ReactiveUI;

namespace MEFrpLauncherX.ViewModels;

/// <summary>「迁移隧道」对话框的候选节点排序方式。</summary>
public enum TunnelMigrateSortMode
{
    /// <summary>推荐：在线优先 → 未过载优先 → 负载升序</summary>
    Recommended,

    /// <summary>延迟升序（需先探测延迟，未探测的出在最后）</summary>
    Latency,

    /// <summary>负载升序</summary>
    Load,

    /// <summary>名称（拼音）升序</summary>
    Name
}

/// <summary>
///     「迁移隧道」对话框的 ViewModel（26.4）。
///     复用创建隧道页的节点数据源（<see cref="MEFrpApiConverter.EnsureNodesListInfoAsync" />）与节点模型
///     （<see cref="TunnelNodeViewModel" />），并复用 <see cref="NodesContainerViewModel" /> 的搜索判定
///     （含 <c>/d:</c>、<c>/pn:</c> 等拼音语法），在其之上提供排序、候选筛选与按需延迟探测。
///     本类不发起任何隧道删除/创建请求，仅产出用户选中的目标节点。
/// </summary>
public partial class TunnelMigrateViewModel : ViewModelBase
{
    // 复用创建隧道页的节点加载/过滤/搜索逻辑（不再重复实现节点映射）
    private readonly NodesContainerViewModel _nodeLoader = new();
    private readonly string _sourceProxyType;

    private CancellationTokenSource? _probeCts;
    private string _searchText = string.Empty;
    private TunnelMigrateSortMode _sortMode = TunnelMigrateSortMode.Recommended;

    public TunnelMigrateViewModel(string proxyName, int sourceNodeId, string sourceNodeName, string sourceProxyType)
    {
        ProxyName = proxyName ?? string.Empty;
        SourceNodeId = sourceNodeId;
        _sourceProxyType = sourceProxyType ?? string.Empty;
        SourceNodeText = string.Format(Languages.Text_UserProxy_MigrateSourceNodeFormat, sourceNodeId,
            sourceNodeName ?? string.Empty);
    }

    /// <summary>待迁移隧道名（仅用于展示）</summary>
    public string ProxyName { get; }

    /// <summary>原节点 ID</summary>
    public int SourceNodeId { get; }

    /// <summary>原节点展示文案（#ID 名称）</summary>
    public string SourceNodeText { get; }

    /// <summary>候选节点集合（已过滤 + 已排序）</summary>
    public AvaloniaList<TunnelNodeViewModel> FilteredNodes { get; } = [];

    /// <summary>排序方式下拉项（顺序与 <see cref="TunnelMigrateSortMode" /> 一致）</summary>
    public List<string> SortOptions { get; } =
    [
        Languages.Text_UserProxy_MigrateSortRecommended,
        Languages.Text_UserProxy_MigrateSortLatency,
        Languages.Text_UserProxy_MigrateSortLoad,
        Languages.Text_UserProxy_MigrateSortName
    ];

    /// <summary>名称/拼音搜索关键字（语法与创建隧道页节点搜索一致）</summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            this.RaiseAndSetIfChanged(ref _searchText, value);
            ApplyFilter();
        }
    }

    /// <summary>排序方式（供 ComboBox SelectedIndex 绑定）</summary>
    public int SortModeIndex
    {
        get => (int)_sortMode;
        set
        {
            var mode = (TunnelMigrateSortMode)Math.Clamp(value, 0, 3);
            if (mode == _sortMode)
            {
                return;
            }

            _sortMode = mode;
            this.RaisePropertyChanged();
            ApplyFilter();
        }
    }

    public bool IsLoading
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>是否正在探测节点延迟</summary>
    public bool IsProbing
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public TunnelNodeViewModel? SelectedNode
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>是否存在候选节点（空态提示用）</summary>
    public bool HasCandidates
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>状态提示（候选数量 / 空态原因）</summary>
    public string StatusHint
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    /// <summary>
    ///     加载候选节点：优先复用节点列表/状态的 5 分钟缓存，避免仅打开对话框就产生额外流量。
    /// </summary>
    public async Task LoadNodesAsync()
    {
        try
        {
            IsLoading = true;
            StatusHint = string.Empty;

            var listInfo = await MEFrpApiConverter.EnsureNodesListInfoAsync();
            if (listInfo?.NodesList is null)
            {
                FilteredNodes.Clear();
                HasCandidates = false;
                StatusHint = Languages.Text_UserProxy_MigrateNoNodes;
                return;
            }

            var statusInfo = await MEFrpApiConverter.EnsureNodesStatusInfoAsync()
                             ?? new InfoClasses.NodesStatusInfo { NodesStatus = [] };

            await _nodeLoader.LoadNodesAsync(listInfo, statusInfo);
            ApplyFilter();
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger?.Error(ex, "加载迁移候选节点失败");
            FilteredNodes.Clear();
            HasCandidates = false;
            StatusHint = Languages.Text_UserProxy_MigrateNoNodes;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    ///     按需探测当前候选节点的延迟（Ping），用于「按延迟排序」。
    ///     仅在用户显式点击时执行，不影响候选列表的既有加载流程。
    /// </summary>
    public async Task ProbeLatencyAsync()
    {
        if (IsProbing)
        {
            return;
        }

        IsProbing = true;
        _probeCts?.Cancel();
        _probeCts?.Dispose();
        _probeCts = new CancellationTokenSource();
        var ct = _probeCts.Token;
        try
        {
            var targets = FilteredNodes.ToList();
            var probes = targets.Select(async node =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(node.Hostname) || node.ServicePort <= 0)
                    {
                        node.LatencyMs = null;
                        return;
                    }

                    var result = await Core.App.NodeProbeService.ProbeAsync(node.Hostname, node.ServicePort, ct);
                    node.LatencyMs = result.Status == ProbeStatus.Ok ? result.LatencyMs : null;
                }
                catch (OperationCanceledException)
                {
                    // 对话框关闭：忽略
                }
                catch (Exception ex)
                {
                    Core.App.CurrentLogger?.Error(ex, "探测节点延迟失败");
                }
            });
            await Task.WhenAll(probes);
            ApplyFilter();
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger?.Error(ex, "探测节点延迟失败");
        }
        finally
        {
            IsProbing = false;
        }
    }

    /// <summary>取消进行中的延迟探测（对话框关闭时调用，避免后台请求悬挂）</summary>
    public void CancelProbe()
    {
        try
        {
            _probeCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // 已释放：忽略
        }
    }

    /// <summary>
    ///     过滤 + 排序候选节点：
    ///     排除原节点与不支持原隧道协议的节点，复用创建隧道页的搜索判定（含拼音语法）。
    /// </summary>
    private void ApplyFilter()
    {
        try
        {
            // 复用创建页搜索判定：需要把关键字同步给复用的节点容器
            _nodeLoader.SearchText = _searchText;

            var candidates = _nodeLoader.AllNodes
                .Where(node => node.NodeId != SourceNodeId)
                .Where(SupportsSourceProtocol)
                .Where(_nodeLoader.MeetsSearchCriteria);

            FilteredNodes.Clear();
            FilteredNodes.AddRange(Order(candidates));

            HasCandidates = FilteredNodes.Count > 0;
            StatusHint = HasCandidates
                ? string.Format(Languages.Text_UserProxy_MigrateNodeCountFormat, FilteredNodes.Count)
                : Languages.Text_UserProxy_MigrateNoMatchingNode;
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger?.Error(ex, "筛选迁移候选节点失败");
        }
    }

    private IEnumerable<TunnelNodeViewModel> Order(IEnumerable<TunnelNodeViewModel> source) => _sortMode switch
    {
        TunnelMigrateSortMode.Latency => source
            .OrderBy(node => node.LatencyMs ?? long.MaxValue)
            .ThenBy(node => node.LoadPercent),
        TunnelMigrateSortMode.Load => source
            .OrderBy(node => node.LoadPercent),
        TunnelMigrateSortMode.Name => source
            .OrderBy(node => PinYinHelper.ConvertToAllSpellWithCache(node.Name ?? string.Empty),
                StringComparer.OrdinalIgnoreCase),
        _ => source
            .OrderByDescending(node => node.IsOnline)
            .ThenByDescending(node => node.IsNotOverloaded)
            .ThenBy(node => node.LoadPercent)
    };

    /// <summary>节点是否支持原隧道协议（原隧道协议缺失时不限制）</summary>
    private bool SupportsSourceProtocol(TunnelNodeViewModel node)
    {
        if (string.IsNullOrWhiteSpace(_sourceProxyType))
        {
            return true;
        }

        return node.AllowTypes?.Any(type =>
            string.Equals(type, _sourceProxyType, StringComparison.OrdinalIgnoreCase)) ?? false;
    }
}
