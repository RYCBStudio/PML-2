using System.Text.RegularExpressions;

namespace MEFrpLauncherX.Core.Services;

/// <summary>
///     隧道启动成功信息（从客户端终端输出解析）。
/// </summary>
/// <param name="ProxyType">隧道协议类型（例如 tcp/http/https），未解析到时为 null</param>
/// <param name="ProxyFullName">客户端输出的完整隧道标识（形如 <c>token.proxyName</c>），未解析到时为 null</param>
/// <param name="Address">客户端给出的访问地址，未解析到时为 null</param>
public sealed record TunnelReadyInfo(string? ProxyType, string? ProxyFullName, string? Address);

/// <summary>
///     解析 mefrpc 的「隧道启动成功」输出（26.5.1）：
///     <code>
///     2026-11-16 20:10:29.414 [I] [d3b003c63b43abbe] 启动 [tcp]隧道 [token.proxyName] 成功,您可以使用 [address] 访问您的服务
///     </code>
///     该判断只用于「确认可用」与提示访问地址，不替代既有的失败检测；
///     解析器对颜色控制符、行内分段输出以及缺少部分字段的日志都保持宽容。
/// </summary>
public static partial class TunnelReadyParser
{
    private const string ReadyPhrase = "访问您的服务";

    /// <summary>
    ///     尝试从累计的终端输出中解析「隧道已可用」信息。
    /// </summary>
    /// <param name="output">截至当前的终端输出（可包含历史行）</param>
    /// <returns>解析成功时返回 <see cref="TunnelReadyInfo" />；尚未出现成功特征时返回 null。</returns>
    public static TunnelReadyInfo? TryParse(string? output)
    {
        if (string.IsNullOrEmpty(output) || !output.Contains(ReadyPhrase, StringComparison.Ordinal))
        {
            return null;
        }

        // 终端输出可能夹带 ANSI 颜色/光标控制符，先清理再做匹配。
        var text = AnsiEscape().Replace(output.Replace("\r", string.Empty), string.Empty);

        // 访问地址：兼容「[addr]」「(addr)」「【addr】」以及没有括号的写法。
        var addressMatch = AccessAddress().Match(text);
        var address = addressMatch.Success ? addressMatch.Groups["addr"].Value.Trim() : null;

        // 协议与完整隧道名：客户端可能先打印「启动 [tcp]隧道 [token.name]」，
        // 再在后续行里输出「成功，您可以使用 ...」。多次启动时取最后一次。
        var launchMatches = LaunchHeader().Matches(text);
        var type = launchMatches.Count > 0 ? launchMatches[^1].Groups["type"].Value.Trim() : null;
        var fullName = launchMatches.Count > 0 ? launchMatches[^1].Groups["id"].Value.Trim() : null;

        return new TunnelReadyInfo(
            string.IsNullOrWhiteSpace(type) ? null : type,
            string.IsNullOrWhiteSpace(fullName) ? null : fullName,
            string.IsNullOrWhiteSpace(address) ? null : address);
    }

    /// <summary>ANSI CSI 控制序列（颜色、清屏、光标移动等）</summary>
    [GeneratedRegex(@"\x1B\[[0-?]*[ -/]*[@-~]")]
    private static partial Regex AnsiEscape();

    /// <summary>「启动 [tcp]隧道 [token.name]」头部</summary>
    [GeneratedRegex(@"启动\s*\[(?<type>[^\]\r\n]+)\]\s*隧道\s*\[(?<id>[^\]\r\n]+)\]?")]
    private static partial Regex LaunchHeader();

    /// <summary>「您可以使用 [address] 访问您的服务」中的访问地址</summary>
    [GeneratedRegex(@"您可以使用\s*[\[\(【]?\s*(?<addr>[^\]\)】\s]+)\s*[\]\)】]?\s*访问您的服务")]
    private static partial Regex AccessAddress();
}
