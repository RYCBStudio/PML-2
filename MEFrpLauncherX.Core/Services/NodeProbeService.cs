using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MEFrpLauncherX.Core.Services;

/// <summary>
///     探测结果状态
/// </summary>
public enum ProbeStatus
{
    /// <summary>探测成功</summary>
    Ok,

    /// <summary>连接超时</summary>
    Timeout,

    /// <summary>连接失败（拒绝/无法解析等）</summary>
    Error,

    /// <summary>不可探测（缺少 hostname 或端口非法）</summary>
    NotProbeable
}

/// <summary>
///     节点探测结果
/// </summary>
/// <param name="LatencyMs">延迟毫秒数，探测失败时为 null</param>
/// <param name="Status">探测状态</param>
/// <param name="MeasuredAt">探测时间</param>
public readonly record struct NodeProbeResult(long? LatencyMs, ProbeStatus Status, DateTimeOffset MeasuredAt);

/// <summary>
///     节点连通性/延迟探测服务（TCP Connect）
/// </summary>
public interface INodeProbeService
{
    /// <summary>
    ///     探测指定 hostname:port 的 TCP 连通性与延迟。
    /// </summary>
    /// <param name="hostname">目标主机名或 IP</param>
    /// <param name="port">目标端口</param>
    /// <param name="ct">页面级批量取消令牌；单个探测内部使用独立超时</param>
    Task<NodeProbeResult> ProbeAsync(string hostname, int port, CancellationToken ct = default);
}

/// <summary>
///     基于 TCP Connect 的探测实现。
///     单次探测超时 4s（3–5s 区间），全局并发上限 6（5–8 区间）。
///     不引入任何 UI 依赖。
/// </summary>
public sealed class NodeProbeService : INodeProbeService
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(4);

    // 并发闸门：最多同时 6 个探测
    private readonly SemaphoreSlim _gate = new(6, 6);

    public async Task<NodeProbeResult> ProbeAsync(string hostname, int port, CancellationToken ct = default)
    {
        var measuredAt = DateTimeOffset.Now;

        // 缺少 hostname 或端口非法 → 不可探测，不发起网络请求
        if (string.IsNullOrWhiteSpace(hostname) || port is <= 0 or > 65535)
        {
            return new NodeProbeResult(null, ProbeStatus.NotProbeable, measuredAt);
        }

        await _gate.WaitAsync(ct);
        try
        {
            // 每个节点独立超时；页面级取消通过 ct 联动
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(ProbeTimeout);
            try
            {
                using var pingSender = new Ping();
                var res = await pingSender.SendPingAsync(hostname, ProbeTimeout, cancellationToken: timeoutCts.Token);
                return new NodeProbeResult(res.RoundtripTime, ProbeStatus.Ok, DateTimeOffset.Now);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // 页面级取消：向上传递，不当作失败
                throw;
            }
            catch (OperationCanceledException)
            {
                // 单节点超时
                return new NodeProbeResult(null, ProbeStatus.Timeout, DateTimeOffset.Now);
            }
            catch (SocketException)
            {
                // 连接被拒 / DNS 失败 / 网络不可达等
                return new NodeProbeResult(null, ProbeStatus.Error, DateTimeOffset.Now);
            }
            catch
            {
                // 兜底：任何其他异常视为探测失败
                return new NodeProbeResult(null, ProbeStatus.Error, DateTimeOffset.Now);
            }
        }
        finally
        {
            _gate.Release();
        }
    }
}