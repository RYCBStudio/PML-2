using System;
using System.ComponentModel;
using System.Linq;
using Avalonia.Media;
using ReactiveUI;

namespace MEFrpLauncherX.ViewModels;

/// <summary>
///     精简主页「我的隧道」行模型（26.4）。
///     本质是对管理页 <see cref="UserProxyViewModel" /> 的只读投影：不复制任何启停/进程逻辑，
///     只把行内展示需要的字段暴露给精简主页，并转发源对象的属性变更，
///     使列表中的运行/失败状态与管理页保持同一数据源、实时一致（不开启第二套隧道状态）。
///     行内启停与复制仍由 <c>HomePageViewModel</c> 调用源对象既有命令，与
///     <c>LaunchRecentTunnel</c> 走同一条路径。
/// </summary>
public sealed class SimpleProxyItem : ReactiveObject, IDisposable
{
    /// <summary>源对象变更时需要同步通知的展示属性（一次性全部重抛，避免逐字段映射遗漏）。</summary>
    private static readonly string[] DependentProperties =
    [
        nameof(Name),
        nameof(Type),
        nameof(NodeName),
        nameof(NodeId),
        nameof(PublicAddress),
        nameof(IsRunning),
        nameof(HasFailed),
        nameof(CanStart),
        nameof(CanStop),
        nameof(StatusText),
        nameof(StatusBrush),
        nameof(FailureSummary),
        nameof(HasFailureSummary),
        nameof(IsDisabled),
        nameof(IsBanned)
    ];

    private readonly UserProxyViewModel _source;

    public SimpleProxyItem(UserProxyViewModel source)
    {
        _source = source;
        _source.PropertyChanged += OnSourcePropertyChanged;
    }

    /// <summary>管理页中的原始隧道对象（启停/复制复用其既有命令，保证与管理页同一路径）。</summary>
    public UserProxyViewModel Source => _source;

    /// <summary>隧道 ID（行内按钮回传用）</summary>
    public int ProxyId => _source.proxyId;

    /// <summary>隧道名称</summary>
    public string Name => _source.proxyName;

    /// <summary>协议展示文本（管理页以大写存储，这里保持原样）</summary>
    public string Type => string.IsNullOrWhiteSpace(_source.proxyType)
        ? string.Empty
        : _source.proxyType.ToUpperInvariant();

    /// <summary>节点名（节点缺失时为管理页写入的占位文案）</summary>
    public string NodeName => _source.node ?? string.Empty;

    /// <summary>节点 ID（与节点名一起展示为「#id 名称」）</summary>
    public int NodeId => _source.nodeId;

    /// <summary>
    ///     可复制/可访问地址：HTTP/HTTPS 取首个域名，其余协议取「节点主机:远程端口」。
    ///     为 null 或空串表示当前没有可用地址（例如域名尚未分配）。
    /// </summary>
    public string? PublicAddress => ResolvePublicAddress(_source);

    /// <summary>
    ///     按协议推导可复制地址（与管理页「复制隧道信息」同一规则）：
    ///     HTTP/HTTPS → 域名；TCP/UDP → 节点主机 + 远程端口。
    /// </summary>
    public static string? ResolvePublicAddress(UserProxyViewModel? source)
    {
        if (source is null)
        {
            return null;
        }

        if (IsWebProxy(source.proxyType))
        {
            var domain = source.Domains.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d));
            return string.IsNullOrWhiteSpace(domain) ? null : domain;
        }

        return string.IsNullOrWhiteSpace(source.location) ? null : source.location;
    }

    private static bool IsWebProxy(string? proxyType) =>
        string.Equals(proxyType, "http", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(proxyType, "https", StringComparison.OrdinalIgnoreCase);

    /// <summary>是否处于「占用中」状态（启动中/运行中/重连中），行内按钮据此在启动与停止间切换</summary>
    public bool IsRunning => _source.TunnelStatus is TunnelStatus.Running or TunnelStatus.Starting or
        TunnelStatus.Reconnecting;

    /// <summary>是否处于失败终态</summary>
    public bool HasFailed => _source.TunnelStatus == TunnelStatus.Failed;

    /// <summary>
    ///     是否可启动（与管理页「启动隧道」按钮同一判据：非占用状态即可启动）。
    ///     禁用/封禁不在此拦截，由服务端拒绝并走既有失败提示链路。
    /// </summary>
    public bool CanStart => !IsRunning;

    /// <summary>是否可停止（占用中）</summary>
    public bool CanStop => IsRunning;

    /// <summary>状态文案（与管理页一致；空闲态为空串，不展示）</summary>
    public string StatusText => _source.StatusText;

    /// <summary>状态颜色（与管理页一致）</summary>
    public IBrush StatusBrush => _source.StatusBrush;

    /// <summary>失败原因摘要（非失败态为 null）</summary>
    public string? FailureSummary => _source.HasError ? _source.LastErrorSummary : null;

    /// <summary>是否存在可展示的失败原因</summary>
    public bool HasFailureSummary => _source.HasError;

    /// <summary>是否已在服务端禁用</summary>
    public bool IsDisabled => _source.isDisabled;

    /// <summary>是否被封禁（行内以提示文案呈现）</summary>
    public bool IsBanned => _source.isBanned;

    private void OnSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        foreach (var propertyName in DependentProperties)
        {
            this.RaisePropertyChanged(propertyName);
        }
    }

    private bool _disposed;

    /// <summary>
    ///     退订源对象（列表刷新重建行时调用），避免投影对象长期持有管理页隧道对象的事件订阅。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _source.PropertyChanged -= OnSourcePropertyChanged;
    }
}
