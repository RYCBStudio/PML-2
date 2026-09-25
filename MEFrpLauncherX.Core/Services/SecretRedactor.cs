using System.Text.RegularExpressions;

namespace MEFrpLauncherX.Core.Services;

/// <summary>
///     敏感信息脱敏（26.4 阶段 B）。
///     DNS 服务商凭据只允许出现在 lego 子进程的环境变量中，
///     任何写入界面日志、磁盘日志或崩溃报告之前都必须先经过本类处理。
/// </summary>
public static partial class SecretRedactor
{
    /// <summary>替换明文时使用的占位符</summary>
    public const string Placeholder = "••••••";

    /// <summary>短于该长度的值不做整体替换，避免把常见短串误伤</summary>
    private const int MinSecretLength = 6;

    /// <summary>
    ///     对文本做脱敏：
    ///     <list type="number">
    ///         <item>把已知凭据原值整体替换为占位符；</item>
    ///         <item>把「环境变量名=值」形式的赋值替换为占位符；</item>
    ///         <item>把疑似 Token 形态的长随机串替换为占位符。</item>
    ///     </list>
    /// </summary>
    /// <param name="text">原始文本（可为 null）</param>
    /// <param name="secrets">本次流程用到的凭据原值</param>
    public static string Redact(string? text, IEnumerable<string>? secrets = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        var result = text;

        if (secrets is not null)
        {
            foreach (var secret in secrets)
            {
                if (string.IsNullOrWhiteSpace(secret) || secret.Length < MinSecretLength)
                {
                    continue;
                }

                result = result.Replace(secret, Placeholder, StringComparison.Ordinal);
            }
        }

        // 环境变量赋值形式：XXX_TOKEN=xxxx / XXX_SECRET_KEY=xxxx
        result = EnvAssignmentRegex().Replace(result, m => $"{m.Groups["name"].Value}={Placeholder}");

        // 疑似 Token 形态：连续 32 位以上的 Base64 / 十六进制 / JWT 片段
        result = TokenLikeRegex().Replace(result, Placeholder);

        return result;
    }

    /// <summary>把单个敏感值转换为展示用的掩码（例如 <c>abcd••••••</c>）。</summary>
    public static string Mask(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= 4
            ? Placeholder
            : $"{value[..2]}{Placeholder}";
    }

    /// <summary>把一组「环境变量名 → 值」按脱敏形式渲染，便于排查环境是否正确传入。</summary>
    public static string DescribeEnvironment(IReadOnlyDictionary<string, string> environment)
    {
        if (environment.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(", ", environment
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key}=<已设置>"));
    }

    [GeneratedRegex(
        @"(?<name>[A-Z][A-Z0-9_]*(?:TOKEN|SECRET|KEY|PASSWORD))\s*[=:]\s*(?<value>[^\s""',;]+)",
        RegexOptions.IgnoreCase)]
    private static partial Regex EnvAssignmentRegex();

    [GeneratedRegex(@"(?<![A-Za-z0-9+/_-])[A-Za-z0-9+/_-]{32,}={0,2}(?![A-Za-z0-9+/_=-])")]
    private static partial Regex TokenLikeRegex();
}
