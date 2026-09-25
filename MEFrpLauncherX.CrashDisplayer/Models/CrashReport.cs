using System;
using System.IO;
using System.Text;

namespace MEFrpLauncherX.CrashDisplayer.Models;

/// <summary>
///     主程序传递给崩溃报告器的负载（已解析）。
/// </summary>
public sealed class CrashReport
{
    public required ExceptionPayload Exception
    {
        get;
        init;
    }

    /// <summary>完整崩溃日志文本（来自负载文件或旧的 Base64 直传）。</summary>
    public required string FullLog
    {
        get;
        init;
    }

    /// <summary>崩溃负载文件路径（若主程序以文件形式传递）。</summary>
    public string? LogFilePath
    {
        get;
        init;
    }
}

public sealed class ExceptionPayload
{
    public required string Type
    {
        get;
        init;
    }

    public required string Message
    {
        get;
        init;
    }

    public required string StackTrace
    {
        get;
        init;
    }

    public override string ToString() => $"{Type}: {Message}{Environment.NewLine}{StackTrace}";
}

/// <summary>
///     崩溃负载解析。主程序通过命令行传入：
///     <c>args[0]</c> = Base64(类型||消息||堆栈摘要)，<c>args[1]</c> = 负载文件路径（新版）或 Base64(完整日志)（旧版）。
///     解析过程全面防御：任何字段缺失/损坏都回退为占位值，保证崩溃报告器自身不再崩溃。
/// </summary>
public static class CrashPayloadParser
{
    public static CrashReport Parse(string? exJson, string? logArg)
    {
        var exception = ParseExceptionInfo(exJson);
        var (fullLog, logFilePath) = ParseFullLog(logArg);

        if (string.IsNullOrWhiteSpace(fullLog))
        {
            fullLog = exception.ToString();
        }

        return new CrashReport
        {
            Exception = exception,
            FullLog = fullLog,
            LogFilePath = logFilePath
        };
    }

    private static ExceptionPayload ParseExceptionInfo(string? exJson)
    {
        const string unknownType = "UnknownError";
        const string unknownMessage = "(无法解析的异常信息 / Unparsable exception info)";

        if (string.IsNullOrWhiteSpace(exJson))
        {
            return new ExceptionPayload { Type = unknownType, Message = unknownMessage, StackTrace = "" };
        }

        try
        {
            var decoded = Base64Decode(exJson);
            var parts = decoded.Split("||");
            return new ExceptionPayload
            {
                Type = parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]) ? parts[0] : unknownType,
                Message = parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]) ? parts[1] : unknownMessage,
                StackTrace = parts.Length > 2 ? parts[2] : ""
            };
        }
        catch
        {
            // Base64 解码失败（参数被截断/损坏）时按原文展示，仍保证界面可用。
            return new ExceptionPayload { Type = unknownType, Message = exJson, StackTrace = "" };
        }
    }

    private static (string FullLog, string? LogFilePath) ParseFullLog(string? logArg)
    {
        if (string.IsNullOrWhiteSpace(logArg))
        {
            return ("", null);
        }

        // 新版：负载文件路径（主程序把完整崩溃日志写入 Logs/Crash 后传路径，
        // 避免超长命令行导致崩溃报告器无法启动）
        try
        {
            if (File.Exists(logArg))
            {
                return (File.ReadAllText(logArg), logArg);
            }
        }
        catch
        {
            // 文件读取失败则按旧格式尝试
        }

        // 旧版兼容：Base64 直传的完整日志
        try
        {
            return (Base64Decode(logArg), null);
        }
        catch
        {
            return (logArg, null);
        }
    }

    public static string Base64Decode(string base64EncodedData)
    {
        var base64EncodedBytes = Convert.FromBase64String(base64EncodedData);
        return Encoding.UTF8.GetString(base64EncodedBytes);
    }
}
