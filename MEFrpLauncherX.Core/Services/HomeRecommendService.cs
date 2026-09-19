using MEFrpLauncherX.Core.Languages;

namespace MEFrpLauncherX.Core.Services;

/// <summary>
///     精简主页推荐条目的种类。
///     每种对应一条固定的优先级规则（见 <see cref="HomeRecommendService" />）。
/// </summary>
public enum HomeRecommendKind
{
    /// <summary>存在启动失败的隧道</summary>
    FailedTunnel,

    /// <summary>账户流量已超限</summary>
    TrafficExceeded,

    /// <summary>剩余流量偏低</summary>
    TrafficLow,

    /// <summary>有隧道但当前没有任何一条在运行</summary>
    StartAnyTunnel,

    /// <summary>尚无隧道</summary>
    CreateTunnel,

    /// <summary>检测到可用更新</summary>
    UpdateAvailable,

    /// <summary>兜底：查看节点状态</summary>
    ExploreNodes,

    /// <summary>兜底：阅读文档</summary>
    ReadDocs
}

/// <summary>
///     推荐条目点击后的去向（由 UI 层解释执行，Core 不依赖任何 UI 类型）。
/// </summary>
public enum HomeRecommendAction
{
    /// <summary>前往隧道管理页</summary>
    Manage,

    /// <summary>前往用户中心</summary>
    UserCenter,

    /// <summary>前往创建隧道页</summary>
    Create,

    /// <summary>前往更新页</summary>
    Update,

    /// <summary>前往节点监控页</summary>
    Nodes,

    /// <summary>打开官方文档</summary>
    Docs
}

/// <summary>
///     推荐条目的输入上下文。由主页 ViewModel 组装，服务本身不做任何 IO。
/// </summary>
public sealed record HomeRecommendContext
{
    /// <summary>用户是否已登录</summary>
    public bool IsLoggedIn { get; init; }

    /// <summary>隧道总数</summary>
    public int TunnelCount { get; init; }

    /// <summary>运行中的隧道数</summary>
    public int RunningCount { get; init; }

    /// <summary>启动失败的隧道名（空表示没有失败）</summary>
    public string? FailedTunnelName { get; init; }

    /// <summary>账户状态：0-正常 1-封禁 2-流量超限（未登录时为 null）</summary>
    public int? AccountStatus { get; init; }

    /// <summary>剩余流量（字节，未登录或未知时为 null）</summary>
    public ulong? RemainingTrafficBytes { get; init; }

    /// <summary>剩余流量的可读文本（用于文案占位）</summary>
    public string? RemainingTrafficText { get; init; }

    /// <summary>可用更新（null 表示未知/未检查）</summary>
    public bool? HasUpdate { get; init; }

    /// <summary>可用更新的版本号</summary>
    public string? LatestVersion { get; init; }

    /// <summary>剩余流量低于该值（字节）时提示流量偏低；0 表示不启用该项</summary>
    public ulong LowTrafficThresholdBytes { get; init; }
}

/// <summary>
///     单条推荐：标题 + 可解释原因 + 主按钮文案与去向。
/// </summary>
/// <param name="Kind">规则种类（用于「暂时忽略」记录与去重）</param>
/// <param name="Title">标题文案</param>
/// <param name="Reason">原因文案（一句话，可解释）</param>
/// <param name="ActionText">主按钮文案</param>
/// <param name="Action">按钮去向</param>
public sealed record HomeRecommendation(
    HomeRecommendKind Kind,
    string Title,
    string Reason,
    string ActionText,
    HomeRecommendAction Action);

/// <summary>
///     精简主页的推荐规则引擎（26.4）。
///     纯规则实现（<b>不接入 AI</b>），按固定优先级有序生成，最多返回 <see cref="MaxItems" /> 条。
///     不依赖 UI 程序集，可在 Core 内独立测试与复用。
/// </summary>
public static class HomeRecommendService
{
    /// <summary>精简主页最多展示的推荐条数</summary>
    public const int MaxItems = 4;

