namespace MEFrpLauncherX.Plugin.Core;

/// <summary>
///     隧道模板定义（create-proxy-template 类型插件的 <c>templates</c> 条目）。
///     YAML 字段沿用项目 camelCase 序列化约定（与 triggers/functions 一致），
///     多词键如 <c>minCoreVersion</c>、<c>nodeFilter</c>、<c>extraTunnel</c>。
/// </summary>
public class ProxyTemplateDefinition
{
    /// <summary>模板唯一 id（同一插件文件内唯一，用于来源追踪/去重）</summary>
    public string Id { get; set; } = "";

    /// <summary>展示分组（Web/Game/Productivity 等），仅用于引导页分组，不再参与逻辑分支</summary>
    public string Category { get; set; } = "";

    /// <summary>模板显示名（回退文案，单语言场景直接填写）</summary>
    public string Name { get; set; } = "";

    /// <summary>模板描述（回退文案）</summary>
    public string Description { get; set; } = "";

    /// <summary>多语言显示名覆盖，key 取语言码（zh-hans/zh-hant/en）</summary>
    public Dictionary<string, string> NameLocalized { get; set; } = new();

    /// <summary>多语言描述覆盖，key 取语言码（zh-hans/zh-hant/en）</summary>
    public Dictionary<string, string> DescriptionLocalized { get; set; } = new();

    /// <summary>图标：pack 为 Material/SimpleIcons/FileIcons/Lucide（大小写不敏感），name 为图标名</summary>
    public ProxyTemplateIconDefinition Icon { get; set; } = new();

    /// <summary>创建表单默认值（进入 CreateProxy 时预填）</summary>
    public ProxyTemplateCreateDefinition Create { get; set; } = new();

    /// <summary>引导自动选节点条件；不满足协议/带宽要求时进入放宽轮</summary>
    public ProxyTemplateNodeFilterDefinition NodeFilter { get; set; } = new();

    /// <summary>副隧道声明（主隧道创建成功后补建互补协议隧道，如 RDP 的 TCP+UDP）；缺省无</summary>
    public ProxyTemplateExtraTunnelDefinition? ExtraTunnel { get; set; }
}

/// <summary>模板图标声明</summary>
public class ProxyTemplateIconDefinition
{
    /// <summary>图标字体族：Material/SimpleIcons/FileIcons/Lucide（大小写不敏感）</summary>
    public string Pack { get; set; } = "";

    /// <summary>图标名（对应 IconPacks 枚举成员名）</summary>
    public string Name { get; set; } = "";
}

/// <summary>创建默认值声明</summary>
public class ProxyTemplateCreateDefinition
{
    /// <summary>
    ///     隧道名模板，支持占位符：{name}=本地化显示名、{nodeId}=节点ID；其余为字面量（如 "-"、"#"）。
    ///     例："{name}-{nodeId}" 与 "{name}-#{nodeId}"。
    /// </summary>
    public string ProxyName { get; set; } = "{name}-{nodeId}";

    /// <summary>本地地址默认值</summary>
    public string LocalAddress { get; set; } = "127.0.0.1";

    /// <summary>本地端口默认值</summary>
    public int LocalPort { get; set; }

    /// <summary>预选协议（tcp/udp/http/https），进入表单时尝试选中</summary>
    public string Protocol { get; set; } = "";

    /// <summary>
    ///     远程端口策略：null/空=不预填（沿用现状）；"auto"=自动获取可用远端端口；数字字符串=固定预填。
    /// </summary>
    public string? RemotePort { get; set; }
}

/// <summary>引导选节点的筛选条件</summary>
public class ProxyTemplateNodeFilterDefinition
{
    /// <summary>首轮候选必须同时支持的协议（大小写不敏感）；空=不限</summary>
    public List<string> Protocols { get; set; } = [];

    /// <summary>放宽轮（首轮无候选时）仅要求支持的协议；空=回退到仅在线</summary>
    public List<string> FallbackProtocols { get; set; } = [];

    /// <summary>节点带宽下限（Mbps，>0 时生效；用带宽字符串解析后比较）</summary>
    public int MinBandwidthMbps { get; set; }
}

/// <summary>副隧道声明（RDP 双隧道场景）</summary>
public class ProxyTemplateExtraTunnelDefinition
{
    /// <summary>
    ///     追加到主隧道名后的名称后缀，支持 {PROTO}=与主隧道互补的协议大写（TCP/UDP）。
    ///     例："({PROTO})" → 主隧道 "RDP-1" 建好后补建 "RDP-1(UDP)"。
    /// </summary>
    public string NameSuffix { get; set; } = "({PROTO})";
}
