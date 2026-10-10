using System.Text.Json;

namespace MEFrpLauncherX.Core.Models;

/// <summary>
///     「多余版本目录」提醒的本地状态（26.5.0）。
///     <para>
///         升级后安装根会留下当前版本 + 回滚点，这是设计如此；但可清理目录累积到
///         一定体积后需要提醒一次。这里记录「上次提醒时间」与「上次提醒时的可回收体积」，
///         以实现两个节流规则：
///     </para>
///     <list type="number">
///         <item>距离上次提醒不足 <see cref="RemindInterval" /> 天 → 不再提醒（避免每次升级都弹窗）；</item>
///         <item>可回收体积没有明显增长（低于 <see cref="GrowthThreshold" />）→ 不再提醒（无意义打扰）。</item>
///     </list>
/// </summary>
public class VersionCleanupState
{
    /// <summary>最近一次提醒时间（UTC）。</summary>
    public DateTimeOffset? LastNotifiedAt { get; set; }

    /// <summary>最近一次提醒时的可回收字节数，用于判断「是否又长大了」。</summary>
    public long LastNotifiedBytes { get; set; }

    /// <summary>提醒间隔（天）。</summary>
    public const int RemindIntervalDays = 7;

    /// <summary>可回收体积至少增长这么多字节才值得再次提醒（约 100 MB）。</summary>
    public const long GrowthThresholdBytes = 100L * 1024 * 1024;

    /// <summary>提醒间隔。</summary>
    public static TimeSpan RemindInterval => TimeSpan.FromDays(RemindIntervalDays);
}

/// <summary>
///     <see cref="VersionCleanupState" /> 的读写（AOT 安全：走源生成上下文）。
/// </summary>
public static class VersionCleanupStateStore
{
    private static string StatePath
    {
        get;
    } = Path.Combine(AppPaths.CacheDirectory, "version-cleanup.json");

    /// <summary>读取状态；缺失或损坏时返回空状态。</summary>
    public static VersionCleanupState Load()
    {
        try
        {
            if (!File.Exists(StatePath))
            {
                return new VersionCleanupState();
            }

            var json = File.ReadAllText(StatePath);
            return JsonSerializer.Deserialize<VersionCleanupState>(
                       json, App.AppJsonSerializerContext.VersionCleanupState)
                   ?? new VersionCleanupState();
        }
        catch (Exception ex)
        {
            App.CurrentLogger?.Error(ex, "读取版本清理状态失败");
            return new VersionCleanupState();
        }
    }

    /// <summary>记录已提醒。</summary>
    public static void MarkNotified(long removableBytes)
    {
        try
        {
            var dir = Path.GetDirectoryName(StatePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var state = new VersionCleanupState
            {
                LastNotifiedAt = DateTimeOffset.UtcNow,
                LastNotifiedBytes = removableBytes
            };
            File.WriteAllText(StatePath,
                JsonSerializer.Serialize(state, App.AppJsonSerializerContext.VersionCleanupState));
        }
        catch (Exception ex)
        {
            // 状态写失败最多导致下次多提醒一次，不该影响启动
            App.CurrentLogger?.Error(ex, "写入版本清理状态失败");
        }
    }

    /// <summary>
    ///     结合节流规则判断是否该提醒用户清理多余版本目录。
    /// </summary>
    /// <param name="removableBytes">当前可回收字节数（<see cref="Services.VersionCleanupService.Inspection.RemovableSizeBytes" />）</param>
    public static bool ShouldNotify(long removableBytes)
    {
        var state = Load();

        // 从未提醒过 → 只要达到阈值就该提醒
        if (state.LastNotifiedAt is null)
        {
            return true;
        }

        // 节流一：时间间隔未到
        if (DateTimeOffset.UtcNow - state.LastNotifiedAt.Value < VersionCleanupState.RemindInterval)
        {
            return false;
        }

        // 节流二：体积没有明显增长（用户可能已经手动清理过，或清理无济于事）
        return removableBytes - state.LastNotifiedBytes >= VersionCleanupState.GrowthThresholdBytes;
    }
}