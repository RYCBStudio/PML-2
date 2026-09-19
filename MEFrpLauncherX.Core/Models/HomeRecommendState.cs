using System.Text.Json;
using MEFrpLauncherX.Core.Services;

namespace MEFrpLauncherX.Core.Models;

/// <summary>
///     精简主页推荐的本地状态（26.4）。
///     独立存放于 <c>Cache/home-recommend.json</c>，<b>不写入用户配置</b>（避免污染 Settings.json）。
///     失败信息带时间戳并设有效期，避免过期状态长期影响推荐。
/// </summary>
public class HomeRecommendState
{
    /// <summary>被用户「暂时忽略」的推荐种类（<see cref="HomeRecommendKind" /> 名称）</summary>
    public List<string> Dismissed { get; set; } = [];

    /// <summary>最近一次隧道失败时间（UTC），长期未失败则不再提示</summary>
    public DateTimeOffset? LastFailAt { get; set; }

    /// <summary>最近一次失败的隧道名</summary>
    public string? LastFailedProxyName { get; set; }

    /// <summary>最近一次使用的隧道 ID（用于「启动最近使用的一条」类推荐）</summary>
    public int LastProxyId { get; set; } = -1;

    /// <summary>
    ///     最近启动过的隧道记录（26.4）：隧道 ID → 最近一次启动时间（UTC）。
    ///     用于主页「启动最近启动的隧道」推荐，本地记录保证「刚启动」立刻生效。
    /// </summary>
    public Dictionary<int, DateTimeOffset> RecentLaunches { get; set; } = [];

    /// <summary>
    ///     本地启动记录的有效期：超过该时长且服务端 lastStartTime 也为空/更旧时，不再作为推荐理由。
    /// </summary>
    public static readonly TimeSpan RecentLaunchFreshness = TimeSpan.FromDays(30);

    /// <summary>本地最多保留的最近启动记录条数</summary>
    public const int MaxRecentLaunches = 20;

    /// <summary>失败信息有效期：超过该时长不再作为推荐理由</summary>
    public static readonly TimeSpan FailFreshness = TimeSpan.FromHours(24);
}

/// <summary>
///     <see cref="HomeRecommendState" /> 的读写与失效判断（AOT 安全：走源生成上下文）。
/// </summary>
public static class HomeRecommendStateStore
{
    private static string StatePath
    {
        get;
    } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Cache", "home-recommend.json");

    /// <summary>读取状态；文件缺失或损坏时返回空状态（不抛出）。</summary>
    public static HomeRecommendState Load()
    {
        try
        {
            if (!File.Exists(StatePath))
            {
                return new HomeRecommendState();
            }

            var json = File.ReadAllText(StatePath);
            return JsonSerializer.Deserialize<HomeRecommendState>(json, App.AppJsonSerializerContext.HomeRecommendState)
                   ?? new HomeRecommendState();
        }
        catch (Exception ex)
        {
            App.CurrentLogger?.Error(ex, "读取主页推荐状态失败");
            return new HomeRecommendState();
        }
    }

