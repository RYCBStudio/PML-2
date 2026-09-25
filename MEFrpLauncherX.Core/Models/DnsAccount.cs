namespace MEFrpLauncherX.Core.Models;

/// <summary>
///     DNS 账户元数据（26.4 阶段 B）。仅包含可明文展示的信息，
///     真正的凭据（Token / AccessKey 等）以密文形式存于
///     <see cref="MEFrpLauncherX.Core.Services.DnsAccountStore" /> 管理的本地文件中。
/// </summary>
public class DnsAccount
{
    /// <summary>账户标识（新建时生成）</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>用户备注名，例如「CF-主域名」</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>服务商标识（见 <see cref="DnsProviders" />）</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>创建时间（UTC）</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>最后更新时间（UTC）</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
///     单个 DNS 账户的完整记录：元数据 + 密文解密后的凭据字段。
///     该类型<b>只在内存中短暂存在</b>，不参与日志与遥测。
/// </summary>
public class DnsAccountEntry
{
    /// <summary>账户元数据</summary>
    public DnsAccount Meta { get; set; } = new();

    /// <summary>
    ///     凭据字段：键为 <see cref="DnsProviderField.Key" />，
    ///     值为用户填写的明文（仅在内存与加密文件中出现）。
    /// </summary>
    public Dictionary<string, string> Credentials { get; set; } = [];
}

/// <summary>
///     DNS 账户存储文件（整体加密后落盘，
///     形如 <c>%AppData%/PML2/certs/dns_accounts.dat</c>）。
/// </summary>
public class DnsAccountFile
{
    /// <summary>文件格式版本，便于后续迁移</summary>
    public int Version { get; set; } = 1;

    /// <summary>全部账户</summary>
    public List<DnsAccountEntry> Accounts { get; set; } = [];
}

/// <summary>供 UI 列表展示的摘要项，<b>不含任何凭据</b>。</summary>
public class DnsAccountSummary
{
    /// <summary>账户标识</summary>
    public Guid Id { get; set; }

    /// <summary>用户备注名</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>服务商标识</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>服务商展示名（找不到时为 <see cref="Provider" /> 原值）</summary>
    public string ProviderDisplayName { get; set; } = string.Empty;

    /// <summary>最后更新时间（UTC）</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>列表展示用：名称 + 服务商</summary>
    public string DisplayText => $"{DisplayName} · {ProviderDisplayName}";

    /// <summary>最后更新时间的本地化文本</summary>
    public string UpdatedAtText => UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    /// <summary>转换为 <see cref="DnsAccount" /> 以便编辑</summary>
    public DnsAccount ToMeta() => new()
    {
        Id = Id,
        DisplayName = DisplayName,
        Provider = Provider,
        UpdatedAt = UpdatedAt
    };
}

/// <summary>
///     DNS 服务商表单字段描述（表驱动）。
///     新增厂商时只需在 <see cref="DnsProviders" /> 中追加描述，无需改动页面逻辑。
/// </summary>
public sealed class DnsProviderField
{
    /// <summary>凭据字典中的键名（加密负载中的字段名）</summary>
    public required string Key { get; init; }

    /// <summary>lego 读取的环境变量名（例如 <c>CLOUDFLARE_DNS_API_TOKEN</c>）</summary>
    public required string EnvVar { get; init; }

    /// <summary>UI 标签的资源键（通过 <c>Languages.ResourceManager</c> 解析）</summary>
    public required string LabelKey { get; init; }

    /// <summary>UI 水印提示的资源键（可为空）</summary>
    public string? PlaceholderKey { get; init; }

    /// <summary>是否为敏感值（敏感值在日志中一律脱敏，输入框使用密码样式）</summary>
    public bool IsSecret { get; init; } = true;

    /// <summary>是否必填</summary>
    public bool Required { get; init; } = true;
}

/// <summary>DNS 服务商描述（阶段 B 首批覆盖 Cloudflare / 阿里云 DNS / DNSPod）。</summary>
public sealed class DnsProviderDescriptor
{
    /// <summary>本软件的稳定标识（也是凭据存储中 <see cref="DnsAccount.Provider" /> 的值）</summary>
    public required string Id { get; init; }

    /// <summary>
    ///     lego 的 provider 代码（<c>--dns</c> 参数值）。
    ///     注意：lego v5 起 <c>dnspod</c> 已下线，DNSPod 请使用 <c>tencentcloud</c>。
    /// </summary>
    public required string LegoProvider { get; init; }

    /// <summary>厂商环境变量前缀，用于附加传播参数（如 <c>CLOUDFLARE_PROPAGATION_TIMEOUT</c>）</summary>
    public required string EnvPrefix { get; init; }

    /// <summary>界面展示名</summary>
    public required string DisplayName { get; init; }