    /// <summary>
    ///     按优先级生成推荐列表。
    ///     优先级：失败隧道 → 流量超限 → 流量偏低 → 有隧道但无运行 → 尚无隧道 → 有更新 → 兜底。
    /// </summary>
    /// <param name="ctx">主页提供的上下文</param>
    /// <param name="dismissed">被用户「暂时忽略」的规则种类</param>
    public static IReadOnlyList<HomeRecommendation> Build(
        HomeRecommendContext ctx,
        IReadOnlyCollection<HomeRecommendKind>? dismissed = null)
    {
        var ordered = new List<HomeRecommendation>();

        // 1. 存在近期失败的隧道 → 最高优先级，先让用户知道「挂了」
        if (!string.IsNullOrWhiteSpace(ctx.FailedTunnelName))
        {
            ordered.Add(new HomeRecommendation(
                HomeRecommendKind.FailedTunnel,
                Languages.Languages.Text_Home_Recommend_FailedTunnel_Title,
                string.Format(Languages.Languages.Text_Home_Recommend_FailedTunnel_Reason, ctx.FailedTunnelName),
                Languages.Languages.Text_Home_Recommend_Action_Manage,
                HomeRecommendAction.Manage));
        }

        // 2/3. 流量：超限（账户状态=2）优先于「偏低」
        if (ctx.AccountStatus == 2)
        {
            ordered.Add(new HomeRecommendation(
                HomeRecommendKind.TrafficExceeded,
                Languages.Languages.Text_Home_Recommend_TrafficExceeded_Title,
                Languages.Languages.Text_Home_Recommend_TrafficExceeded_Reason,
                Languages.Languages.Text_Home_Recommend_Action_UserCenter,
                HomeRecommendAction.UserCenter));
        }
        else if (ctx.LowTrafficThresholdBytes > 0 &&
                 ctx.RemainingTrafficBytes.HasValue &&
                 ctx.RemainingTrafficBytes.Value < ctx.LowTrafficThresholdBytes)
        {
            ordered.Add(new HomeRecommendation(
                HomeRecommendKind.TrafficLow,
                Languages.Languages.Text_Home_Recommend_TrafficLow_Title,
                string.Format(Languages.Languages.Text_Home_Recommend_TrafficLow_Reason,
                    ctx.RemainingTrafficText ?? "—"),
                Languages.Languages.Text_Home_Recommend_Action_UserCenter,
                HomeRecommendAction.UserCenter));
        }

        // 4. 有隧道但都没跑 → 提示启动
        if (ctx.TunnelCount > 0 && ctx.RunningCount == 0)
        {
            ordered.Add(new HomeRecommendation(
                HomeRecommendKind.StartAnyTunnel,
                Languages.Languages.Text_Home_Recommend_StartAnyTunnel_Title,
                string.Format(Languages.Languages.Text_Home_Recommend_StartAnyTunnel_Reason, ctx.TunnelCount),
                Languages.Languages.Text_Home_Recommend_Action_Manage,
                HomeRecommendAction.Manage));
        }

        // 5. 尚无隧道 → 引导创建
        if (ctx.TunnelCount == 0)
        {
            ordered.Add(new HomeRecommendation(
                HomeRecommendKind.CreateTunnel,
                Languages.Languages.Text_Home_Recommend_CreateTunnel_Title,
                Languages.Languages.Text_Home_Recommend_CreateTunnel_Reason,
                Languages.Languages.Text_Home_Recommend_Action_Create,
                HomeRecommendAction.Create));
        }

        // 6. 有可用更新
        if (ctx.HasUpdate == true)
        {
            ordered.Add(new HomeRecommendation(
                HomeRecommendKind.UpdateAvailable,
                Languages.Languages.Text_Home_Recommend_UpdateAvailable_Title,
                string.Format(Languages.Languages.Text_Home_Recommend_UpdateAvailable_Reason,
                    ctx.LatestVersion ?? string.Empty),
                Languages.Languages.Text_Home_Recommend_Action_Update,
                HomeRecommendAction.Update));
        }

        // 7. 兜底：节点监控 / 文档（仅在「没有更重要的推荐」或条目未满时出现，
        //    避免主页在无事可做时空白）
        ordered.Add(new HomeRecommendation(
            HomeRecommendKind.ExploreNodes,
            Languages.Languages.Text_Home_Recommend_ExploreNodes_Title,
            Languages.Languages.Text_Home_Recommend_ExploreNodes_Reason,
            Languages.Languages.Text_Home_Recommend_Action_Nodes,
            HomeRecommendAction.Nodes));

        ordered.Add(new HomeRecommendation(
            HomeRecommendKind.ReadDocs,
            Languages.Languages.Text_Home_Recommend_ReadDocs_Title,
            Languages.Languages.Text_Home_Recommend_ReadDocs_Reason,
            Languages.Languages.Text_Home_Recommend_Action_Docs,
            HomeRecommendAction.Docs));

        // 过滤用户已忽略的条目，再截断到上限
        var filter = dismissed is { Count: > 0 }
            ? ordered.Where(r => !dismissed.Contains(r.Kind))
            : ordered;

        return filter.Take(MaxItems).ToList();
    }
}