    /// <summary>保存状态；失败仅记日志（不影响主页可用性）。</summary>
    public static void Save(HomeRecommendState state)
    {
        try
        {
            var dir = Path.GetDirectoryName(StatePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(state, App.AppJsonSerializerContext.HomeRecommendState);
            File.WriteAllText(StatePath, json);
        }
        catch (Exception ex)
        {
            App.CurrentLogger?.Error(ex, "保存主页推荐状态失败");
        }
    }

    /// <summary>把种类标记为「暂时忽略」并立即落盘。</summary>
    public static void Dismiss(HomeRecommendKind kind)
    {
        var state = Load();
        var name = kind.ToString();
        if (!state.Dismissed.Contains(name))
        {
            state.Dismissed.Add(name);
            Save(state);
        }
    }

    /// <summary>清除全部忽略记录。</summary>
    public static void ClearDismissed()
    {
        var state = Load();
        state.Dismissed.Clear();
        Save(state);
    }

    /// <summary>读取当前有效的忽略集合；无法解析的名称被忽略。</summary>
    public static IReadOnlyCollection<HomeRecommendKind> LoadDismissedKinds()
    {
        var result = new List<HomeRecommendKind>();
        foreach (var name in Load().Dismissed)
        {
            if (Enum.TryParse<HomeRecommendKind>(name, true, out var kind))
            {
                result.Add(kind);
            }
        }

        return result;
    }

    /// <summary>记录一次隧道失败（供主页推荐判断，超过有效期自动失效）。</summary>
    public static void RecordFailure(string proxyName)
    {
        var state = Load();
        state.LastFailedProxyName = proxyName;
        state.LastFailAt = DateTimeOffset.UtcNow;
        Save(state);
    }

    /// <summary>清除失败记录（隧道成功启动/停止时调用）。</summary>
    public static void ClearFailure()
    {
        var state = Load();
        if (state.LastFailAt is null && state.LastFailedProxyName is null)
        {
            return;
        }

        state.LastFailAt = null;
        state.LastFailedProxyName = null;
        Save(state);
    }

    /// <summary>
    ///     记录一次隧道启动（26.4）：写入本地最近启动时间，供主页「启动最近启动的隧道」推荐使用。
    ///     本地记录的作用是让「刚刚启动」立即反映到推荐里，无需等待服务端 <c>lastStartTime</c> 更新。
    /// </summary>
    /// <param name="proxyId">隧道 ID</param>
    public static void RecordLaunch(int proxyId)
    {
        if (proxyId <= 0)
        {
            return;
        }

        var state = Load();
        state.LastProxyId = proxyId;
        state.RecentLaunches[proxyId] = DateTimeOffset.UtcNow;

        // 超出上限时丢弃最旧的记录，避免状态文件无限增长
        if (state.RecentLaunches.Count > HomeRecommendState.MaxRecentLaunches)
        {
            foreach (var stale in state.RecentLaunches
                         .OrderBy(pair => pair.Value)
                         .Take(state.RecentLaunches.Count - HomeRecommendState.MaxRecentLaunches)
                         .Select(pair => pair.Key)
                         .ToList())
            {
                state.RecentLaunches.Remove(stale);
            }
        }

        Save(state);
    }

    /// <summary>读取本地最近启动时间；无记录或已超过有效期时返回 null。</summary>
    public static DateTimeOffset? GetLastLaunchAt(int proxyId)
    {
        if (proxyId <= 0)
        {
            return null;
        }

        var state = Load();
        if (!state.RecentLaunches.TryGetValue(proxyId, out var at))
        {
            return null;
        }

        return DateTimeOffset.UtcNow - at <= HomeRecommendState.RecentLaunchFreshness ? at : null;
    }

    /// <summary>读取全部有效的本地最近启动记录（按时间倒序）。</summary>
    public static IReadOnlyList<(int ProxyId, DateTimeOffset At)> GetRecentLaunches()
    {
        var state = Load();
        var now = DateTimeOffset.UtcNow;
        return state.RecentLaunches
            .Where(pair => now - pair.Value <= HomeRecommendState.RecentLaunchFreshness)
            .OrderByDescending(pair => pair.Value)
            .Select(pair => (pair.Key, pair.Value))
            .ToList();
    }

    /// <summary>移除已被删除的隧道的本地启动记录（管理页删除隧道后调用）。</summary>
    public static void ForgetLaunch(int proxyId)
    {
        var state = Load();
        if (!state.RecentLaunches.Remove(proxyId))
        {
            return;
        }

        if (state.LastProxyId == proxyId)
        {
            state.LastProxyId = -1;
        }

        Save(state);
    }

    /// <summary>返回仍然「新鲜」的失败隧道名；无有效失败时返回 null。</summary>
    public static string? GetFreshFailedProxyName()
    {
        var state = Load();
        if (string.IsNullOrWhiteSpace(state.LastFailedProxyName) || state.LastFailAt is null)
        {
            return null;
        }

        return DateTimeOffset.UtcNow - state.LastFailAt.Value <= HomeRecommendState.FailFreshness
            ? state.LastFailedProxyName
            : null;
    }
}