    /// <summary>「最小权限如何创建 Token」文档链接</summary>
    public required string DocumentationUrl { get; init; }

    /// <summary>最小权限提示的资源键（可为空）</summary>
    public string? PermissionHintKey { get; init; }

    /// <summary>表驱动字段列表</summary>
    public required IReadOnlyList<DnsProviderField> Fields { get; init; }
}

/// <summary>
///     DNS 服务商注册表（26.4 阶段 B）。
///     仅覆盖首批 3 家，后续新增厂商在此追加即可，UI 会自动出现对应字段。
/// </summary>
public static class DnsProviders
{
    /// <summary>Cloudflare</summary>
    public const string Cloudflare = "cloudflare";

    /// <summary>阿里云 DNS（Alibaba Cloud DNS）</summary>
    public const string AliDns = "alidns";

    /// <summary>DNSPod / 腾讯云 DNS</summary>
    public const string DnsPod = "dnspod";

    /// <summary>全部已支持的服务商（顺序即 UI 下拉顺序）</summary>
    public static IReadOnlyList<DnsProviderDescriptor> All { get; } =
    [
        new DnsProviderDescriptor
        {
            Id = Cloudflare,
            LegoProvider = "cloudflare",
            EnvPrefix = "CLOUDFLARE",
            DisplayName = "Cloudflare",
            DocumentationUrl = "https://developers.cloudflare.com/fundamentals/api/get-started/create-token/",
            PermissionHintKey = "Text.Dns.Permission.Cloudflare",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiToken",
                    EnvVar = "CLOUDFLARE_DNS_API_TOKEN",
                    LabelKey = "Text.Dns.Field.ApiToken",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiToken"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = AliDns,
            LegoProvider = "alidns",
            EnvPrefix = "ALICLOUD",
            DisplayName = "阿里云 DNS",
            DocumentationUrl = "https://help.aliyun.com/zh/ram/user-guide/create-an-accesskey-pair",
            PermissionHintKey = "Text.Dns.Permission.AliDns",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "AccessKeyId",
                    EnvVar = "ALICLOUD_ACCESS_KEY",
                    LabelKey = "Text.Dns.Field.AccessKeyId",
                    PlaceholderKey = "Text.Dns.Placeholder.AccessKeyId"
                },
                new DnsProviderField
                {
                    Key = "AccessKeySecret",
                    EnvVar = "ALICLOUD_SECRET_KEY",
                    LabelKey = "Text.Dns.Field.AccessKeySecret",
                    PlaceholderKey = "Text.Dns.Placeholder.AccessKeySecret"
                },
                new DnsProviderField
                {
                    Key = "SecurityToken",
                    EnvVar = "ALICLOUD_SECURITY_TOKEN",
                    LabelKey = "Text.Dns.Field.SecurityToken",
                    PlaceholderKey = "Text.Dns.Placeholder.Optional",
                    Required = false
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = DnsPod,
            // lego v5 已移除 dnspod provider，DNSPod / 腾讯云统一走 tencentcloud
            LegoProvider = "tencentcloud",
            EnvPrefix = "TENCENTCLOUD",
            DisplayName = "DNSPod / 腾讯云",
            DocumentationUrl = "https://console.cloud.tencent.com/cam/capi",
            PermissionHintKey = "Text.Dns.Permission.DnsPod",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "SecretId",
                    EnvVar = "TENCENTCLOUD_SECRET_ID",
                    LabelKey = "Text.Dns.Field.SecretId",
                    PlaceholderKey = "Text.Dns.Placeholder.SecretId"
                },
                new DnsProviderField
                {
                    Key = "SecretKey",
                    EnvVar = "TENCENTCLOUD_SECRET_KEY",
                    LabelKey = "Text.Dns.Field.SecretKey",
                    PlaceholderKey = "Text.Dns.Placeholder.SecretKey"
                },
                new DnsProviderField
                {
                    Key = "SessionToken",
                    EnvVar = "TENCENTCLOUD_SESSION_TOKEN",
                    LabelKey = "Text.Dns.Field.SessionToken",
                    PlaceholderKey = "Text.Dns.Placeholder.Optional",
                    Required = false
                }
            ]
        }
    ];

    /// <summary>按标识解析服务商描述；未知标识返回 null。</summary>
    public static DnsProviderDescriptor? Resolve(string? providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            return null;
        }

        foreach (var p in All)
        {
            if (string.Equals(p.Id, providerId, StringComparison.OrdinalIgnoreCase))
            {
                return p;
            }
        }

        return null;
    }

    /// <summary>服务商的界面展示名；未知标识时回退为原值。</summary>
    public static string GetDisplayName(string? providerId) =>
        Resolve(providerId)?.DisplayName ?? providerId ?? string.Empty;
}
