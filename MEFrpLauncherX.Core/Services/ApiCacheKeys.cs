namespace MEFrpLauncherX.Core.Services;

/// <summary>
///     统一缓存的逻辑键（26.4）。命名与后端接口路径保持一致，便于日志与排障时对照。
///     <para>
///         仅「页面展示类」数据进入缓存；一次性动作（快速启动 token、启动配置、空闲端口申请、
///         人机校验）与所有写操作不缓存，避免影响功能正确性。
///     </para>
/// </summary>
public static class ApiCacheKeys
{
    /// <summary>系统状态</summary>
    public const string SystemStatus = "auth/system/status";

    /// <summary>重要公告（弹窗）</summary>
    public const string PopupNotice = "auth/popupNotice";

    /// <summary>站内公告</summary>
    public const string Notice = "auth/notice";

    /// <summary>平台公共统计</summary>
    public const string PublicInfo = "public/statistics";

    /// <summary>当前用户信息（含剩余流量与账户状态）</summary>
    public const string UserInfo = "auth/user/info";

    /// <summary>节点在线状态</summary>
    public const string NodesStatus = "auth/node/status";

    /// <summary>节点列表</summary>
    public const string NodesInfo = "auth/node/list";

    /// <summary>已创建隧道涉及的节点连接地址</summary>
    public const string NodesNameList = "auth/node/nameList";

    /// <summary>用户隧道列表</summary>
    public const string ProxyList = "auth/proxy/list";

    /// <summary>用户备案域名列表</summary>
    public const string IcpDomainList = "auth/user/icpDomain/list";

    /// <summary>流量统计（需拼接周期，如 <c>auth/user/trafficStats:7</c>）</summary>
    public const string TrafficStatsPrefix = "auth/user/trafficStats:";

    /// <summary>软件最新正式版本</summary>
    public const string LatestVersion = "changelog/latest";

    /// <summary>软件最新预览版本</summary>
    public const string LatestPreviewVersion = "changelog/preview/latest";

    /// <summary>软件公告列表</summary>
    public const string SoftwareNotice = "notice";

    /// <summary>隧道错误信息全量列表</summary>
    public const string TunnelErrorShell = "tpca/errors";

    /// <summary>单条隧道错误信息（需拼接 flag，如 <c>tpca/errors/E1001</c>）</summary>
    public const string TunnelErrorPrefix = "tpca/errors/";
}
