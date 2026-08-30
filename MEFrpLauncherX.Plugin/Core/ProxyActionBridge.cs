namespace MEFrpLauncherX.Plugin.Core;

/// <summary>
///     插件动作 → 主程序能力的委托桥（26.3.1 S2）。
///     插件引擎位于独立程序集，不能反向引用主程序，因此通过静态委托解耦：
///     主程序在启动阶段注册实现，插件动作（如 <c>proxy.restart</c>）只依赖本桥。
/// </summary>
public static class ProxyActionBridge
{
    /// <summary>
    ///     按隧道名重启：停止该隧道终端标签并重新启动。
    ///     返回 <c>null</c> 表示成功，否则返回可读错误消息（写入插件日志）。
    ///     未注册时调用会得到空引用安全提示，不影响其他动作。
    /// </summary>
    public static Func<string, Task<string?>>? RestartProxy;

    /// <summary>按隧道名判断是否正在运行（存在对应终端标签）。</summary>
    public static Func<string, bool>? IsProxyRunning;
}
