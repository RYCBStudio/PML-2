namespace MEFrpLauncherX.Core.Models;

/// <summary>
///     证书助手本地元数据（26.4），随证书一同落盘于
///     <c>Config/Certificates/{slug}/meta.json</c>。
/// </summary>
public class CertificateMeta
{
    /// <summary>主域名（如 <c>example.com</c>）</summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>附加域名 / SAN 列表（不含主域名）</summary>
    public List<string> AltNames { get; set; } = [];

    /// <summary>证书到期时间（UTC）</summary>
    public DateTimeOffset? NotAfter { get; set; }

    /// <summary>是否使用 ACME Staging 环境签发</summary>
    public bool Staging { get; set; }

    /// <summary>签发时间（UTC）</summary>
    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>使用的 ACME 目录地址</summary>
    public string AcmeServer { get; set; } = string.Empty;

    /// <summary>签发时使用的 lego 版本</summary>
    public string LegoVersion { get; set; } = string.Empty;

    /// <summary>
    ///     签发时使用的 DNS 账户标识（26.4 阶段 B）。
    ///     为空表示当时使用手动 DNS 模式；账户被删除后此值仍保留以便追溯。
    /// </summary>
    public Guid? DnsAccountId { get; set; }

    /// <summary>
    ///     验证方式（26.4 阶段 B）：<c>DnsApi</c> 表示 DNS 账号自动，<c>Manual</c> 表示手动 DNS。
    /// </summary>
    public string ChallengeMode { get; set; } = "Manual";

    /// <summary>证书链文件相对路径（固定为 fullchain.pem）</summary>
    public string FullChainFile { get; set; } = "fullchain.pem";

    /// <summary>私钥文件相对路径（固定为 privkey.pem）</summary>
    public string PrivateKeyFile { get; set; } = "privkey.pem";

    /// <summary>用于目录命名的标识（优先域名，缺失时回退 altNames 首项）</summary>
    public string SlugOrDomain() => !string.IsNullOrWhiteSpace(Domain)
        ? Domain
        : AltNames.FirstOrDefault() ?? "unknown";
}

/// <summary>
///     证书列表项（供 UI 展示与隧道表单选择）。
/// </summary>
public class CertificateListItem
{
    /// <summary>用于目录名的 slug</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>主域名</summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>附加域名（展示用，逗号分隔）</summary>
    public string AltNamesText { get; set; } = string.Empty;

    /// <summary>证书链完整路径（写入隧道的 crtPath）</summary>
    public string FullChainPath { get; set; } = string.Empty;

    /// <summary>私钥完整路径（写入隧道的 keyPath）</summary>
    public string PrivateKeyPath { get; set; } = string.Empty;

    /// <summary>到期时间（UTC）</summary>
    public DateTimeOffset? NotAfter { get; set; }

    /// <summary>距到期天数；无法解析时为 null</summary>
    public int? DaysToExpiry { get; set; }

    /// <summary>是否为 Staging 证书（不可用于正式环境）</summary>
    public bool Staging { get; set; }

    /// <summary>是否临近到期（&lt; 30 天）</summary>
    public bool IsExpiringSoon => DaysToExpiry is < 30;

    /// <summary>展示名：域名 + 状态前缀</summary>
    public string DisplayName => Staging ? $"[Staging] {Domain}" : Domain;
}
