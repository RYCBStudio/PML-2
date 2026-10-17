using System.Text.Json;

namespace MEFrpLauncherX.Core.Models;

/// <summary>
///     收件箱（InboxViewer）的本地状态（26.4）。
///     仅用于判断「系统通知 / 软件通知是否有新内容」：保存上一次看到的内容快照，
///     每次加载时用当前解析结果与快照求差集，从而只把<b>这一次新出现</b>的条目标记为「新」。
///     独立存放于 <c>Cache/inbox-notice.json</c>，<b>不写入用户配置</b>（避免污染 Settings.json）。
/// </summary>
public class InboxNoticeState
{
    /// <summary>
    ///     上一次解析出的系统公告快照（按 markdown 切分后的条目原文）。
    ///     采用<b>状态替换</b>语义：每次保存都用本次解析结果整体覆盖，公告被撤回后也会随之消失。
    /// </summary>
    public List<string> SystemNoticeSnapshot
    {
        get;
        set;
    } = [];

    /// <summary>上一次看到的软件公告 ID 快照（同样为状态替换语义）</summary>
    public List<int> SoftwareNoticeIds
    {
        get;
        set;
    } = [];

    /// <summary>最近一次更新时间（UTC）</summary>
    public DateTimeOffset? UpdatedAt
    {
        get;
        set;
    }
}

/// <summary>
///     <see cref="InboxNoticeState" /> 的读写（AOT 安全：走源生成上下文）。
/// </summary>
public static class InboxNoticeStateStore
{
    private static string StatePath
    {
        get;
    } = Path.Combine(AppPaths.CacheDirectory, "inbox-notice.json");

    /// <summary>读取状态；文件缺失或损坏时返回空状态（不抛出）。</summary>
    public static InboxNoticeState Load()
    {
        try
        {
            if (!File.Exists(StatePath))
            {
                return new InboxNoticeState();
            }

            var json = File.ReadAllText(StatePath);
            return JsonSerializer.Deserialize<InboxNoticeState>(json, App.AppJsonSerializerContext.InboxNoticeState)
                   ?? new InboxNoticeState();
        }
        catch (Exception ex)
        {
            App.CurrentLogger?.Error(ex, "读取收件箱通知状态失败");
            return new InboxNoticeState();
        }
    }

    /// <summary>保存状态；失败仅记日志（不影响收件箱可用性）。</summary>
    public static void Save(InboxNoticeState state)
    {
        try
        {
            var dir = Path.GetDirectoryName(StatePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(state, App.AppJsonSerializerContext.InboxNoticeState);
            File.WriteAllText(StatePath, json);
        }
        catch (Exception ex)
        {
            App.CurrentLogger?.Error(ex, "保存收件箱通知状态失败");
        }
    }

    /// <summary>读取上一次的系统公告快照（无记录时返回空列表，首次加载因此会全部标记为新）。</summary>
    public static List<string> LoadSystemNoticeSnapshot() => Load().SystemNoticeSnapshot;

    /// <summary>
    ///     用本次解析结果整体替换系统公告快照（<b>状态替换，而不是合并</b>），
    ///     保证快照始终等于「当前实际存在的公告集合」。
    /// </summary>
    public static void SaveSystemNoticeSnapshot(IEnumerable<string> current)
    {
        var state = Load();
        state.SystemNoticeSnapshot = [.. current];
        state.UpdatedAt = DateTimeOffset.UtcNow;
        Save(state);
    }

    /// <summary>读取上一次的软件公告 ID 快照（无记录时返回空列表）。</summary>
    public static List<int> LoadSoftwareNoticeIds() => Load().SoftwareNoticeIds;

    /// <summary>用本次拉取到的软件公告 ID 整体替换快照（状态替换，而不是合并）。</summary>
    public static void SaveSoftwareNoticeIds(IEnumerable<int> current)
    {
        var state = Load();
        state.SoftwareNoticeIds = [.. current];
        state.UpdatedAt = DateTimeOffset.UtcNow;
        Save(state);
    }
}