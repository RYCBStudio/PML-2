namespace MEFrpLauncherX.Plugin.Core;

/// <summary>
///     插件执行日志条目（26.3.1 S4）。
///     记录每次事件触发的条件判断结果与每个动作的执行结果，供插件页只读查看/清空。
/// </summary>
public sealed class PluginExecutionLogEntry
{
    public DateTime Timestamp { get; init; } = DateTime.Now;

    /// <summary>插件 ID（YAML 中的 id 字段）</summary>
    public string PluginId { get; init; } = "";

    /// <summary>订阅的事件名（triggers.on）</summary>
    public string EventName { get; init; } = "";

    /// <summary>条件表达式（可为空）</summary>
    public string? Condition { get; init; }

    /// <summary>条件是否通过（无条件时视为 true）</summary>
    public bool ConditionMatched { get; init; } = true;

    /// <summary>动作名（动作级日志有值）</summary>
    public string ActionName { get; init; } = "";

    /// <summary>状态：info / success / failed / skipped</summary>
    public string Status { get; init; } = "info";

    /// <summary>详情（成功/失败信息）</summary>
    public string Message { get; init; } = "";
}
