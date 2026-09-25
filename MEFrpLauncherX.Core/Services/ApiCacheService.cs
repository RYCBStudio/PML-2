using System.Collections.Concurrent;

namespace MEFrpLauncherX.Core.Services;

/// <summary>
///     统一的 API 数据缓存（26.4）。
///     <para>
///         策略：页面数据首次请求成功后写入缓存；<see cref="Ttl" />（默认 5 分钟）内再次访问同一数据
///         直接命中缓存、<b>不发起网络请求</b>；仅当距上次真实请求超过 <see cref="Ttl" /> 时才重新请求并覆盖缓存。
///     </para>
///     <para>
///         TTL 自「上次成功请求时刻」起算（<b>不滑动续期</b>），因此页面在 TTL 内反复进出不会无限推迟刷新。
///     </para>
///     <para>
///         一致性保证：只缓存成功（<c>code == 200</c>）的响应<b>原文</b>，失败结果不缓存；
///         命中后重新反序列化，调用方每次都拿到独立对象实例，不会共享可变状态。
///         写操作（新建/更新/删除隧道、签到、ICP 域名增删等）后由 API 层精确失效相关键。
///     </para>
///     <para>
///         作用域：缓存键带当前登录用户前缀，切换账号不会串数据；退出登录时整体清空。
///         本缓存为进程内缓存，不落盘。
///     </para>
/// </summary>
public static class ApiCacheService
{
    /// <summary>
    ///     缓存有效期，自上次成功请求时刻起算（非滑动）。默认 5 分钟。
    ///     修改该值主要用于自检 / 排障，运行期请保持默认。
    /// </summary>
    public static TimeSpan Ttl { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     当前用户作用域提供者。由 API 层注入（读取只读内存字段，避免构造缓存键时触发磁盘 IO）。
    ///     返回 null / 空表示未登录，统一归入 <c>anonymous</c> 作用域。
    /// </summary>
    public static Func<string?>? UserScopeProvider { get; set; }

    /// <summary>失效保护上限：条目数超过该值且存在过期项时，写入前先行清理</summary>
    private const int SoftLimit = 128;

    private static readonly ConcurrentDictionary<string, CacheEntry> Entries = new(StringComparer.Ordinal);

    /// <summary>当前作用域名（未登录为 <c>anonymous</c>）</summary>
    public static string Scope
    {
        get
        {
            try
            {
                var scope = UserScopeProvider?.Invoke();
                return string.IsNullOrWhiteSpace(scope) ? "anonymous" : scope;
            }
            catch
            {
                return "anonymous";
            }
        }
    }

    /// <summary>当前缓存的条目数（含已过期但尚未清理的条目，供自检使用）</summary>
    public static int Count => Entries.Count;

    /// <summary>
    ///     尝试读取仍然有效的缓存内容。
    ///     已过期条目会被移除并返回 <c>false</c>，从而触发调用方重新请求。
    /// </summary>
    /// <param name="key">逻辑键（见 <see cref="ApiCacheKeys" />）</param>
    /// <param name="content">命中的响应原文</param>
    public static bool TryGetContent(string key, out string content)
    {
        content = string.Empty;
        var fullKey = BuildKey(key);
        if (!Entries.TryGetValue(fullKey, out var entry))
        {
            return false;
        }

        if (entry.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            // 过期：移除后交由调用方重新请求（保证「超过 5 分钟才重新请求 API」）
            Entries.TryRemove(fullKey, out _);
            return false;
        }

        content = entry.Content;
        return true;
    }

    /// <summary>写入（或覆盖）缓存内容，并把本次请求时刻记为起点。</summary>
    public static void SetContent(string key, string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return;
        }

        PurgeExpiredIfNeeded();
        var now = DateTimeOffset.UtcNow;
        Entries[BuildKey(key)] = new CacheEntry(content, now + Ttl, now);
    }

    /// <summary>查询某个键当前是否仍在有效期内（供自检与诊断使用）</summary>
    public static bool IsFresh(string key) => TryGetContent(key, out _);

    /// <summary>查询某个键的上次成功请求时刻（不存在时返回 null）</summary>
    public static DateTimeOffset? GetFetchedAt(string key) =>
        Entries.TryGetValue(BuildKey(key), out var entry) ? entry.FetchedAt : null;

    /// <summary>失效指定键（写操作后由 API 层调用，保证下次访问拿到最新数据）。</summary>
    public static void Invalidate(string key) => Entries.TryRemove(BuildKey(key), out _);

    /// <summary>失效多个键。</summary>
    public static void Invalidate(params string[] keys)
    {
        foreach (var key in keys)
        {
            Invalidate(key);
        }
    }

    /// <summary>
    ///     失效所有以指定前缀开头的键（用于带参数的分页 / 周期类接口，如流量统计各周期）。
    /// </summary>
    public static void InvalidatePrefix(string prefix)
    {
        var fullPrefix = $"{Scope}|{prefix}";
        foreach (var key in Entries.Keys)
        {
            if (key.StartsWith(fullPrefix, StringComparison.Ordinal))
            {
                Entries.TryRemove(key, out _);
            }
        }
    }

    /// <summary>清空全部缓存（登录 / 退出登录、手动清理缓存时调用）。</summary>
    public static void InvalidateAll() => Entries.Clear();

    private static string BuildKey(string key) => $"{Scope}|{key}";

    /// <summary>条目数接近上限时清理过期项，避免长期运行后字典无界增长。</summary>
    private static void PurgeExpiredIfNeeded()
    {
        if (Entries.Count < SoftLimit)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var pair in Entries)
        {
            if (pair.Value.ExpiresAt <= now)
            {
                Entries.TryRemove(pair.Key, out _);
            }
        }
    }

    private sealed record CacheEntry(string Content, DateTimeOffset ExpiresAt, DateTimeOffset FetchedAt);
}
