using System.Text.Json;

namespace MEFrpLauncherX.Core.Models;

/// <summary>
///     「本次更新内容」窗口的本地状态（26.4）。
///     记录上一次已经展示过的版本号，只有版本变化时才再次弹出，避免打扰日常启动。
/// </summary>
public class WhatsNewState
{
    /// <summary>最近一次已展示更新内容的版本号（空表示从未展示）</summary>
    public string LastShownVersion { get; set; } = string.Empty;

    /// <summary>最近一次展示时间（UTC）</summary>
    public DateTimeOffset? LastShownAt { get; set; }
}

/// <summary>
///     <see cref="WhatsNewState" /> 的读写（AOT 安全：走源生成上下文）。
/// </summary>
public static class WhatsNewStateStore
{
    private static string StatePath
    {
        get;
    } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Cache", "whats-new.json");

    /// <summary>读取状态；缺失或损坏时返回空状态。</summary>
    public static WhatsNewState Load()
    {
        try
        {
            if (!File.Exists(StatePath))
            {
                return new WhatsNewState();
            }

            var json = File.ReadAllText(StatePath);
            return JsonSerializer.Deserialize<WhatsNewState>(json, App.AppJsonSerializerContext.WhatsNewState)
                   ?? new WhatsNewState();
        }
        catch (Exception ex)
        {
            App.CurrentLogger?.Error(ex, "读取更新内容状态失败");
            return new WhatsNewState();
        }
    }

    /// <summary>判断指定版本是否需要展示「本次更新内容」。</summary>
    /// <param name="currentVersion">当前应用版本（<see cref="App.Version" />）</param>
    public static bool ShouldShow(string currentVersion)
    {
        if (string.IsNullOrWhiteSpace(currentVersion))
        {
            return false;
        }

        var state = Load();
        return !string.Equals(state.LastShownVersion, currentVersion, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>记录已展示，后续同版本不再弹出。</summary>
    public static void MarkShown(string version)
    {
        try
        {
            var dir = Path.GetDirectoryName(StatePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var state = new WhatsNewState
            {
                LastShownVersion = version,
                LastShownAt = DateTimeOffset.UtcNow
            };
            File.WriteAllText(StatePath, JsonSerializer.Serialize(state, App.AppJsonSerializerContext.WhatsNewState));
        }
        catch (Exception ex)
        {
            App.CurrentLogger?.Error(ex, "保存更新内容状态失败");
        }
    }
}
