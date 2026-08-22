using System;
using System.Linq;
using System.Text.RegularExpressions;
using MEFrpLauncherX.Core.Languages;

namespace MEFrpLauncherX.Core.Services;

/// <summary>隧道失败类别（26.3 M3）</summary>
public enum TunnelErrorCategory
{
    /// <summary>未识别或 API 层原始错误</summary>
    Unknown,

    /// <summary>认证失败（token/登录态异常）</summary>
    AuthenticationFailed,

    /// <summary>端口占用</summary>
    PortInUse,

    /// <summary>节点不可达（连接被拒/域名无法解析/启动超时无确认）</summary>
    NodeUnreachable,

    /// <summary>进程崩溃（mefrpc 退出码非 0）</summary>
    ProcessCrashed,

    /// <summary>本地服务未启动</summary>
    LocalServiceUnavailable
}

/// <summary>隧道错误信息：类别 + 可读摘要（非原始字符串）</summary>
public sealed record TunnelErrorInfo(TunnelErrorCategory Category, string Summary);

/// <summary>
///     将终端输出摘要 / API 错误 / 进程退出状态映射为可读的失败原因。
///     所有摘要文案走 i18n（<see cref="Languages" />），不含原始输出，便于用户直接阅读与上报。
/// </summary>
public static class TunnelErrorMapper
{
    /// <summary>
    ///     综合终端输出、API 错误与进程退出码，判定失败类别并生成可读摘要。
    /// </summary>
    /// <param name="terminalOutput">终端最近输出（可为 null）</param>
    /// <param name="apiError">API 层错误信息（可为 null）</param>
    /// <param name="processExitCode">进程退出码（可为 null）</param>
    public static TunnelErrorInfo Map(string? terminalOutput = null, string? apiError = null,
        int? processExitCode = null)
    {
        var text = string.Join("\n",
            new[] { terminalOutput, apiError }.Where(s => !string.IsNullOrWhiteSpace(s)));

        // 1) 认证失败：token / 登录态异常（frpc 常见 "login to server failed: ..."）
        if (text.Contains("401", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("authentication", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("auth failed", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("invalid token", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("token expired", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("token invalid", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("login to server failed", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("认证失败", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("未授权", StringComparison.OrdinalIgnoreCase))
        {
            return new TunnelErrorInfo(TunnelErrorCategory.AuthenticationFailed,
                Languages.Languages.Text_TunnelError_AuthFailed);
        }

        // 2) 端口占用
        if (text.Contains("port already in use", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("address already in use", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("bind: address already in use", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("端口被占用", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("端口已使用", StringComparison.OrdinalIgnoreCase))
        {
            return new TunnelErrorInfo(TunnelErrorCategory.PortInUse, Languages.Languages.Text_TunnelError_PortInUse);
        }

        // 3) 节点不可达：连接被拒 / 域名无法解析 / 网络不可达
        if (text.Contains("connection refused", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("connect refused", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("name or service not known", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("no route to host", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("无法连接", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("连接被拒绝", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("connection timed out", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("连接超时", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("登录节点失败", StringComparison.OrdinalIgnoreCase)
            )
        {
            return new TunnelErrorInfo(TunnelErrorCategory.NodeUnreachable,
                Languages.Languages.Text_TunnelError_NodeUnreachable);
        }

        // 4) 进程崩溃：输出文本含 "Process exited with code: N"（N≠0），或显式退出码非 0
        var exitMatch = Regex.Match(text, @"[Pp]rocess exited with code:\s*(-?\d+)");
        if (exitMatch.Success &&
            int.TryParse(exitMatch.Groups[1].Value, out var parsedExitCode) &&
            parsedExitCode != 0)
        {
            return new TunnelErrorInfo(TunnelErrorCategory.ProcessCrashed,
                string.Format(Languages.Languages.Text_TunnelError_ProcessCrashedFormat, parsedExitCode));
        }

        // 5) 本地服务
        if (text.Contains("the target machine actively refused it", StringComparison.OrdinalIgnoreCase))
        {
            return new TunnelErrorInfo(TunnelErrorCategory.LocalServiceUnavailable,
                Languages.Languages.Text_TunnelError_LocalServiceUnavailable);
        }

        if (processExitCode is not null && processExitCode != 0)
        {
            return new TunnelErrorInfo(TunnelErrorCategory.ProcessCrashed,
                string.Format(Languages.Languages.Text_TunnelError_ProcessCrashedFormat, processExitCode.Value));
        }

        // 5) API 层错误：保留原始信息便于排查
        if (!string.IsNullOrWhiteSpace(apiError))
        {
            return new TunnelErrorInfo(TunnelErrorCategory.Unknown, apiError);
        }

        return new TunnelErrorInfo(TunnelErrorCategory.Unknown, Languages.Languages.Text_TunnelError_Unknown);
    }

    /// <summary>启动超时（一定时间内无输出、服务端未确认在线）→ 节点不可达</summary>
    public static TunnelErrorInfo MapTimeout() =>
        new(TunnelErrorCategory.NodeUnreachable, Languages.Languages.Text_TunnelError_NodeUnreachable);
}