namespace MEFrpLauncherX.Plugin.Core;

/// <summary>事件注册表项：名称、描述、携带数据字段</summary>
public sealed record PluginEventInfo(string Name, string Description, string[] DataFields);

/// <summary>动作参数定义：参数键、界面标签、是否必填</summary>
public sealed record PluginActionParamInfo(string Key, string Label, bool Required);

/// <summary>动作注册表项：名称、描述、参数定义</summary>
public sealed record PluginActionInfo(string Name, string Description, PluginActionParamInfo[] Params);

/// <summary>
///     插件事件/动作注册表（26.3.1 S6）。
///     表单编辑器的下拉选项与运行时引擎同源：新增事件/动作时只注册此处一处，
///     编辑器自动出现，禁止在 UI 侧硬编码另一份列表。
/// </summary>
public static class PluginCatalog
{
    /// <summary>已实现并可被插件订阅的事件（与 PluginService 埋点一一对应）</summary>
    public static IReadOnlyList<PluginEventInfo> Events { get; } =
    [
        new("app.startup", "应用启动完成", ["version", "os"]),
        new("app.exit", "应用退出", ["version", "os"]),
        new("app.update.available", "检测到新版本", ["currentVersion", "latestVersion"]),
        new("user.login", "用户登录成功", ["username", "group"]),
        new("user.logout", "用户登出", ["username"]),
        new("proxy.start", "隧道启动", ["proxyName", "command"]),
        new("proxy.stop", "隧道停止", ["proxyName"]),
        new("proxy.failed", "隧道失败（26.3.1 新增）", ["proxyName", "errorMessage", "errorCategory"]),
        new("page.navigate", "切换主界面页面", ["page"]),
        new("plugin.install", "插件安装成功", ["file"]),
        new("plugin.uninstall", "插件卸载成功", ["pluginId", "pluginName"]),
        new("plugin.enable", "插件启用", ["pluginId"]),
        new("plugin.disable", "插件禁用", ["pluginId"])
    ];

    /// <summary>已实现的内置动作（与 PluginEngine 注册一致；http_request 未启用故不列出）</summary>
    public static IReadOnlyList<PluginActionInfo> Actions { get; } =
    [
        new("log", "写入日志（控制台 + 应用日志文件）",
        [
            new("msg", "消息内容", true)
        ]),
        new("notify", "发送系统通知",
        [
            new("msg", "通知内容", true)
        ]),
        new("open_url", "打开 URL（系统默认浏览器）",
        [
            new("url", "URL 地址", true)
        ]),
        new("proxy.restart", "按隧道名重启隧道（26.3.1 新增）",
        [
            new("proxyName", "隧道名称", true)
        ]),
        new("local_run", "启动本地程序",
        [
            new("exe", "程序路径", true),
            new("args", "参数", false),
            new("create_no_window", "隐藏窗口（true/false）", false),
            new("use_shell_execute", "使用系统 shell（true/false）", false)
        ]),
        new("python_run", "调用 Python 脚本（需 PATH 含 python3）",
        [
            new("script", "脚本路径", true),
            new("input", "JSON 输入", false)
        ]),
        new("call_function", "调用 functions 中定义的函数",
        [
            new("func", "函数名", true)
        ]),
        new("conditional", "按条件执行一组动作",
        [
            new("condition", "条件表达式", true)
        ])
    ];

    /// <summary>按事件名查找，找不到返回 null</summary>
    public static PluginEventInfo? FindEvent(string name) =>
        Events.FirstOrDefault(e => e.Name == name);

    /// <summary>按动作名查找，找不到返回 null</summary>
    public static PluginActionInfo? FindAction(string name) =>
        Actions.FirstOrDefault(a => a.Name == name);
}
