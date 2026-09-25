namespace MEFrpLauncherX.Core.Services;

/// <summary>ACME 申请失败的分类（用于 UI 给出可读提示与修复建议）。</summary>
public enum AcmeErrorKind
{
    /// <summary>未能分类</summary>
    Unknown,

    /// <summary>lego 未就绪或下载失败</summary>
    LegoUnavailable,

    /// <summary>DNS 服务商认证失败（Token 无效 / 已过期）</summary>
    DnsAuthFailed,

    /// <summary>DNS 服务商权限不足（缺少 DNS 编辑权限）</summary>
    DnsPermissionDenied,

    /// <summary>域名未在所选服务商托管 / Zone 找不到</summary>
    DnsZoneNotFound,

    /// <summary>DNS 传播超时</summary>
    PropagationTimeout,

    /// <summary>CA 限流（生产环境签发次数过多）</summary>
    RateLimited,

    /// <summary>CA 校验失败（域名不可达 / 记录不匹配）</summary>
    ChallengeFailed,

    /// <summary>域名非法或不受支持</summary>
    InvalidDomain,

    /// <summary>网络问题（无法访问 CA 或 DNS API）</summary>
    NetworkError
}

/// <summary>ACME 错误映射结果。</summary>
/// <param name="Kind">错误分类</param>
/// <param name="ResourceKey">对应的本地化文案资源键</param>
public sealed record AcmeErrorInfo(AcmeErrorKind Kind, string ResourceKey);

/// <summary>
///     把 lego 的输出映射为用户可读的错误分类（26.4 阶段 B，B8）。
///     映射规则来自 lego / ACME 常见失败信息，尽量宽松匹配，避免漏判。
/// </summary>
public static class AcmeErrorMapper
{
    /// <summary>根据 lego 的 stderr（以及可选的 stdout）推断失败原因。</summary>
    public static AcmeErrorInfo Map(string? stderr, string? stdout = null)
    {
        var text = $"{stderr}\n{stdout}";
        if (string.IsNullOrWhiteSpace(text))
        {
            return new AcmeErrorInfo(AcmeErrorKind.Unknown, "Text.Dns.Error.Unknown");
        }

        // 顺序很重要：先匹配更具体的信号
        if (Contains(text, "InvalidAccessKeyId") || Contains(text, "SignatureDoesNotMatch") ||
            Contains(text, "InvalidClientTokenId") || Contains(text, "AuthFailure") ||
            Contains(text, "Invalid credentials") || Contains(text, "authentication failure") ||
            Contains(text, "401") && Contains(text, "Unauthorized"))
        {
            return new AcmeErrorInfo(AcmeErrorKind.DnsAuthFailed, "Text.Dns.Error.DnsAuthFailed");
        }

        if (Contains(text, "Forbidden") || Contains(text, "permission denied") ||
            Contains(text, "AccessDenied") || Contains(text, "not authorized") ||
            Contains(text, "code: 403"))
        {
            return new AcmeErrorInfo(AcmeErrorKind.DnsPermissionDenied, "Text.Dns.Error.DnsPermissionDenied");
        }

        if (Contains(text, "zone not found") || Contains(text, "Could not find zone") ||
            Contains(text, "Zone not found") || Contains(text, "No zone found") ||
            Contains(text, "domain not found") || Contains(text, "unable to find the zone"))
        {
            return new AcmeErrorInfo(AcmeErrorKind.DnsZoneNotFound, "Text.Dns.Error.DnsZoneNotFound");
        }

        if (Contains(text, "propagation") && (Contains(text, "timeout") || Contains(text, "timed out")))
        {
            return new AcmeErrorInfo(AcmeErrorKind.PropagationTimeout, "Text.Dns.Error.PropagationTimeout");
        }

        if (Contains(text, "rateLimited") || Contains(text, "rate limit") ||
            Contains(text, "too many certificates") || Contains(text, "too many failed authorizations") ||
            Contains(text, "429"))
        {
            return new AcmeErrorInfo(AcmeErrorKind.RateLimited, "Text.Dns.Error.RateLimited");
        }

        if (Contains(text, "no such host") || Contains(text, "connection refused") ||
            Contains(text, "i/o timeout") || Contains(text, "TLS handshake") ||
            Contains(text, "dial tcp") || Contains(text, "context deadline exceeded") ||
            Contains(text, "unable to reach"))
        {
            return new AcmeErrorInfo(AcmeErrorKind.NetworkError, "Text.Dns.Error.NetworkError");
        }

        if (Contains(text, "dns: ") || Contains(text, "challenge failed") ||
            Contains(text, "unauthorized") || Contains(text, "incorrect TXT record") ||
            Contains(text, "Invalid response from") || Contains(text, "no TXT record found"))
        {
            return new AcmeErrorInfo(AcmeErrorKind.ChallengeFailed, "Text.Dns.Error.ChallengeFailed");
        }

        if (Contains(text, "invalid domain") || Contains(text, "Domain name contains an invalid character") ||
            Contains(text, "not a valid domain"))
        {
            return new AcmeErrorInfo(AcmeErrorKind.InvalidDomain, "Text.Dns.Error.InvalidDomain");
        }

        return new AcmeErrorInfo(AcmeErrorKind.Unknown, "Text.Dns.Error.Unknown");
    }

    /// <summary>获取分类对应的本地化文案（资源缺失时回退到资源键）。</summary>
    public static string GetMessage(AcmeErrorInfo info)
    {
        try
        {
            var text = Languages.Languages.ResourceManager.GetString(info.ResourceKey,
                Languages.Languages.Culture);
            return string.IsNullOrWhiteSpace(text) ? info.ResourceKey : text;
        }
        catch
        {
            return info.ResourceKey;
        }
    }

    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
