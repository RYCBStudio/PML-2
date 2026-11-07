using System.Text.Json;

namespace MEFrpLauncherX.Core.Models;

/// <summary>
///     「旧版残留待删除」提醒的本地状态（26.5.1）。
///     <para>
///         收拢到 <c>legacy\</c> 之后还要提示用户删除，但每次启动都弹窗会变成骚扰。
///         这里记录上一次提醒时间，用「提醒间隔」做节流：用户点「稍后」后
///         <see cref="RemindIntervalDays" /> 天内不再打扰，过期后再提醒一次。
///     </para>
///     <para>
///         与「多余版本目录」提醒（<see cref="VersionCleanupState" />）的区别：那边用
///         「时间 + 体积增长」双重节流（回滚点是刻意保留的，不该反复催），
///         而 <c>legacy\</c> 里的东西一旦存在就是纯垃圾，周期性提醒一次即可。
///     </para>
/// </summary>
public class LegacyLeftoverState
{
    /// <summary>最近一次提醒时间（UTC）。</summary>
    public DateTimeOffset? LastNotifiedAt { get; set; }

    /// <summary>最近一次提醒时 <c>legacy\</c> 的体积（仅供诊断）。</summary>
    public long LastNotifiedBytes { get; set; }

    /// <summary>提醒间隔（天）。</summary>
    public const int RemindIntervalDays = 7;

    /// <summary>提醒间隔。</summary>
    public static TimeSpan RemindInterval => TimeSpan.FromDays(RemindIntervalDays);
}

/// <summary>
///     <see cref="LegacyLeftoverState" /> 的读写（AOT 安全：走源生成上下文）。
/// </summary>
public static class LegacyLeftoverStateStore
{
    private static string StatePath
    {
        get;
    } = Path.Combine(AppPaths.CacheDirectory, "legacy-leftover.json");

    /// <summary>读取状态；缺失或损坏时返回空状态。</summary>
    public static LegacyLeftoverState Load()
    {
        try
        {
            if (!File.Exists(StatePath))
            {
                return new LegacyLeftoverState();
            }

            var json = File.ReadAllText(StatePath);
            return JsonSerializer.Deserialize<LegacyLeftoverState>(
                       json, App.AppJsonSerializerContext.LegacyLeftoverState)
                   ?? new LegacyLeftoverState();
        }
        catch (Exception ex)
        {
            App.CurrentLogger?.Error(ex, "读取旧版残留清理状态失败");
            return new LegacyLeftoverState();
        }
    }

    /// <summary>记录已提醒。</summary>
    public static void MarkNotified(long bytes)
    {
        try
        {
            var dir = Path.GetDirectoryName(StatePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var state = new LegacyLeftoverState
            {
                LastNotifiedAt = DateTimeOffset.UtcNow,
                LastNotifiedBytes = bytes
            };
            File.WriteAllText(StatePath,
                JsonSerializer.Serialize(state, App.AppJsonSerializerContext.LegacyLeftoverState));
        }
        catch (Exception ex)
        {
            // 状态写失败最多导致下次多提醒一次，不该影响启动
            App.CurrentLogger?.Error(ex, "写入旧版残留清理状态失败");
        }
    }

    /// <summary>
    ///     是否该提醒用户删除 <c>legacy\</c> 中的旧版残留。
    /// </summary>
    /// <param name="bytes">当前 <c>legacy\</c> 的体积；为 0 时永不提醒。</param>
    public static bool ShouldNotify(long bytes)
    {
        if (bytes <= 0)
        {
            return false;
        }

        var state = Load();

        // 从未提醒过 → 立刻提醒（刚收拢完就告诉用户）
        if (state.LastNotifiedAt is null)
        {
            return true;
        }

        // 距上次提醒已超过间隔 → 再提醒一次
        return DateTimeOffset.UtcNow - state.LastNotifiedAt.Value >= LegacyLeftoverState.RemindInterval;
    }
}
