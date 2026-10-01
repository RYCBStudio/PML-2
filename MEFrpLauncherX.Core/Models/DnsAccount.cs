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

/// <summary>
///     DNS 服务商描述（表驱动，覆盖 lego v5.5.1 提供的全部 provider）。
/// </summary>
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

    /// <summary>
    ///     是否需要用户填写凭据。默认 <c>true</c>；
    ///     少数服务商（如 HyperOne）改用本机护照文件认证，无需在此填写任何字段。
    /// </summary>
    public bool RequiresCredentials { get; init; } = true;

    /// <summary>表驱动字段列表</summary>
    public required IReadOnlyList<DnsProviderField> Fields { get; init; }
}

/// <summary>
///     DNS 服务商注册表（26.4 阶段 B）。
/// </summary>
public static class DnsProviders
{
    /// <summary>Cloudflare</summary>
    public const string Cloudflare = "cloudflare";

    /// <summary>阿里云 DNS（Alibaba Cloud DNS）</summary>
    public const string AliDns = "alidns";

    /// <summary>DNSPod / 腾讯云 DNS</summary>
    public const string DnsPod = "dnspod";

    /// <summary>AWS Route 53</summary>
    public const string Route53 = "route53";

    /// <summary>Azure DNS</summary>
    public const string AzureDns = "azuredns";

    /// <summary>DigitalOcean</summary>
    public const string DigitalOcean = "digitalocean";

    /// <summary>Gandi</summary>
    public const string Gandi = "gandi";

    /// <summary>Namecheap</summary>
    public const string Namecheap = "namecheap";

    /// <summary>GoDaddy</summary>
    public const string GoDaddy = "godaddy";

    /// <summary>Google Cloud DNS</summary>
    public const string GCloud = "gcloud";

    /// <summary>Hetzner</summary>
    public const string Hetzner = "hetzner";

    /// <summary>Vultr</summary>
    public const string Vultr = "vultr";

    /// <summary>Linode</summary>
    public const string Linode = "linode";

    /// <summary>PowerDNS</summary>
    public const string PowerDNS = "pdns";
    
    /// <summary>Dynu</summary>
    public const string Dynu = "dynu";

    /// <summary>EasyDNS</summary>
    public const string EasyDns = "easydns";

    /// <summary>Akamai EdgeDNS</summary>
    public const string EdgeDns = "edgedns";

    /// <summary>Exoscale</summary>
    public const string Exoscale = "exoscale";

    /// <summary>G-Core</summary>
    public const string GCore = "gcore";

    /// <summary>Gandi LiveDNS (v5)</summary>
    public const string GandiV5 = "gandiv5";

    /// <summary>Glesys</summary>
    public const string Glesys = "glesys";

    /// <summary>Hostinger</summary>
    public const string Hostinger = "hostinger";

    /// <summary>Infomaniak</summary>
    public const string Infomaniak = "infomaniak";

    /// <summary>Joker</summary>
    public const string Joker = "joker";

    /// <summary>Amazon Lightsail</summary>
    public const string Lightsail = "lightsail";

    /// <summary>Liquid Web</summary>
    public const string LiquidWeb = "liquidweb";

    /// <summary>Loopia</summary>
    public const string Loopia = "loopia";

    /// <summary>LuaDNS</summary>
    public const string LuaDns = "luadns";

    /// <summary>Netcup</summary>
    public const string Netcup = "netcup";

    /// <summary>NS1</summary>
    public const string NS1 = "ns1";

    /// <summary>Oracle Cloud</summary>
    public const string OracleCloud = "oraclecloud";

    /// <summary>OVH</summary>
    public const string Ovh = "ovh";

    /// <summary>Porkbun</summary>
    public const string Porkbun = "porkbun";

    /// <summary>Rackspace</summary>
    public const string Rackspace = "rackspace";

    /// <summary>TransIP</summary>
    public const string TransIp = "transip";

    /// <summary>Yandex PDD</summary>
    public const string Yandex = "yandex";

    /// <summary>RFC2136 DNS Update</summary>
    public const string DnsUpdate = "dnsupdate";

    // ===== 以下为随 lego v5.5.1 同步补齐的服务商（按 CLI flag 字母序）=====

    /// <summary>1cloud.ru</summary>
    public const string OneCloudRu = "onecloudru";

    /// <summary>35.com/三五互联</summary>
    public const string Com35 = "com35";

    /// <summary>51DNS</summary>
    public const string Dns51 = "dns51";

    /// <summary>Abion</summary>
    public const string Abion = "abion";

    /// <summary>Active24</summary>
    public const string Active24 = "active24";

    /// <summary>AlibabaCloud ESA</summary>
    public const string AliEsa = "aliesa";

    /// <summary>all-inkl</summary>
    public const string AllInkl = "allinkl";

    /// <summary>Alwaysdata</summary>
    public const string AlwaysData = "alwaysdata";

    /// <summary>Anexia CloudDNS</summary>
    public const string Anexia = "anexia";

    /// <summary>ANS SafeDNS</summary>
    public const string SafeDns = "safedns";

    /// <summary>ArtFiles</summary>
    public const string ArtFiles = "artfiles";

    /// <summary>ArvanCloud</summary>
    public const string ArvanCloud = "arvancloud";

    /// <summary>Aurora DNS</summary>
    public const string AuroraDns = "auroradns";

    /// <summary>Autodns</summary>
    public const string AutoDns = "autodns";

    /// <summary>Axelname</summary>
    public const string AxelName = "axelname";

    /// <summary>Azion</summary>
    public const string Azion = "azion";

    /// <summary>Baidu Cloud</summary>
    public const string BaiduCloud = "baiducloud";

    /// <summary>Beget.com</summary>
    public const string Beget = "beget";

    /// <summary>Binary Lane</summary>
    public const string BinaryLane = "binarylane";

    /// <summary>Bindman</summary>
    public const string Bindman = "bindman";

    /// <summary>Bluecat</summary>
    public const string Bluecat = "bluecat";

    /// <summary>Bluecat v2</summary>
    public const string BluecatV2 = "bluecatv2";

    /// <summary>BookMyName</summary>
    public const string BookMyName = "bookmyname";

    /// <summary>Bunny</summary>
    public const string Bunny = "bunny";

    /// <summary>Checkdomain</summary>
    public const string Checkdomain = "checkdomain";

    /// <summary>Civo</summary>
    public const string Civo = "civo";

    /// <summary>Cloud.ru</summary>
    public const string CloudRu = "cloudru";

    /// <summary>CloudDNS</summary>
    public const string CloudDns = "clouddns";

    /// <summary>ClouDNS</summary>
    public const string ClouDns = "cloudns";

    /// <summary>Connbyte</summary>
    public const string Connbyte = "connbyte";

    /// <summary>ConoHa v2</summary>
    public const string ConoHa = "conoha";

    /// <summary>ConoHa v3</summary>
    public const string ConoHaV3 = "conohav3";

    /// <summary>Constellix</summary>
    public const string Constellix = "constellix";

    /// <summary>Core-Networks</summary>
    public const string CoreNetworks = "corenetworks";

    /// <summary>CPanel/WHM</summary>
    public const string CPanel = "cpanel";

    /// <summary>Curanet</summary>
    public const string Curanet = "curanet";

    /// <summary>Czechia</summary>
    public const string Czechia = "czechia";

    /// <summary>DanDomain</summary>
    public const string DanDomain = "dandomain";

    /// <summary>DDnss (DynDNS Service)</summary>
    public const string DDnss = "ddnss";

    /// <summary>Derak Cloud</summary>
    public const string Derak = "derak";

    /// <summary>deSEC.io</summary>
    public const string DeSec = "desec";

    /// <summary>Designate DNSaaS for Openstack</summary>
    public const string Designate = "designate";

    /// <summary>Dinahosting</summary>
    public const string Dinahosting = "dinahosting";

    /// <summary>DirectAdmin</summary>
    public const string DirectAdmin = "directadmin";

    /// <summary>DNS Made Easy</summary>
    public const string DnsMadeEasy = "dnsmadeeasy";

    /// <summary>dns.la</summary>
    public const string DnsLa = "dnsla";

    /// <summary>DNS.services</summary>
    public const string DnsServices = "dnsservices";

    /// <summary>DNScale</summary>
    public const string DnsCale = "dnscale";

    /// <summary>DNSExit</summary>
    public const string DnsExit = "dnsexit";

    /// <summary>dnsHome.de</summary>
    public const string DnsHomeDe = "dnshomede";

    /// <summary>DNSimple</summary>
    public const string DnsSimple = "dnsimple";

    /// <summary>DNSMint</summary>
    public const string DnsMint = "dnsmint";

    /// <summary>Domain Offensive (do.de)</summary>
    public const string DomainOffensive = "dode";

    /// <summary>Domeneshop</summary>
    public const string Domeneshop = "domeneshop";

    /// <summary>DreamHost</summary>
    public const string DreamHost = "dreamhost";

    /// <summary>Duck DNS</summary>
    public const string DuckDns = "duckdns";

    /// <summary>Dyn</summary>
    public const string Dyn = "dyn";

    /// <summary>Dynadot</summary>
    public const string Dynadot = "dynadot";

    /// <summary>DynDnsFree.de</summary>
    public const string DynDnsFree = "dyndnsfree";

    /// <summary>EdgeCenter</summary>
    public const string EdgeCenter = "edgecenter";

    /// <summary>Efficient IP</summary>
    public const string EfficientIp = "efficientip";

    /// <summary>Epik</summary>
    public const string Epik = "epik";

    /// <summary>EuroDNS</summary>
    public const string EuroDns = "eurodns";

    /// <summary>EUserv</summary>
    public const string EuServ = "euserv";

    /// <summary>Excedo</summary>
    public const string Excedo = "excedo";

    /// <summary>External program</summary>
    public const string ExternalProgram = "exec";

    /// <summary>F5 XC</summary>
    public const string F5Xc = "f5xc";

    /// <summary>FENO</summary>
    public const string Feno = "feno";

    /// <summary>Fornex</summary>
    public const string Fornex = "fornex";

    /// <summary>freemyip.com</summary>
    public const string FreeMyIp = "freemyip";

    /// <summary>FusionLayer NameSurfer</summary>
    public const string NameSurfer = "namesurfer";

    /// <summary>Gehirn</summary>
    public const string Gehirn = "gehirn";

    /// <summary>Gigahost.no</summary>
    public const string GigahostNo = "gigahostno";

    /// <summary>Gname</summary>
    public const string Gname = "gname";

    /// <summary>Gravity</summary>
    public const string Gravity = "gravity";

    /// <summary>Hosting.de</summary>
    public const string HostingDe = "hostingde";

    /// <summary>Hosting.nl</summary>
    public const string HostingNl = "hostingnl";

    /// <summary>Hosttech</summary>
    public const string Hosttech = "hosttech";

    /// <summary>HostUp</summary>
    public const string HostUp = "hostup";

    /// <summary>HTTP request</summary>
    public const string HttpReq = "httpreq";

    /// <summary>http.net</summary>
    public const string HttpNet = "httpnet";

    /// <summary>Huawei Cloud</summary>
    public const string HuaweiCloud = "huaweicloud";

    /// <summary>Hurricane Electric DNS</summary>
    public const string Hurricane = "hurricane";

    /// <summary>HyperOne</summary>
    public const string HyperOne = "hyperone";

    /// <summary>IBM Cloud (SoftLayer)</summary>
    public const string IbmCloud = "ibmcloud";

    /// <summary>IIJ DNS Platform Service</summary>
    public const string IijDpf = "iijdpf";

    /// <summary>Infoblox</summary>
    public const string Infoblox = "infoblox";

    /// <summary>Internet.bs</summary>
    public const string InternetBs = "internetbs";

    /// <summary>INWX</summary>
    public const string Inwx = "inwx";

    /// <summary>Ionos</summary>
    public const string Ionos = "ionos";

    /// <summary>Ionos Cloud</summary>
    public const string IonosCloud = "ionoscloud";

    /// <summary>IPv64</summary>
    public const string Ipv64 = "ipv64";

    /// <summary>ISPConfig 3</summary>
    public const string IspConfig = "ispconfig";

    /// <summary>ISPConfig 3 - Dynamic DNS (DDNS) Module</summary>
    public const string IspConfigDdns = "ispconfigddns";

    /// <summary>JD Cloud</summary>
    public const string JdCloud = "jdcloud";

    /// <summary>Joohoi's ACME-DNS</summary>
    public const string AcmeDns = "acmedns";

    /// <summary>Katapult</summary>
    public const string Katapult = "katapult";

    /// <summary>KeyHelp</summary>
    public const string KeyHelp = "keyhelp";

    /// <summary>Leaseweb</summary>
    public const string Leaseweb = "leaseweb";

    /// <summary>Liara</summary>
    public const string Liara = "liara";

    /// <summary>Lima-City</summary>
    public const string LimaCity = "limacity";

    /// <summary>Mail-in-a-Box</summary>
    public const string MailInABox = "mailinabox";

    /// <summary>ManageEngine CloudDNS</summary>
    public const string ManageEngine = "manageengine";

    /// <summary>Metaname</summary>
    public const string Metaname = "metaname";

    /// <summary>Metaregistrar</summary>
    public const string Metaregistrar = "metaregistrar";

    /// <summary>mijn.host</summary>
    public const string MijnHost = "mijnhost";

    /// <summary>Mittwald</summary>
    public const string Mittwald = "mittwald";

    /// <summary>myaddr.{tools,dev,io}</summary>
    public const string MyAddr = "myaddr";

    /// <summary>MyDNS.jp</summary>
    public const string MyDnsJp = "mydnsjp";

    /// <summary>Myra</summary>
    public const string Myra = "myra";

    /// <summary>MythicBeasts</summary>
    public const string MythicBeasts = "mythicbeasts";

    /// <summary>Name.com</summary>
    public const string NameDotCom = "namedotcom";

    /// <summary>Namesilo</summary>
    public const string NameSilo = "namesilo";

    /// <summary>NearlyFreeSpeech.NET</summary>
    public const string NearlyFreeSpeech = "nearlyfreespeech";

    /// <summary>NederHost</summary>
    public const string NederHost = "nederhost";

    /// <summary>Neodigit</summary>
    public const string Neodigit = "neodigit";

    /// <summary>Netlify</summary>
    public const string Netlify = "netlify";

    /// <summary>Netnod</summary>
    public const string Netnod = "netnod";

    /// <summary>NexDNS</summary>
    public const string NexDns = "nexdns";

    /// <summary>Ngenix</summary>
    public const string Ngenix = "ngenix";

    /// <summary>Nicmanager</summary>
    public const string Nicmanager = "nicmanager";

    /// <summary>NIFCloud</summary>
    public const string NifCloud = "nifcloud";

    /// <summary>Njalla</summary>
    public const string Njalla = "njalla";

    /// <summary>Nodion</summary>
    public const string Nodion = "nodion";

    /// <summary>Octenium</summary>
    public const string Octenium = "octenium";

    /// <summary>omg.lol</summary>
    public const string OmgLol = "omglol";

    /// <summary>Online.net</summary>
    public const string OnlineNet = "onlinenet";

    /// <summary>Open Telekom Cloud</summary>
    public const string OpenTelekomCloud = "otc";

    /// <summary>Openprovider</summary>
    public const string Openprovider = "openprovider";

    /// <summary>OpusDNS</summary>
    public const string OpusDns = "opusdns";

    /// <summary>plesk.com</summary>
    public const string Plesk = "plesk";

    /// <summary>PointDNS/PointHQ</summary>
    public const string PointDns = "pointdns";

    /// <summary>Poweradmin</summary>
    public const string PowerAdmin = "poweradmin";

    /// <summary>Rage4</summary>
    public const string Rage4 = "rage4";

    /// <summary>Rain Yun/雨云</summary>
    public const string RainYun = "rainyun";

    /// <summary>RcodeZero</summary>
    public const string RcodeZero = "rcodezero";

    /// <summary>reg.ru</summary>
    public const string RegRu = "regru";

    /// <summary>Regfish</summary>
    public const string Regfish = "regfish";

    /// <summary>RimuHosting</summary>
    public const string RimuHosting = "rimuhosting";

    /// <summary>RU CENTER</summary>
    public const string RuCenter = "nicru";

    /// <summary>Sakura Cloud</summary>
    public const string SakuraCloud = "sakuracloud";

    /// <summary>Scaleway</summary>
    public const string Scaleway = "scaleway";

    /// <summary>ScanNet</summary>
    public const string ScanNet = "scannet";

    /// <summary>Selectel</summary>
    public const string Selectel = "selectel";

    /// <summary>Selectel v2</summary>
    public const string SelectelV2 = "selectelv2";

    /// <summary>SelfHost.(de|eu)</summary>
    public const string SelfHostDe = "selfhostde";

    /// <summary>Servercow</summary>
    public const string Servercow = "servercow";

    /// <summary>Shellrent</summary>
    public const string Shellrent = "shellrent";

    /// <summary>Simply.com</summary>
    public const string Simply = "simply";

    /// <summary>Sonic</summary>
    public const string Sonic = "sonic";

    /// <summary>Spaceship</summary>
    public const string Spaceship = "spaceship";

    /// <summary>Syse</summary>
    public const string Syse = "syse";

    /// <summary>Technitium</summary>
    public const string Technitium = "technitium";

    /// <summary>Tele3</summary>
    public const string Tele3 = "tele3";

    /// <summary>Tencent EdgeOne</summary>
    public const string EdgeOne = "edgeone";

    /// <summary>Timeweb Cloud</summary>
    public const string TimewebCloud = "timewebcloud";

    /// <summary>TodayNIC/时代互联</summary>
    public const string TodayNic = "todaynic";

    /// <summary>UCloud</summary>
    public const string UCloud = "ucloud";

    /// <summary>Ultradns</summary>
    public const string UltraDns = "ultradns";

    /// <summary>United-Domains</summary>
    public const string UnitedDomains = "uniteddomains";

    /// <summary>Variomedia</summary>
    public const string Variomedia = "variomedia";

    /// <summary>Veesp</summary>
    public const string Veesp = "veesp";

    /// <summary>VegaDNS</summary>
    public const string VegaDns = "vegadns";

    /// <summary>Vercel</summary>
    public const string Vercel = "vercel";

    /// <summary>Versio.[nl|eu|uk]</summary>
    public const string Versio = "versio";

    /// <summary>VinylDNS</summary>
    public const string VinylDns = "vinyldns";

    /// <summary>Virtualname</summary>
    public const string Virtualname = "virtualname";

    /// <summary>VK Cloud</summary>
    public const string VkCloud = "vkcloud";

    /// <summary>Volcano Engine/火山引擎</summary>
    public const string VolcEngine = "volcengine";

    /// <summary>Vscale</summary>
    public const string Vscale = "vscale";

    /// <summary>Wannafind</summary>
    public const string Wannafind = "wannafind";

    /// <summary>Webglobe</summary>
    public const string Webglobe = "webglobe";

    /// <summary>webnames.ca</summary>
    public const string WebNamesCa = "webnamesca";

    /// <summary>webnames.ru</summary>
    public const string WebNamesRu = "webnamesru";

    /// <summary>Websupport</summary>
    public const string Websupport = "websupport";

    /// <summary>WEDOS</summary>
    public const string Wedos = "wedos";

    /// <summary>West.cn/西部数码</summary>
    public const string WestCn = "westcn";

    /// <summary>Xinnet</summary>
    public const string Xinnet = "xinnet";

    /// <summary>Yandex 360</summary>
    public const string Yandex360 = "yandex360";

    /// <summary>Yandex Cloud</summary>
    public const string YandexCloud = "yandexcloud";

    /// <summary>Zilore</summary>
    public const string Zilore = "zilore";

    /// <summary>Zone.ee</summary>
    public const string ZoneEe = "zoneee";

    /// <summary>ZoneEdit</summary>
    public const string ZoneEdit = "zoneedit";

    /// <summary>Zonomi</summary>
    public const string Zonomi = "zonomi";
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
        },
        new DnsProviderDescriptor
        {
            Id = Route53,
            LegoProvider = "route53",
            EnvPrefix = "AWS",
            DisplayName = "AWS Route 53",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/route53/",
            PermissionHintKey = "Text.Dns.Permission.Route53",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "AccessKeyId",
                    EnvVar = "AWS_ACCESS_KEY_ID",
                    LabelKey = "Text.Dns.Field.AccessKeyId",
                    PlaceholderKey = "Text.Dns.Placeholder.AccessKeyId"
                },
                new DnsProviderField
                {
                    Key = "SecretAccessKey",
                    EnvVar = "AWS_SECRET_ACCESS_KEY",
                    LabelKey = "Text.Dns.Field.SecretAccessKey",
                    PlaceholderKey = "Text.Dns.Placeholder.SecretAccessKey"
                },
                new DnsProviderField
                {
                    Key = "Region",
                    EnvVar = "AWS_REGION",
                    LabelKey = "Text.Dns.Field.Region",
                    PlaceholderKey = "Text.Dns.Placeholder.Region"
                },
                new DnsProviderField
                {
                    Key = "SessionToken",
                    EnvVar = "AWS_SESSION_TOKEN",
                    LabelKey = "Text.Dns.Field.SessionToken",
                    PlaceholderKey = "Text.Dns.Placeholder.Optional",
                    Required = false
                },
                new DnsProviderField
                {
                    Key = "HostedZoneId",
                    EnvVar = "AWS_HOSTED_ZONE_ID",
                    LabelKey = "Text.Dns.Field.HostedZoneId",
                    PlaceholderKey = "Text.Dns.Placeholder.Optional",
                    Required = false
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = AzureDns,
            LegoProvider = "azuredns",
            EnvPrefix = "AZURE",
            DisplayName = "Azure DNS",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/azuredns/",
            PermissionHintKey = "Text.Dns.Permission.AzureDns",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ClientId",
                    EnvVar = "AZURE_CLIENT_ID",
                    LabelKey = "Text.Dns.Field.ClientId",
                    PlaceholderKey = "Text.Dns.Placeholder.ClientId"
                },
                new DnsProviderField
                {
                    Key = "ClientSecret",
                    EnvVar = "AZURE_CLIENT_SECRET",
                    LabelKey = "Text.Dns.Field.ClientSecret",
                    PlaceholderKey = "Text.Dns.Placeholder.ClientSecret"
                },
                new DnsProviderField
                {
                    Key = "TenantId",
                    EnvVar = "AZURE_TENANT_ID",
                    LabelKey = "Text.Dns.Field.TenantId",
                    PlaceholderKey = "Text.Dns.Placeholder.TenantId"
                },
                new DnsProviderField
                {
                    Key = "SubscriptionId",
                    EnvVar = "AZURE_SUBSCRIPTION_ID",
                    LabelKey = "Text.Dns.Field.SubscriptionId",
                    PlaceholderKey = "Text.Dns.Placeholder.SubscriptionId"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = DigitalOcean,
            LegoProvider = "digitalocean",
            EnvPrefix = "DO",
            DisplayName = "DigitalOcean",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/digitalocean/",
            PermissionHintKey = "Text.Dns.Permission.DigitalOcean",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "AuthToken",
                    EnvVar = "DO_AUTH_TOKEN",
                    LabelKey = "Text.Dns.Field.AuthToken",
                    PlaceholderKey = "Text.Dns.Placeholder.AuthToken"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Gandi,
            LegoProvider = "gandi",
            EnvPrefix = "GANDI",
            DisplayName = "Gandi",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/gandi/",
            PermissionHintKey = "Text.Dns.Permission.Gandi",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "GANDI_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Namecheap,
            LegoProvider = "namecheap",
            EnvPrefix = "NAMECHEAP",
            DisplayName = "Namecheap",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/namecheap/",
            PermissionHintKey = "Text.Dns.Permission.Namecheap",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiUser",
                    EnvVar = "NAMECHEAP_API_USER",
                    LabelKey = "Text.Dns.Field.ApiUser",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiUser"
                },
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "NAMECHEAP_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = GoDaddy,
            LegoProvider = "godaddy",
            EnvPrefix = "GODADDY",
            DisplayName = "GoDaddy",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/godaddy/",
            PermissionHintKey = "Text.Dns.Permission.GoDaddy",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "GODADDY_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                },
                new DnsProviderField
                {
                    Key = "ApiSecret",
                    EnvVar = "GODADDY_API_SECRET",
                    LabelKey = "Text.Dns.Field.ApiSecret",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiSecret"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = GCloud,
            LegoProvider = "gcloud",
            EnvPrefix = "GCE",
            DisplayName = "Google Cloud DNS",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/gcloud/",
            PermissionHintKey = "Text.Dns.Permission.GCloud",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Project",
                    EnvVar = "GCE_PROJECT",
                    LabelKey = "Text.Dns.Field.Project",
                    PlaceholderKey = "Text.Dns.Placeholder.Project"
                },
                new DnsProviderField
                {
                    Key = "ServiceAccountFile",
                    EnvVar = "GCE_SERVICE_ACCOUNT_FILE",
                    LabelKey = "Text.Dns.Field.ServiceAccountFile",
                    PlaceholderKey = "Text.Dns.Placeholder.ServiceAccountFile"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Hetzner,
            LegoProvider = "hetzner",
            EnvPrefix = "HETZNER",
            DisplayName = "Hetzner",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/hetzner/",
            PermissionHintKey = "Text.Dns.Permission.Hetzner",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "HETZNER_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Vultr,
            LegoProvider = "vultr",
            EnvPrefix = "VULTR",
            DisplayName = "Vultr",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/vultr/",
            PermissionHintKey = "Text.Dns.Permission.Vultr",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "VULTR_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Linode,
            LegoProvider = "linode",
            EnvPrefix = "LINODE",
            DisplayName = "Linode",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/linode/",
            PermissionHintKey = "Text.Dns.Permission.Linode",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "LINODE_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = PowerDNS,
            LegoProvider = "pdns",
            EnvPrefix = "PDNS",
            DisplayName = "PowerDNS",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/pdns/",
            PermissionHintKey = "Text.Dns.Permission.PowerDNS",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiUrl",
                    EnvVar = "PDNS_API_URL",
                    LabelKey = "Text.Dns.Field.ApiUrl",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiUrl"
                },
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "PDNS_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
                // ===== 新增厂商描述符 =====

        new DnsProviderDescriptor
        {
            Id = Dynu,
            LegoProvider = "dynu",
            EnvPrefix = "DYNU",
            DisplayName = "Dynu",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/dynu/",
            PermissionHintKey = "Text.Dns.Permission.Dynu",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "DYNU_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = EasyDns,
            LegoProvider = "easydns",
            EnvPrefix = "EASYDNS",
            DisplayName = "EasyDNS",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/easydns/",
            PermissionHintKey = "Text.Dns.Permission.EasyDns",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Token",
                    EnvVar = "EASYDNS_TOKEN",
                    LabelKey = "Text.Dns.Field.Token",
                    PlaceholderKey = "Text.Dns.Placeholder.Token"
                },
                new DnsProviderField
                {
                    Key = "Key",
                    EnvVar = "EASYDNS_KEY",
                    LabelKey = "Text.Dns.Field.Key",
                    PlaceholderKey = "Text.Dns.Placeholder.Key"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = EdgeDns,
            LegoProvider = "edgedns",
            EnvPrefix = "AKAMAI",
            DisplayName = "Akamai EdgeDNS",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/edgedns/",
            PermissionHintKey = "Text.Dns.Permission.EdgeDns",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ClientToken",
                    EnvVar = "AKAMAI_CLIENT_TOKEN",
                    LabelKey = "Text.Dns.Field.ClientToken",
                    PlaceholderKey = "Text.Dns.Placeholder.ClientToken"
                },
                new DnsProviderField
                {
                    Key = "ClientSecret",
                    EnvVar = "AKAMAI_CLIENT_SECRET",
                    LabelKey = "Text.Dns.Field.ClientSecret",
                    PlaceholderKey = "Text.Dns.Placeholder.ClientSecret"
                },
                new DnsProviderField
                {
                    Key = "AccessToken",
                    EnvVar = "AKAMAI_ACCESS_TOKEN",
                    LabelKey = "Text.Dns.Field.AccessToken",
                    PlaceholderKey = "Text.Dns.Placeholder.AccessToken"
                },
                new DnsProviderField
                {
                    Key = "Host",
                    EnvVar = "AKAMAI_HOST",
                    LabelKey = "Text.Dns.Field.Host",
                    PlaceholderKey = "Text.Dns.Placeholder.Host"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Exoscale,
            LegoProvider = "exoscale",
            EnvPrefix = "EXOSCALE",
            DisplayName = "Exoscale",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/exoscale/",
            PermissionHintKey = "Text.Dns.Permission.Exoscale",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "EXOSCALE_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                },
                new DnsProviderField
                {
                    Key = "ApiSecret",
                    EnvVar = "EXOSCALE_API_SECRET",
                    LabelKey = "Text.Dns.Field.ApiSecret",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiSecret"
                },
                new DnsProviderField
                {
                    Key = "Endpoint",
                    EnvVar = "EXOSCALE_ENDPOINT",
                    LabelKey = "Text.Dns.Field.Endpoint",
                    PlaceholderKey = "Text.Dns.Placeholder.Optional",
                    Required = false
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = GCore,
            LegoProvider = "gcore",
            EnvPrefix = "GCORE",
            DisplayName = "G-Core",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/gcore/",
            PermissionHintKey = "Text.Dns.Permission.GCore",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "PermanentApiToken",
                    EnvVar = "GCORE_PERMANENT_API_TOKEN",
                    LabelKey = "Text.Dns.Field.PermanentApiToken",
                    PlaceholderKey = "Text.Dns.Placeholder.PermanentApiToken"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = GandiV5,
            LegoProvider = "gandiv5",
            EnvPrefix = "GANDIV5",
            DisplayName = "Gandi LiveDNS (v5)",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/gandiv5/",
            PermissionHintKey = "Text.Dns.Permission.GandiV5",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "PersonalAccessToken",
                    EnvVar = "GANDIV5_PERSONAL_ACCESS_TOKEN",
                    LabelKey = "Text.Dns.Field.PersonalAccessToken",
                    PlaceholderKey = "Text.Dns.Placeholder.PersonalAccessToken"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Glesys,
            LegoProvider = "glesys",
            EnvPrefix = "GLESYS",
            DisplayName = "Glesys",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/glesys/",
            PermissionHintKey = "Text.Dns.Permission.Glesys",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiUser",
                    EnvVar = "GLESYS_API_USER",
                    LabelKey = "Text.Dns.Field.ApiUser",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiUser"
                },
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "GLESYS_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Hostinger,
            LegoProvider = "hostinger",
            EnvPrefix = "HOSTINGER",
            DisplayName = "Hostinger",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/hostinger/",
            PermissionHintKey = "Text.Dns.Permission.Hostinger",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiToken",
                    EnvVar = "HOSTINGER_API_TOKEN",
                    LabelKey = "Text.Dns.Field.ApiToken",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiToken"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Infomaniak,
            LegoProvider = "infomaniak",
            EnvPrefix = "INFOMANIAK",
            DisplayName = "Infomaniak",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/infomaniak/",
            PermissionHintKey = "Text.Dns.Permission.Infomaniak",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "AccessToken",
                    EnvVar = "INFOMANIAK_ACCESS_TOKEN",
                    LabelKey = "Text.Dns.Field.AccessToken",
                    PlaceholderKey = "Text.Dns.Placeholder.AccessToken"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Joker,
            LegoProvider = "joker",
            EnvPrefix = "JOKER",
            DisplayName = "Joker",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/joker/",
            PermissionHintKey = "Text.Dns.Permission.Joker",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "JOKER_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "JOKER_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Lightsail,
            LegoProvider = "lightsail",
            EnvPrefix = "AWS",
            DisplayName = "Amazon Lightsail",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/lightsail/",
            PermissionHintKey = "Text.Dns.Permission.Lightsail",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "AccessKeyId",
                    EnvVar = "AWS_ACCESS_KEY_ID",
                    LabelKey = "Text.Dns.Field.AccessKeyId",
                    PlaceholderKey = "Text.Dns.Placeholder.AccessKeyId"
                },
                new DnsProviderField
                {
                    Key = "SecretAccessKey",
                    EnvVar = "AWS_SECRET_ACCESS_KEY",
                    LabelKey = "Text.Dns.Field.SecretAccessKey",
                    PlaceholderKey = "Text.Dns.Placeholder.SecretAccessKey"
                },
                new DnsProviderField
                {
                    Key = "SessionToken",
                    EnvVar = "AWS_SESSION_TOKEN",
                    LabelKey = "Text.Dns.Field.SessionToken",
                    PlaceholderKey = "Text.Dns.Placeholder.Optional",
                    Required = false
                },
                new DnsProviderField
                {
                    Key = "DnsZone",
                    EnvVar = "DNS_ZONE",
                    LabelKey = "Text.Dns.Field.DnsZone",
                    PlaceholderKey = "Text.Dns.Placeholder.DnsZone"
                },
                new DnsProviderField
                {
                    Key = "Region",
                    EnvVar = "LIGHTSAIL_REGION",
                    LabelKey = "Text.Dns.Field.Region",
                    PlaceholderKey = "Text.Dns.Placeholder.Optional",
                    Required = false
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = LiquidWeb,
            LegoProvider = "liquidweb",
            EnvPrefix = "LWAPI",
            DisplayName = "Liquid Web",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/liquidweb/",
            PermissionHintKey = "Text.Dns.Permission.LiquidWeb",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "LWAPI_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "LWAPI_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Loopia,
            LegoProvider = "loopia",
            EnvPrefix = "LOOPIA",
            DisplayName = "Loopia",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/loopia/",
            PermissionHintKey = "Text.Dns.Permission.Loopia",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiUser",
                    EnvVar = "LOOPIA_API_USER",
                    LabelKey = "Text.Dns.Field.ApiUser",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiUser"
                },
                new DnsProviderField
                {
                    Key = "ApiPassword",
                    EnvVar = "LOOPIA_API_PASSWORD",
                    LabelKey = "Text.Dns.Field.ApiPassword",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiPassword"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = LuaDns,
            LegoProvider = "luadns",
            EnvPrefix = "LUADNS",
            DisplayName = "LuaDNS",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/luadns/",
            PermissionHintKey = "Text.Dns.Permission.LuaDns",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiUsername",
                    EnvVar = "LUADNS_API_USERNAME",
                    LabelKey = "Text.Dns.Field.ApiUsername",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiUsername"
                },
                new DnsProviderField
                {
                    Key = "ApiToken",
                    EnvVar = "LUADNS_API_TOKEN",
                    LabelKey = "Text.Dns.Field.ApiToken",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiToken"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Netcup,
            LegoProvider = "netcup",
            EnvPrefix = "NETCUP",
            DisplayName = "Netcup",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/netcup/",
            PermissionHintKey = "Text.Dns.Permission.Netcup",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "NETCUP_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                },
                new DnsProviderField
                {
                    Key = "ApiPassword",
                    EnvVar = "NETCUP_API_PASSWORD",
                    LabelKey = "Text.Dns.Field.ApiPassword",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiPassword"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = NS1,
            LegoProvider = "ns1",
            EnvPrefix = "NS1",
            DisplayName = "NS1",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/ns1/",
            PermissionHintKey = "Text.Dns.Permission.NS1",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "NS1_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = OracleCloud,
            LegoProvider = "oraclecloud",
            EnvPrefix = "OCI",
            DisplayName = "Oracle Cloud",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/oraclecloud/",
            PermissionHintKey = "Text.Dns.Permission.OracleCloud",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ConfigFile",
                    EnvVar = "OCI_CONFIG_FILE",
                    LabelKey = "Text.Dns.Field.ConfigFile",
                    PlaceholderKey = "Text.Dns.Placeholder.ConfigFile",
                    Required = false
                },
                new DnsProviderField
                {
                    Key = "CompartmentId",
                    EnvVar = "OCI_COMPARTMENT_ID",
                    LabelKey = "Text.Dns.Field.CompartmentId",
                    PlaceholderKey = "Text.Dns.Placeholder.CompartmentId"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Ovh,
            LegoProvider = "ovh",
            EnvPrefix = "OVH",
            DisplayName = "OVH",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/ovh/",
            PermissionHintKey = "Text.Dns.Permission.Ovh",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Endpoint",
                    EnvVar = "OVH_ENDPOINT",
                    LabelKey = "Text.Dns.Field.Endpoint",
                    PlaceholderKey = "Text.Dns.Placeholder.Endpoint"
                },
                new DnsProviderField
                {
                    Key = "ApplicationKey",
                    EnvVar = "OVH_APPLICATION_KEY",
                    LabelKey = "Text.Dns.Field.ApplicationKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApplicationKey"
                },
                new DnsProviderField
                {
                    Key = "ApplicationSecret",
                    EnvVar = "OVH_APPLICATION_SECRET",
                    LabelKey = "Text.Dns.Field.ApplicationSecret",
                    PlaceholderKey = "Text.Dns.Placeholder.ApplicationSecret"
                },
                new DnsProviderField
                {
                    Key = "ConsumerKey",
                    EnvVar = "OVH_CONSUMER_KEY",
                    LabelKey = "Text.Dns.Field.ConsumerKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ConsumerKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Porkbun,
            LegoProvider = "porkbun",
            EnvPrefix = "PORKBUN",
            DisplayName = "Porkbun",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/porkbun/",
            PermissionHintKey = "Text.Dns.Permission.Porkbun",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "PORKBUN_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                },
                new DnsProviderField
                {
                    Key = "SecretApiKey",
                    EnvVar = "PORKBUN_SECRET_API_KEY",
                    LabelKey = "Text.Dns.Field.SecretApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.SecretApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Rackspace,
            LegoProvider = "rackspace",
            EnvPrefix = "RACKSPACE",
            DisplayName = "Rackspace",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/rackspace/",
            PermissionHintKey = "Text.Dns.Permission.Rackspace",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "User",
                    EnvVar = "RACKSPACE_USER",
                    LabelKey = "Text.Dns.Field.User",
                    PlaceholderKey = "Text.Dns.Placeholder.User"
                },
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "RACKSPACE_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = TransIp,
            LegoProvider = "transip",
            EnvPrefix = "TRANSIP",
            DisplayName = "TransIP",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/transip/",
            PermissionHintKey = "Text.Dns.Permission.TransIp",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "AccountName",
                    EnvVar = "TRANSIP_ACCOUNTNAME",
                    LabelKey = "Text.Dns.Field.AccountName",
                    PlaceholderKey = "Text.Dns.Placeholder.AccountName"
                },
                new DnsProviderField
                {
                    Key = "PrivateKeyPath",
                    EnvVar = "TRANSIP_PRIVATEKEYPATH",
                    LabelKey = "Text.Dns.Field.PrivateKeyPath",
                    PlaceholderKey = "Text.Dns.Placeholder.PrivateKeyPath"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Yandex,
            LegoProvider = "yandex",
            EnvPrefix = "YANDEX",
            DisplayName = "Yandex PDD",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/yandex/",
            PermissionHintKey = "Text.Dns.Permission.Yandex",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "PddToken",
                    EnvVar = "YANDEX_PDD_TOKEN",
                    LabelKey = "Text.Dns.Field.PddToken",
                    PlaceholderKey = "Text.Dns.Placeholder.PddToken"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = DnsUpdate,
            LegoProvider = "dnsupdate",
            EnvPrefix = "DNSUPDATE",
            DisplayName = "RFC2136 DNS Update",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/dnsupdate/",
            PermissionHintKey = "Text.Dns.Permission.DnsUpdate",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Nameserver",
                    EnvVar = "DNSUPDATE_NAMESERVER",
                    LabelKey = "Text.Dns.Field.Nameserver",
                    PlaceholderKey = "Text.Dns.Placeholder.Nameserver"
                },
                new DnsProviderField
                {
                    Key = "TsigKey",
                    EnvVar = "DNSUPDATE_TSIG_KEY",
                    LabelKey = "Text.Dns.Field.TsigKey",
                    PlaceholderKey = "Text.Dns.Placeholder.Optional",
                    Required = false
                },
                new DnsProviderField
                {
                    Key = "TsigSecret",
                    EnvVar = "DNSUPDATE_TSIG_SECRET",
                    LabelKey = "Text.Dns.Field.TsigSecret",
                    PlaceholderKey = "Text.Dns.Placeholder.Optional",
                    Required = false
                },
                new DnsProviderField
                {
                    Key = "TsigAlgorithm",
                    EnvVar = "DNSUPDATE_TSIG_ALGORITHM",
                    LabelKey = "Text.Dns.Field.TsigAlgorithm",
                    PlaceholderKey = "Text.Dns.Placeholder.Optional",
                    Required = false
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = OneCloudRu,
            LegoProvider = "onecloudru",
            EnvPrefix = "ONECLOUDRU",
            DisplayName = "1cloud.ru",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "onecloudru/",
            PermissionHintKey = "Text.Dns.Permission.OneCloudRu",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Token",
                    EnvVar = "ONECLOUDRU_TOKEN",
                    LabelKey = "Text.Dns.Field.Token",
                    PlaceholderKey = "Text.Dns.Placeholder.Token"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Com35,
            LegoProvider = "com35",
            EnvPrefix = "COM35",
            DisplayName = "35.com/三五互联",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "com35/",
            PermissionHintKey = "Text.Dns.Permission.Com35",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "COM35_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "COM35_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Dns51,
            LegoProvider = "dns51",
            EnvPrefix = "DNS51",
            DisplayName = "51DNS",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "dns51/",
            PermissionHintKey = "Text.Dns.Permission.Dns51",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "DNS51_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                },
                new DnsProviderField
                {
                    Key = "ApiSecret",
                    EnvVar = "DNS51_API_SECRET",
                    LabelKey = "Text.Dns.Field.ApiSecret",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiSecret"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Abion,
            LegoProvider = "abion",
            EnvPrefix = "ABION",
            DisplayName = "Abion",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "abion/",
            PermissionHintKey = "Text.Dns.Permission.Abion",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "ABION_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Active24,
            LegoProvider = "active24",
            EnvPrefix = "ACTIVE24",
            DisplayName = "Active24",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "active24/",
            PermissionHintKey = "Text.Dns.Permission.Active24",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "ACTIVE24_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                },
                new DnsProviderField
                {
                    Key = "Secret",
                    EnvVar = "ACTIVE24_SECRET",
                    LabelKey = "Text.Dns.Field.Secret",
                    PlaceholderKey = "Text.Dns.Placeholder.Secret"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = AliEsa,
            LegoProvider = "aliesa",
            EnvPrefix = "ALIESA",
            DisplayName = "AlibabaCloud ESA",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "aliesa/",
            PermissionHintKey = "Text.Dns.Permission.AliEsa",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "RamRole",
                    EnvVar = "ALIESA_RAM_ROLE",
                    LabelKey = "Text.Dns.Field.RamRole",
                    PlaceholderKey = "Text.Dns.Placeholder.RamRole",
                    Required = false
                },
                new DnsProviderField
                {
                    Key = "AccessKeyId",
                    EnvVar = "ALIESA_ACCESS_KEY",
                    LabelKey = "Text.Dns.Field.AccessKeyId",
                    PlaceholderKey = "Text.Dns.Placeholder.AccessKeyId"
                },
                new DnsProviderField
                {
                    Key = "AccessKeySecret",
                    EnvVar = "ALIESA_SECRET_KEY",
                    LabelKey = "Text.Dns.Field.AccessKeySecret",
                    PlaceholderKey = "Text.Dns.Placeholder.AccessKeySecret"
                },
                new DnsProviderField
                {
                    Key = "SecurityToken",
                    EnvVar = "ALIESA_SECURITY_TOKEN",
                    LabelKey = "Text.Dns.Field.SecurityToken",
                    PlaceholderKey = "Text.Dns.Placeholder.SecurityToken",
                    Required = false
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = AllInkl,
            LegoProvider = "allinkl",
            EnvPrefix = "ALL_INKL",
            DisplayName = "all-inkl",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "allinkl/",
            PermissionHintKey = "Text.Dns.Permission.AllInkl",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Login",
                    EnvVar = "ALL_INKL_LOGIN",
                    LabelKey = "Text.Dns.Field.Login",
                    PlaceholderKey = "Text.Dns.Placeholder.Login"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "ALL_INKL_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = AlwaysData,
            LegoProvider = "alwaysdata",
            EnvPrefix = "ALWAYSDATA",
            DisplayName = "Alwaysdata",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "alwaysdata/",
            PermissionHintKey = "Text.Dns.Permission.AlwaysData",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "ALWAYSDATA_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Anexia,
            LegoProvider = "anexia",
            EnvPrefix = "ANEXIA",
            DisplayName = "Anexia CloudDNS",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "anexia/",
            PermissionHintKey = "Text.Dns.Permission.Anexia",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Token",
                    EnvVar = "ANEXIA_TOKEN",
                    LabelKey = "Text.Dns.Field.Token",
                    PlaceholderKey = "Text.Dns.Placeholder.Token"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = SafeDns,
            LegoProvider = "safedns",
            EnvPrefix = "SAFEDNS",
            DisplayName = "ANS SafeDNS",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "safedns/",
            PermissionHintKey = "Text.Dns.Permission.SafeDns",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "AuthToken",
                    EnvVar = "SAFEDNS_AUTH_TOKEN",
                    LabelKey = "Text.Dns.Field.AuthToken",
                    PlaceholderKey = "Text.Dns.Placeholder.AuthToken"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = ArtFiles,
            LegoProvider = "artfiles",
            EnvPrefix = "ARTFILES",
            DisplayName = "ArtFiles",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "artfiles/",
            PermissionHintKey = "Text.Dns.Permission.ArtFiles",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "ARTFILES_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "ARTFILES_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = ArvanCloud,
            LegoProvider = "arvancloud",
            EnvPrefix = "ARVANCLOUD",
            DisplayName = "ArvanCloud",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "arvancloud/",
            PermissionHintKey = "Text.Dns.Permission.ArvanCloud",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "ARVANCLOUD_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = AuroraDns,
            LegoProvider = "auroradns",
            EnvPrefix = "AURORA",
            DisplayName = "Aurora DNS",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "auroradns/",
            PermissionHintKey = "Text.Dns.Permission.AuroraDns",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "AURORA_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                },
                new DnsProviderField
                {
                    Key = "Secret",
                    EnvVar = "AURORA_SECRET",
                    LabelKey = "Text.Dns.Field.Secret",
                    PlaceholderKey = "Text.Dns.Placeholder.Secret"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = AutoDns,
            LegoProvider = "autodns",
            EnvPrefix = "AUTODNS",
            DisplayName = "Autodns",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "autodns/",
            PermissionHintKey = "Text.Dns.Permission.AutoDns",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiUser",
                    EnvVar = "AUTODNS_API_USER",
                    LabelKey = "Text.Dns.Field.ApiUser",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiUser"
                },
                new DnsProviderField
                {
                    Key = "ApiPassword",
                    EnvVar = "AUTODNS_API_PASSWORD",
                    LabelKey = "Text.Dns.Field.ApiPassword",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiPassword"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = AxelName,
            LegoProvider = "axelname",
            EnvPrefix = "AXELNAME",
            DisplayName = "Axelname",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "axelname/",
            PermissionHintKey = "Text.Dns.Permission.AxelName",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Nickname",
                    EnvVar = "AXELNAME_NICKNAME",
                    LabelKey = "Text.Dns.Field.Nickname",
                    PlaceholderKey = "Text.Dns.Placeholder.Nickname"
                },
                new DnsProviderField
                {
                    Key = "Token",
                    EnvVar = "AXELNAME_TOKEN",
                    LabelKey = "Text.Dns.Field.Token",
                    PlaceholderKey = "Text.Dns.Placeholder.Token"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Azion,
            LegoProvider = "azion",
            EnvPrefix = "AZION",
            DisplayName = "Azion",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "azion/",
            PermissionHintKey = "Text.Dns.Permission.Azion",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "PersonalToken",
                    EnvVar = "AZION_PERSONAL_TOKEN",
                    LabelKey = "Text.Dns.Field.PersonalToken",
                    PlaceholderKey = "Text.Dns.Placeholder.PersonalToken"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = BaiduCloud,
            LegoProvider = "baiducloud",
            EnvPrefix = "BAIDUCLOUD",
            DisplayName = "Baidu Cloud",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "baiducloud/",
            PermissionHintKey = "Text.Dns.Permission.BaiduCloud",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "AccessKeyId",
                    EnvVar = "BAIDUCLOUD_ACCESS_KEY_ID",
                    LabelKey = "Text.Dns.Field.AccessKeyId",
                    PlaceholderKey = "Text.Dns.Placeholder.AccessKeyId"
                },
                new DnsProviderField
                {
                    Key = "SecretAccessKey",
                    EnvVar = "BAIDUCLOUD_SECRET_ACCESS_KEY",
                    LabelKey = "Text.Dns.Field.SecretAccessKey",
                    PlaceholderKey = "Text.Dns.Placeholder.SecretAccessKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Beget,
            LegoProvider = "beget",
            EnvPrefix = "BEGET",
            DisplayName = "Beget.com",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "beget/",
            PermissionHintKey = "Text.Dns.Permission.Beget",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "BEGET_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "BEGET_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = BinaryLane,
            LegoProvider = "binarylane",
            EnvPrefix = "BINARYLANE",
            DisplayName = "Binary Lane",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "binarylane/",
            PermissionHintKey = "Text.Dns.Permission.BinaryLane",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiToken",
                    EnvVar = "BINARYLANE_API_TOKEN",
                    LabelKey = "Text.Dns.Field.ApiToken",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiToken"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Bindman,
            LegoProvider = "bindman",
            EnvPrefix = "BINDMAN",
            DisplayName = "Bindman",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "bindman/",
            PermissionHintKey = "Text.Dns.Permission.Bindman",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ManagerAddress",
                    EnvVar = "BINDMAN_MANAGER_ADDRESS",
                    LabelKey = "Text.Dns.Field.ManagerAddress",
                    PlaceholderKey = "Text.Dns.Placeholder.ManagerAddress"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Bluecat,
            LegoProvider = "bluecat",
            EnvPrefix = "BLUECAT",
            DisplayName = "Bluecat",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "bluecat/",
            PermissionHintKey = "Text.Dns.Permission.Bluecat",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ServerUrl",
                    EnvVar = "BLUECAT_SERVER_URL",
                    LabelKey = "Text.Dns.Field.ServerUrl",
                    PlaceholderKey = "Text.Dns.Placeholder.ServerUrl"
                },
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "BLUECAT_USER_NAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "BLUECAT_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                },
                new DnsProviderField
                {
                    Key = "ConfigName",
                    EnvVar = "BLUECAT_CONFIG_NAME",
                    LabelKey = "Text.Dns.Field.ConfigName",
                    PlaceholderKey = "Text.Dns.Placeholder.ConfigName"
                },
                new DnsProviderField
                {
                    Key = "DnsView",
                    EnvVar = "BLUECAT_DNS_VIEW",
                    LabelKey = "Text.Dns.Field.DnsView",
                    PlaceholderKey = "Text.Dns.Placeholder.DnsView"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = BluecatV2,
            LegoProvider = "bluecatv2",
            EnvPrefix = "BLUECATV2",
            DisplayName = "Bluecat v2",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "bluecatv2/",
            PermissionHintKey = "Text.Dns.Permission.BluecatV2",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ServerUrl",
                    EnvVar = "BLUECATV2_SERVER_URL",
                    LabelKey = "Text.Dns.Field.ServerUrl",
                    PlaceholderKey = "Text.Dns.Placeholder.ServerUrl"
                },
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "BLUECATV2_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "BLUECATV2_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                },
                new DnsProviderField
                {
                    Key = "ConfigName",
                    EnvVar = "BLUECATV2_CONFIG_NAME",
                    LabelKey = "Text.Dns.Field.ConfigName",
                    PlaceholderKey = "Text.Dns.Placeholder.ConfigName"
                },
                new DnsProviderField
                {
                    Key = "ViewName",
                    EnvVar = "BLUECATV2_VIEW_NAME",
                    LabelKey = "Text.Dns.Field.ViewName",
                    PlaceholderKey = "Text.Dns.Placeholder.ViewName"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = BookMyName,
            LegoProvider = "bookmyname",
            EnvPrefix = "BOOKMYNAME",
            DisplayName = "BookMyName",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "bookmyname/",
            PermissionHintKey = "Text.Dns.Permission.BookMyName",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "BOOKMYNAME_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "BOOKMYNAME_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Bunny,
            LegoProvider = "bunny",
            EnvPrefix = "BUNNY",
            DisplayName = "Bunny",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "bunny/",
            PermissionHintKey = "Text.Dns.Permission.Bunny",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "BUNNY_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Checkdomain,
            LegoProvider = "checkdomain",
            EnvPrefix = "CHECKDOMAIN",
            DisplayName = "Checkdomain",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "checkdomain/",
            PermissionHintKey = "Text.Dns.Permission.Checkdomain",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Token",
                    EnvVar = "CHECKDOMAIN_TOKEN",
                    LabelKey = "Text.Dns.Field.Token",
                    PlaceholderKey = "Text.Dns.Placeholder.Token"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Civo,
            LegoProvider = "civo",
            EnvPrefix = "CIVO",
            DisplayName = "Civo",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "civo/",
            PermissionHintKey = "Text.Dns.Permission.Civo",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Token",
                    EnvVar = "CIVO_TOKEN",
                    LabelKey = "Text.Dns.Field.Token",
                    PlaceholderKey = "Text.Dns.Placeholder.Token"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = CloudRu,
            LegoProvider = "cloudru",
            EnvPrefix = "CLOUDRU",
            DisplayName = "Cloud.ru",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "cloudru/",
            PermissionHintKey = "Text.Dns.Permission.CloudRu",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ServiceInstanceId",
                    EnvVar = "CLOUDRU_SERVICE_INSTANCE_ID",
                    LabelKey = "Text.Dns.Field.ServiceInstanceId",
                    PlaceholderKey = "Text.Dns.Placeholder.ServiceInstanceId"
                },
                new DnsProviderField
                {
                    Key = "KeyId",
                    EnvVar = "CLOUDRU_KEY_ID",
                    LabelKey = "Text.Dns.Field.KeyId",
                    PlaceholderKey = "Text.Dns.Placeholder.KeyId"
                },
                new DnsProviderField
                {
                    Key = "Secret",
                    EnvVar = "CLOUDRU_SECRET",
                    LabelKey = "Text.Dns.Field.Secret",
                    PlaceholderKey = "Text.Dns.Placeholder.Secret"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = CloudDns,
            LegoProvider = "clouddns",
            EnvPrefix = "CLOUDDNS",
            DisplayName = "CloudDNS",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "clouddns/",
            PermissionHintKey = "Text.Dns.Permission.CloudDns",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ClientId",
                    EnvVar = "CLOUDDNS_CLIENT_ID",
                    LabelKey = "Text.Dns.Field.ClientId",
                    PlaceholderKey = "Text.Dns.Placeholder.ClientId"
                },
                new DnsProviderField
                {
                    Key = "Email",
                    EnvVar = "CLOUDDNS_EMAIL",
                    LabelKey = "Text.Dns.Field.Email",
                    PlaceholderKey = "Text.Dns.Placeholder.Email"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "CLOUDDNS_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = ClouDns,
            LegoProvider = "cloudns",
            EnvPrefix = "CLOUDNS",
            DisplayName = "ClouDNS",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "cloudns/",
            PermissionHintKey = "Text.Dns.Permission.ClouDns",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "AuthId",
                    EnvVar = "CLOUDNS_AUTH_ID",
                    LabelKey = "Text.Dns.Field.AuthId",
                    PlaceholderKey = "Text.Dns.Placeholder.AuthId",
                    Required = false
                },
                new DnsProviderField
                {
                    Key = "AuthPassword",
                    EnvVar = "CLOUDNS_AUTH_PASSWORD",
                    LabelKey = "Text.Dns.Field.AuthPassword",
                    PlaceholderKey = "Text.Dns.Placeholder.AuthPassword"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Connbyte,
            LegoProvider = "connbyte",
            EnvPrefix = "CONNBYTE",
            DisplayName = "Connbyte",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "connbyte/",
            PermissionHintKey = "Text.Dns.Permission.Connbyte",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Token",
                    EnvVar = "CONNBYTE_TOKEN",
                    LabelKey = "Text.Dns.Field.Token",
                    PlaceholderKey = "Text.Dns.Placeholder.Token"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = ConoHa,
            LegoProvider = "conoha",
            EnvPrefix = "CONOHA",
            DisplayName = "ConoHa v2",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "conoha/",
            PermissionHintKey = "Text.Dns.Permission.ConoHa",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "TenantId",
                    EnvVar = "CONOHA_TENANT_ID",
                    LabelKey = "Text.Dns.Field.TenantId",
                    PlaceholderKey = "Text.Dns.Placeholder.TenantId"
                },
                new DnsProviderField
                {
                    Key = "ApiUsername",
                    EnvVar = "CONOHA_API_USERNAME",
                    LabelKey = "Text.Dns.Field.ApiUsername",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiUsername"
                },
                new DnsProviderField
                {
                    Key = "ApiPassword",
                    EnvVar = "CONOHA_API_PASSWORD",
                    LabelKey = "Text.Dns.Field.ApiPassword",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiPassword"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = ConoHaV3,
            LegoProvider = "conohav3",
            EnvPrefix = "CONOHAV3",
            DisplayName = "ConoHa v3",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "conohav3/",
            PermissionHintKey = "Text.Dns.Permission.ConoHaV3",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "TenantId",
                    EnvVar = "CONOHAV3_TENANT_ID",
                    LabelKey = "Text.Dns.Field.TenantId",
                    PlaceholderKey = "Text.Dns.Placeholder.TenantId"
                },
                new DnsProviderField
                {
                    Key = "ApiUserId",
                    EnvVar = "CONOHAV3_API_USER_ID",
                    LabelKey = "Text.Dns.Field.ApiUserId",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiUserId"
                },
                new DnsProviderField
                {
                    Key = "ApiPassword",
                    EnvVar = "CONOHAV3_API_PASSWORD",
                    LabelKey = "Text.Dns.Field.ApiPassword",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiPassword"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Constellix,
            LegoProvider = "constellix",
            EnvPrefix = "CONSTELLIX",
            DisplayName = "Constellix",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "constellix/",
            PermissionHintKey = "Text.Dns.Permission.Constellix",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "CONSTELLIX_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                },
                new DnsProviderField
                {
                    Key = "SecretKey",
                    EnvVar = "CONSTELLIX_SECRET_KEY",
                    LabelKey = "Text.Dns.Field.SecretKey",
                    PlaceholderKey = "Text.Dns.Placeholder.SecretKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = CoreNetworks,
            LegoProvider = "corenetworks",
            EnvPrefix = "CORENETWORKS",
            DisplayName = "Core-Networks",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "corenetworks/",
            PermissionHintKey = "Text.Dns.Permission.CoreNetworks",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Login",
                    EnvVar = "CORENETWORKS_LOGIN",
                    LabelKey = "Text.Dns.Field.Login",
                    PlaceholderKey = "Text.Dns.Placeholder.Login"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "CORENETWORKS_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = CPanel,
            LegoProvider = "cpanel",
            EnvPrefix = "CPANEL",
            DisplayName = "CPanel/WHM",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "cpanel/",
            PermissionHintKey = "Text.Dns.Permission.CPanel",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "CPANEL_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Token",
                    EnvVar = "CPANEL_TOKEN",
                    LabelKey = "Text.Dns.Field.Token",
                    PlaceholderKey = "Text.Dns.Placeholder.Token"
                },
                new DnsProviderField
                {
                    Key = "BaseUrl",
                    EnvVar = "CPANEL_BASE_URL",
                    LabelKey = "Text.Dns.Field.BaseUrl",
                    PlaceholderKey = "Text.Dns.Placeholder.BaseUrl"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Curanet,
            LegoProvider = "curanet",
            EnvPrefix = "CURANET",
            DisplayName = "Curanet",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "curanet/",
            PermissionHintKey = "Text.Dns.Permission.Curanet",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "CURANET_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Czechia,
            LegoProvider = "czechia",
            EnvPrefix = "CZECHIA",
            DisplayName = "Czechia",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "czechia/",
            PermissionHintKey = "Text.Dns.Permission.Czechia",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Token",
                    EnvVar = "CZECHIA_TOKEN",
                    LabelKey = "Text.Dns.Field.Token",
                    PlaceholderKey = "Text.Dns.Placeholder.Token"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = DanDomain,
            LegoProvider = "dandomain",
            EnvPrefix = "DANDOMAIN",
            DisplayName = "DanDomain",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "dandomain/",
            PermissionHintKey = "Text.Dns.Permission.DanDomain",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "DANDOMAIN_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = DDnss,
            LegoProvider = "ddnss",
            EnvPrefix = "DDNSS",
            DisplayName = "DDnss (DynDNS Service)",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "ddnss/",
            PermissionHintKey = "Text.Dns.Permission.DDnss",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Key",
                    EnvVar = "DDNSS_KEY",
                    LabelKey = "Text.Dns.Field.Key",
                    PlaceholderKey = "Text.Dns.Placeholder.Key"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Derak,
            LegoProvider = "derak",
            EnvPrefix = "DERAK",
            DisplayName = "Derak Cloud",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "derak/",
            PermissionHintKey = "Text.Dns.Permission.Derak",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "DERAK_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = DeSec,
            LegoProvider = "desec",
            EnvPrefix = "DESEC",
            DisplayName = "deSEC.io",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "desec/",
            PermissionHintKey = "Text.Dns.Permission.DeSec",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Token",
                    EnvVar = "DESEC_TOKEN",
                    LabelKey = "Text.Dns.Field.Token",
                    PlaceholderKey = "Text.Dns.Placeholder.Token"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Designate,
            LegoProvider = "designate",
            EnvPrefix = "DESIGNATE",
            DisplayName = "Designate DNSaaS for Openstack",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "designate/",
            PermissionHintKey = "Text.Dns.Permission.Designate",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "AuthUrl",
                    EnvVar = "OS_AUTH_URL",
                    LabelKey = "Text.Dns.Field.AuthUrl",
                    PlaceholderKey = "Text.Dns.Placeholder.AuthUrl",
                    Required = false
                },
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "OS_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username",
                    Required = false
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "OS_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password",
                    Required = false
                },
                new DnsProviderField
                {
                    Key = "UserId",
                    EnvVar = "OS_USER_ID",
                    LabelKey = "Text.Dns.Field.UserId",
                    PlaceholderKey = "Text.Dns.Placeholder.UserId",
                    Required = false
                },
                new DnsProviderField
                {
                    Key = "ApplicationCredentialId",
                    EnvVar = "OS_APPLICATION_CREDENTIAL_ID",
                    LabelKey = "Text.Dns.Field.ApplicationCredentialId",
                    PlaceholderKey = "Text.Dns.Placeholder.ApplicationCredentialId",
                    Required = false
                },
                new DnsProviderField
                {
                    Key = "ApplicationCredentialName",
                    EnvVar = "OS_APPLICATION_CREDENTIAL_NAME",
                    LabelKey = "Text.Dns.Field.ApplicationCredentialName",
                    PlaceholderKey = "Text.Dns.Placeholder.ApplicationCredentialName",
                    Required = false
                },
                new DnsProviderField
                {
                    Key = "ApplicationCredentialSecret",
                    EnvVar = "OS_APPLICATION_CREDENTIAL_SECRET",
                    LabelKey = "Text.Dns.Field.ApplicationCredentialSecret",
                    PlaceholderKey = "Text.Dns.Placeholder.ApplicationCredentialSecret",
                    Required = false
                },
                new DnsProviderField
                {
                    Key = "ProjectName",
                    EnvVar = "OS_PROJECT_NAME",
                    LabelKey = "Text.Dns.Field.ProjectName",
                    PlaceholderKey = "Text.Dns.Placeholder.ProjectName",
                    Required = false
                },
                new DnsProviderField
                {
                    Key = "RegionName",
                    EnvVar = "OS_REGION_NAME",
                    LabelKey = "Text.Dns.Field.RegionName",
                    PlaceholderKey = "Text.Dns.Placeholder.RegionName",
                    Required = false
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Dinahosting,
            LegoProvider = "dinahosting",
            EnvPrefix = "DINAHOSTING",
            DisplayName = "Dinahosting",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "dinahosting/",
            PermissionHintKey = "Text.Dns.Permission.Dinahosting",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "DINAHOSTING_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "DINAHOSTING_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = DirectAdmin,
            LegoProvider = "directadmin",
            EnvPrefix = "DIRECTADMIN",
            DisplayName = "DirectAdmin",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "directadmin/",
            PermissionHintKey = "Text.Dns.Permission.DirectAdmin",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiUrl",
                    EnvVar = "DIRECTADMIN_API_URL",
                    LabelKey = "Text.Dns.Field.ApiUrl",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiUrl"
                },
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "DIRECTADMIN_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "DIRECTADMIN_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = DnsMadeEasy,
            LegoProvider = "dnsmadeeasy",
            EnvPrefix = "DNSMADEEASY",
            DisplayName = "DNS Made Easy",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "dnsmadeeasy/",
            PermissionHintKey = "Text.Dns.Permission.DnsMadeEasy",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "DNSMADEEASY_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                },
                new DnsProviderField
                {
                    Key = "ApiSecret",
                    EnvVar = "DNSMADEEASY_API_SECRET",
                    LabelKey = "Text.Dns.Field.ApiSecret",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiSecret"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = DnsLa,
            LegoProvider = "dnsla",
            EnvPrefix = "DNSLA",
            DisplayName = "dns.la",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "dnsla/",
            PermissionHintKey = "Text.Dns.Permission.DnsLa",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiId",
                    EnvVar = "DNSLA_API_ID",
                    LabelKey = "Text.Dns.Field.ApiId",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiId"
                },
                new DnsProviderField
                {
                    Key = "ApiSecret",
                    EnvVar = "DNSLA_API_SECRET",
                    LabelKey = "Text.Dns.Field.ApiSecret",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiSecret"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = DnsServices,
            LegoProvider = "dnsservices",
            EnvPrefix = "DNSSERVICES",
            DisplayName = "DNS.services",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "dnsservices/",
            PermissionHintKey = "Text.Dns.Permission.DnsServices",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "DNSSERVICES_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "DNSSERVICES_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = DnsCale,
            LegoProvider = "dnscale",
            EnvPrefix = "DNSCALE",
            DisplayName = "DNScale",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "dnscale/",
            PermissionHintKey = "Text.Dns.Permission.DnsCale",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiToken",
                    EnvVar = "DNSCALE_API_TOKEN",
                    LabelKey = "Text.Dns.Field.ApiToken",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiToken"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = DnsExit,
            LegoProvider = "dnsexit",
            EnvPrefix = "DNSEXIT",
            DisplayName = "DNSExit",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "dnsexit/",
            PermissionHintKey = "Text.Dns.Permission.DnsExit",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "DNSEXIT_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = DnsHomeDe,
            LegoProvider = "dnshomede",
            EnvPrefix = "DNSHOMEDE",
            DisplayName = "dnsHome.de",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "dnshomede/",
            PermissionHintKey = "Text.Dns.Permission.DnsHomeDe",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Credentials",
                    EnvVar = "DNSHOMEDE_CREDENTIALS",
                    LabelKey = "Text.Dns.Field.Credentials",
                    PlaceholderKey = "Text.Dns.Placeholder.Credentials"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = DnsSimple,
            LegoProvider = "dnsimple",
            EnvPrefix = "DNSIMPLE",
            DisplayName = "DNSimple",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "dnsimple/",
            PermissionHintKey = "Text.Dns.Permission.DnsSimple",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "OauthToken",
                    EnvVar = "DNSIMPLE_OAUTH_TOKEN",
                    LabelKey = "Text.Dns.Field.OauthToken",
                    PlaceholderKey = "Text.Dns.Placeholder.OauthToken",
                    Required = false
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = DnsMint,
            LegoProvider = "dnsmint",
            EnvPrefix = "DNSMINT",
            DisplayName = "DNSMint",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "dnsmint/",
            PermissionHintKey = "Text.Dns.Permission.DnsMint",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "DNSMINT_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = DomainOffensive,
            LegoProvider = "dode",
            EnvPrefix = "DODE",
            DisplayName = "Domain Offensive (do.de)",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "dode/",
            PermissionHintKey = "Text.Dns.Permission.DomainOffensive",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Token",
                    EnvVar = "DODE_TOKEN",
                    LabelKey = "Text.Dns.Field.Token",
                    PlaceholderKey = "Text.Dns.Placeholder.Token"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Domeneshop,
            LegoProvider = "domeneshop",
            EnvPrefix = "DOMENESHOP",
            DisplayName = "Domeneshop",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "domeneshop/",
            PermissionHintKey = "Text.Dns.Permission.Domeneshop",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiToken",
                    EnvVar = "DOMENESHOP_API_TOKEN",
                    LabelKey = "Text.Dns.Field.ApiToken",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiToken"
                },
                new DnsProviderField
                {
                    Key = "ApiSecret",
                    EnvVar = "DOMENESHOP_API_SECRET",
                    LabelKey = "Text.Dns.Field.ApiSecret",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiSecret"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = DreamHost,
            LegoProvider = "dreamhost",
            EnvPrefix = "DREAMHOST",
            DisplayName = "DreamHost",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "dreamhost/",
            PermissionHintKey = "Text.Dns.Permission.DreamHost",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "DREAMHOST_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = DuckDns,
            LegoProvider = "duckdns",
            EnvPrefix = "DUCKDNS",
            DisplayName = "Duck DNS",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "duckdns/",
            PermissionHintKey = "Text.Dns.Permission.DuckDns",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Token",
                    EnvVar = "DUCKDNS_TOKEN",
                    LabelKey = "Text.Dns.Field.Token",
                    PlaceholderKey = "Text.Dns.Placeholder.Token"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Dyn,
            LegoProvider = "dyn",
            EnvPrefix = "DYN",
            DisplayName = "Dyn",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "dyn/",
            PermissionHintKey = "Text.Dns.Permission.Dyn",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "CustomerName",
                    EnvVar = "DYN_CUSTOMER_NAME",
                    LabelKey = "Text.Dns.Field.CustomerName",
                    PlaceholderKey = "Text.Dns.Placeholder.CustomerName"
                },
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "DYN_USER_NAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "DYN_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Dynadot,
            LegoProvider = "dynadot",
            EnvPrefix = "DYNADOT",
            DisplayName = "Dynadot",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "dynadot/",
            PermissionHintKey = "Text.Dns.Permission.Dynadot",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "DYNADOT_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                },
                new DnsProviderField
                {
                    Key = "ApiSecret",
                    EnvVar = "DYNADOT_API_SECRET",
                    LabelKey = "Text.Dns.Field.ApiSecret",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiSecret"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = DynDnsFree,
            LegoProvider = "dyndnsfree",
            EnvPrefix = "DYNDNSFREE",
            DisplayName = "DynDnsFree.de",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "dyndnsfree/",
            PermissionHintKey = "Text.Dns.Permission.DynDnsFree",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "DYNDNSFREE_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "DYNDNSFREE_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = EdgeCenter,
            LegoProvider = "edgecenter",
            EnvPrefix = "EDGECENTER",
            DisplayName = "EdgeCenter",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "edgecenter/",
            PermissionHintKey = "Text.Dns.Permission.EdgeCenter",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "PermanentApiToken",
                    EnvVar = "EDGECENTER_PERMANENT_API_TOKEN",
                    LabelKey = "Text.Dns.Field.PermanentApiToken",
                    PlaceholderKey = "Text.Dns.Placeholder.PermanentApiToken"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = EfficientIp,
            LegoProvider = "efficientip",
            EnvPrefix = "EFFICIENTIP",
            DisplayName = "Efficient IP",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "efficientip/",
            PermissionHintKey = "Text.Dns.Permission.EfficientIp",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "EFFICIENTIP_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "EFFICIENTIP_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                },
                new DnsProviderField
                {
                    Key = "Hostname",
                    EnvVar = "EFFICIENTIP_HOSTNAME",
                    LabelKey = "Text.Dns.Field.Hostname",
                    PlaceholderKey = "Text.Dns.Placeholder.Hostname"
                },
                new DnsProviderField
                {
                    Key = "DnsName",
                    EnvVar = "EFFICIENTIP_DNS_NAME",
                    LabelKey = "Text.Dns.Field.DnsName",
                    PlaceholderKey = "Text.Dns.Placeholder.DnsName"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Epik,
            LegoProvider = "epik",
            EnvPrefix = "EPIK",
            DisplayName = "Epik",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "epik/",
            PermissionHintKey = "Text.Dns.Permission.Epik",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Signature",
                    EnvVar = "EPIK_SIGNATURE",
                    LabelKey = "Text.Dns.Field.Signature",
                    PlaceholderKey = "Text.Dns.Placeholder.Signature"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = EuroDns,
            LegoProvider = "eurodns",
            EnvPrefix = "EURODNS",
            DisplayName = "EuroDNS",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "eurodns/",
            PermissionHintKey = "Text.Dns.Permission.EuroDns",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "AppId",
                    EnvVar = "EURODNS_APP_ID",
                    LabelKey = "Text.Dns.Field.AppId",
                    PlaceholderKey = "Text.Dns.Placeholder.AppId"
                },
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "EURODNS_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = EuServ,
            LegoProvider = "euserv",
            EnvPrefix = "EUSERV",
            DisplayName = "EUserv",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "euserv/",
            PermissionHintKey = "Text.Dns.Permission.EuServ",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Email",
                    EnvVar = "EUSERV_EMAIL",
                    LabelKey = "Text.Dns.Field.Email",
                    PlaceholderKey = "Text.Dns.Placeholder.Email"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "EUSERV_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                },
                new DnsProviderField
                {
                    Key = "OrderId",
                    EnvVar = "EUSERV_ORDER_ID",
                    LabelKey = "Text.Dns.Field.OrderId",
                    PlaceholderKey = "Text.Dns.Placeholder.OrderId"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Excedo,
            LegoProvider = "excedo",
            EnvPrefix = "EXCEDO",
            DisplayName = "Excedo",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "excedo/",
            PermissionHintKey = "Text.Dns.Permission.Excedo",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "EXCEDO_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                },
                new DnsProviderField
                {
                    Key = "ApiUrl",
                    EnvVar = "EXCEDO_API_URL",
                    LabelKey = "Text.Dns.Field.ApiUrl",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiUrl"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = ExternalProgram,
            LegoProvider = "exec",
            EnvPrefix = "",
            DisplayName = "External program",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "exec/",
            PermissionHintKey = "Text.Dns.Permission.ExternalProgram",
            RequiresCredentials = false,
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ExecPath",
                    EnvVar = "EXEC_PATH",
                    LabelKey = "Text.Dns.Field.ExecPath",
                    PlaceholderKey = "Text.Dns.Placeholder.ExecPath"
                },
                new DnsProviderField
                {
                    Key = "ExecMode",
                    EnvVar = "EXEC_MODE",
                    LabelKey = "Text.Dns.Field.ExecMode",
                    PlaceholderKey = "Text.Dns.Placeholder.ExecMode",
                    Required = false
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = F5Xc,
            LegoProvider = "f5xc",
            EnvPrefix = "F5XC",
            DisplayName = "F5 XC",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "f5xc/",
            PermissionHintKey = "Text.Dns.Permission.F5Xc",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiToken",
                    EnvVar = "F5XC_API_TOKEN",
                    LabelKey = "Text.Dns.Field.ApiToken",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiToken"
                },
                new DnsProviderField
                {
                    Key = "TenantName",
                    EnvVar = "F5XC_TENANT_NAME",
                    LabelKey = "Text.Dns.Field.TenantName",
                    PlaceholderKey = "Text.Dns.Placeholder.TenantName"
                },
                new DnsProviderField
                {
                    Key = "GroupName",
                    EnvVar = "F5XC_GROUP_NAME",
                    LabelKey = "Text.Dns.Field.GroupName",
                    PlaceholderKey = "Text.Dns.Placeholder.GroupName"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Feno,
            LegoProvider = "feno",
            EnvPrefix = "FENO",
            DisplayName = "FENO",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "feno/",
            PermissionHintKey = "Text.Dns.Permission.Feno",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "FENO_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Fornex,
            LegoProvider = "fornex",
            EnvPrefix = "FORNEX",
            DisplayName = "Fornex",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "fornex/",
            PermissionHintKey = "Text.Dns.Permission.Fornex",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "FORNEX_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = FreeMyIp,
            LegoProvider = "freemyip",
            EnvPrefix = "FREEMYIP",
            DisplayName = "freemyip.com",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "freemyip/",
            PermissionHintKey = "Text.Dns.Permission.FreeMyIp",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Token",
                    EnvVar = "FREEMYIP_TOKEN",
                    LabelKey = "Text.Dns.Field.Token",
                    PlaceholderKey = "Text.Dns.Placeholder.Token"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = NameSurfer,
            LegoProvider = "namesurfer",
            EnvPrefix = "NAMESURFER",
            DisplayName = "FusionLayer NameSurfer",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "namesurfer/",
            PermissionHintKey = "Text.Dns.Permission.NameSurfer",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "BaseUrl",
                    EnvVar = "NAMESURFER_BASE_URL",
                    LabelKey = "Text.Dns.Field.BaseUrl",
                    PlaceholderKey = "Text.Dns.Placeholder.BaseUrl"
                },
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "NAMESURFER_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                },
                new DnsProviderField
                {
                    Key = "ApiSecret",
                    EnvVar = "NAMESURFER_API_SECRET",
                    LabelKey = "Text.Dns.Field.ApiSecret",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiSecret"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Gehirn,
            LegoProvider = "gehirn",
            EnvPrefix = "GEHIRN",
            DisplayName = "Gehirn",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "gehirn/",
            PermissionHintKey = "Text.Dns.Permission.Gehirn",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "TokenId",
                    EnvVar = "GEHIRN_TOKEN_ID",
                    LabelKey = "Text.Dns.Field.TokenId",
                    PlaceholderKey = "Text.Dns.Placeholder.TokenId"
                },
                new DnsProviderField
                {
                    Key = "TokenSecret",
                    EnvVar = "GEHIRN_TOKEN_SECRET",
                    LabelKey = "Text.Dns.Field.TokenSecret",
                    PlaceholderKey = "Text.Dns.Placeholder.TokenSecret"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = GigahostNo,
            LegoProvider = "gigahostno",
            EnvPrefix = "GIGAHOSTNO",
            DisplayName = "Gigahost.no",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "gigahostno/",
            PermissionHintKey = "Text.Dns.Permission.GigahostNo",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "GIGAHOSTNO_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "GIGAHOSTNO_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                },
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "GIGAHOSTNO_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey",
                    Required = false
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Gname,
            LegoProvider = "gname",
            EnvPrefix = "GNAME",
            DisplayName = "Gname",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "gname/",
            PermissionHintKey = "Text.Dns.Permission.Gname",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "AppId",
                    EnvVar = "GNAME_APP_ID",
                    LabelKey = "Text.Dns.Field.AppId",
                    PlaceholderKey = "Text.Dns.Placeholder.AppId"
                },
                new DnsProviderField
                {
                    Key = "AppKey",
                    EnvVar = "GNAME_APP_KEY",
                    LabelKey = "Text.Dns.Field.AppKey",
                    PlaceholderKey = "Text.Dns.Placeholder.AppKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Gravity,
            LegoProvider = "gravity",
            EnvPrefix = "GRAVITY",
            DisplayName = "Gravity",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "gravity/",
            PermissionHintKey = "Text.Dns.Permission.Gravity",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ServerUrl",
                    EnvVar = "GRAVITY_SERVER_URL",
                    LabelKey = "Text.Dns.Field.ServerUrl",
                    PlaceholderKey = "Text.Dns.Placeholder.ServerUrl"
                },
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "GRAVITY_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "GRAVITY_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = HostingDe,
            LegoProvider = "hostingde",
            EnvPrefix = "HOSTINGDE",
            DisplayName = "Hosting.de",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "hostingde/",
            PermissionHintKey = "Text.Dns.Permission.HostingDe",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "HOSTINGDE_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = HostingNl,
            LegoProvider = "hostingnl",
            EnvPrefix = "HOSTINGNL",
            DisplayName = "Hosting.nl",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "hostingnl/",
            PermissionHintKey = "Text.Dns.Permission.HostingNl",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "HOSTINGNL_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Hosttech,
            LegoProvider = "hosttech",
            EnvPrefix = "HOSTTECH",
            DisplayName = "Hosttech",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "hosttech/",
            PermissionHintKey = "Text.Dns.Permission.Hosttech",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "HOSTTECH_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "HOSTTECH_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password",
                    Required = false
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = HostUp,
            LegoProvider = "hostup",
            EnvPrefix = "HOSTUP",
            DisplayName = "HostUp",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "hostup/",
            PermissionHintKey = "Text.Dns.Permission.HostUp",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "HOSTUP_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = HttpReq,
            LegoProvider = "httpreq",
            EnvPrefix = "HTTPREQ",
            DisplayName = "HTTP request",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "httpreq/",
            PermissionHintKey = "Text.Dns.Permission.HttpReq",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Mode",
                    EnvVar = "HTTPREQ_MODE",
                    LabelKey = "Text.Dns.Field.Mode",
                    PlaceholderKey = "Text.Dns.Placeholder.Mode",
                    Required = false
                },
                new DnsProviderField
                {
                    Key = "Endpoint",
                    EnvVar = "HTTPREQ_ENDPOINT",
                    LabelKey = "Text.Dns.Field.Endpoint",
                    PlaceholderKey = "Text.Dns.Placeholder.Endpoint"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = HttpNet,
            LegoProvider = "httpnet",
            EnvPrefix = "HTTPNET",
            DisplayName = "http.net",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "httpnet/",
            PermissionHintKey = "Text.Dns.Permission.HttpNet",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "HTTPNET_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = HuaweiCloud,
            LegoProvider = "huaweicloud",
            EnvPrefix = "HUAWEICLOUD",
            DisplayName = "Huawei Cloud",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "huaweicloud/",
            PermissionHintKey = "Text.Dns.Permission.HuaweiCloud",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "AccessKeyId",
                    EnvVar = "HUAWEICLOUD_ACCESS_KEY_ID",
                    LabelKey = "Text.Dns.Field.AccessKeyId",
                    PlaceholderKey = "Text.Dns.Placeholder.AccessKeyId"
                },
                new DnsProviderField
                {
                    Key = "SecretAccessKey",
                    EnvVar = "HUAWEICLOUD_SECRET_ACCESS_KEY",
                    LabelKey = "Text.Dns.Field.SecretAccessKey",
                    PlaceholderKey = "Text.Dns.Placeholder.SecretAccessKey"
                },
                new DnsProviderField
                {
                    Key = "Region",
                    EnvVar = "HUAWEICLOUD_REGION",
                    LabelKey = "Text.Dns.Field.Region",
                    PlaceholderKey = "Text.Dns.Placeholder.Region"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Hurricane,
            LegoProvider = "hurricane",
            EnvPrefix = "HURRICANE",
            DisplayName = "Hurricane Electric DNS",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "hurricane/",
            PermissionHintKey = "Text.Dns.Permission.Hurricane",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Tokens",
                    EnvVar = "HURRICANE_TOKENS",
                    LabelKey = "Text.Dns.Field.Tokens",
                    PlaceholderKey = "Text.Dns.Placeholder.Tokens"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = HyperOne,
            LegoProvider = "hyperone",
            EnvPrefix = "HYPERONE",
            DisplayName = "HyperOne",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "hyperone/",
            PermissionHintKey = "Text.Dns.Permission.HyperOne",
            RequiresCredentials = false,
            Fields =
            [
                new DnsProviderField
                {
                    Key = "PassportLocation",
                    EnvVar = "HYPERONE_PASSPORT_LOCATION",
                    LabelKey = "Text.Dns.Field.PassportLocation",
                    PlaceholderKey = "Text.Dns.Placeholder.PassportLocation",
                    Required = false
                },
                new DnsProviderField
                {
                    Key = "ApiUrl",
                    EnvVar = "HYPERONE_API_URL",
                    LabelKey = "Text.Dns.Field.ApiUrl",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiUrl",
                    Required = false
                },
                new DnsProviderField
                {
                    Key = "LocationId",
                    EnvVar = "HYPERONE_LOCATION_ID",
                    LabelKey = "Text.Dns.Field.LocationId",
                    PlaceholderKey = "Text.Dns.Placeholder.LocationId",
                    Required = false
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = IbmCloud,
            LegoProvider = "ibmcloud",
            EnvPrefix = "SOFTLAYER",
            DisplayName = "IBM Cloud (SoftLayer)",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "ibmcloud/",
            PermissionHintKey = "Text.Dns.Permission.IbmCloud",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "SOFTLAYER_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "SOFTLAYER_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = IijDpf,
            LegoProvider = "iijdpf",
            EnvPrefix = "IIJ_DPF",
            DisplayName = "IIJ DNS Platform Service",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "iijdpf/",
            PermissionHintKey = "Text.Dns.Permission.IijDpf",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiToken",
                    EnvVar = "IIJ_DPF_API_TOKEN",
                    LabelKey = "Text.Dns.Field.ApiToken",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiToken"
                },
                new DnsProviderField
                {
                    Key = "DpmServiceCode",
                    EnvVar = "IIJ_DPF_DPM_SERVICE_CODE",
                    LabelKey = "Text.Dns.Field.DpmServiceCode",
                    PlaceholderKey = "Text.Dns.Placeholder.DpmServiceCode"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Infoblox,
            LegoProvider = "infoblox",
            EnvPrefix = "INFOBLOX",
            DisplayName = "Infoblox",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "infoblox/",
            PermissionHintKey = "Text.Dns.Permission.Infoblox",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "INFOBLOX_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "INFOBLOX_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                },
                new DnsProviderField
                {
                    Key = "Host",
                    EnvVar = "INFOBLOX_HOST",
                    LabelKey = "Text.Dns.Field.Host",
                    PlaceholderKey = "Text.Dns.Placeholder.Host"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = InternetBs,
            LegoProvider = "internetbs",
            EnvPrefix = "INTERNET_BS",
            DisplayName = "Internet.bs",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "internetbs/",
            PermissionHintKey = "Text.Dns.Permission.InternetBs",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "INTERNET_BS_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "INTERNET_BS_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Inwx,
            LegoProvider = "inwx",
            EnvPrefix = "INWX",
            DisplayName = "INWX",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "inwx/",
            PermissionHintKey = "Text.Dns.Permission.Inwx",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "INWX_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "INWX_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Ionos,
            LegoProvider = "ionos",
            EnvPrefix = "IONOS",
            DisplayName = "Ionos",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "ionos/",
            PermissionHintKey = "Text.Dns.Permission.Ionos",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "IONOS_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = IonosCloud,
            LegoProvider = "ionoscloud",
            EnvPrefix = "IONOSCLOUD",
            DisplayName = "Ionos Cloud",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "ionoscloud/",
            PermissionHintKey = "Text.Dns.Permission.IonosCloud",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiToken",
                    EnvVar = "IONOSCLOUD_API_TOKEN",
                    LabelKey = "Text.Dns.Field.ApiToken",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiToken"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Ipv64,
            LegoProvider = "ipv64",
            EnvPrefix = "IPV64",
            DisplayName = "IPv64",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "ipv64/",
            PermissionHintKey = "Text.Dns.Permission.Ipv64",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "IPV64_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = IspConfig,
            LegoProvider = "ispconfig",
            EnvPrefix = "ISPCONFIG",
            DisplayName = "ISPConfig 3",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "ispconfig/",
            PermissionHintKey = "Text.Dns.Permission.IspConfig",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ServerUrl",
                    EnvVar = "ISPCONFIG_SERVER_URL",
                    LabelKey = "Text.Dns.Field.ServerUrl",
                    PlaceholderKey = "Text.Dns.Placeholder.ServerUrl"
                },
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "ISPCONFIG_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "ISPCONFIG_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = IspConfigDdns,
            LegoProvider = "ispconfigddns",
            EnvPrefix = "ISPCONFIG_DDNS",
            DisplayName = "ISPConfig 3 - Dynamic DNS (DDNS) Module",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "ispconfigddns/",
            PermissionHintKey = "Text.Dns.Permission.IspConfigDdns",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ServerUrl",
                    EnvVar = "ISPCONFIG_DDNS_SERVER_URL",
                    LabelKey = "Text.Dns.Field.ServerUrl",
                    PlaceholderKey = "Text.Dns.Placeholder.ServerUrl"
                },
                new DnsProviderField
                {
                    Key = "Token",
                    EnvVar = "ISPCONFIG_DDNS_TOKEN",
                    LabelKey = "Text.Dns.Field.Token",
                    PlaceholderKey = "Text.Dns.Placeholder.Token"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = JdCloud,
            LegoProvider = "jdcloud",
            EnvPrefix = "JDCLOUD",
            DisplayName = "JD Cloud",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "jdcloud/",
            PermissionHintKey = "Text.Dns.Permission.JdCloud",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "AccessKeyId",
                    EnvVar = "JDCLOUD_ACCESS_KEY_ID",
                    LabelKey = "Text.Dns.Field.AccessKeyId",
                    PlaceholderKey = "Text.Dns.Placeholder.AccessKeyId"
                },
                new DnsProviderField
                {
                    Key = "AccessKeySecret",
                    EnvVar = "JDCLOUD_ACCESS_KEY_SECRET",
                    LabelKey = "Text.Dns.Field.AccessKeySecret",
                    PlaceholderKey = "Text.Dns.Placeholder.AccessKeySecret"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = AcmeDns,
            LegoProvider = "acmedns",
            EnvPrefix = "",
            DisplayName = "Joohoi's ACME-DNS",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "acmedns/",
            PermissionHintKey = "Text.Dns.Permission.AcmeDns",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiBase",
                    EnvVar = "ACME_DNS_API_BASE",
                    LabelKey = "Text.Dns.Field.ApiBase",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiBase"
                },
                new DnsProviderField
                {
                    Key = "StoragePath",
                    EnvVar = "ACME_DNS_STORAGE_PATH",
                    LabelKey = "Text.Dns.Field.StoragePath",
                    PlaceholderKey = "Text.Dns.Placeholder.StoragePath",
                    Required = false
                },
                new DnsProviderField
                {
                    Key = "StorageBaseUrl",
                    EnvVar = "ACME_DNS_STORAGE_BASE_URL",
                    LabelKey = "Text.Dns.Field.StorageBaseUrl",
                    PlaceholderKey = "Text.Dns.Placeholder.StorageBaseUrl",
                    Required = false
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Katapult,
            LegoProvider = "katapult",
            EnvPrefix = "KATAPULT",
            DisplayName = "Katapult",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "katapult/",
            PermissionHintKey = "Text.Dns.Permission.Katapult",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "KATAPULT_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = KeyHelp,
            LegoProvider = "keyhelp",
            EnvPrefix = "KEYHELP",
            DisplayName = "KeyHelp",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "keyhelp/",
            PermissionHintKey = "Text.Dns.Permission.KeyHelp",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "BaseUrl",
                    EnvVar = "KEYHELP_BASE_URL",
                    LabelKey = "Text.Dns.Field.BaseUrl",
                    PlaceholderKey = "Text.Dns.Placeholder.BaseUrl"
                },
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "KEYHELP_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Leaseweb,
            LegoProvider = "leaseweb",
            EnvPrefix = "LEASEWEB",
            DisplayName = "Leaseweb",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "leaseweb/",
            PermissionHintKey = "Text.Dns.Permission.Leaseweb",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "LEASEWEB_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Liara,
            LegoProvider = "liara",
            EnvPrefix = "LIARA",
            DisplayName = "Liara",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "liara/",
            PermissionHintKey = "Text.Dns.Permission.Liara",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "LIARA_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = LimaCity,
            LegoProvider = "limacity",
            EnvPrefix = "LIMACITY",
            DisplayName = "Lima-City",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "limacity/",
            PermissionHintKey = "Text.Dns.Permission.LimaCity",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "LIMACITY_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = MailInABox,
            LegoProvider = "mailinabox",
            EnvPrefix = "MAILINABOX",
            DisplayName = "Mail-in-a-Box",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "mailinabox/",
            PermissionHintKey = "Text.Dns.Permission.MailInABox",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Email",
                    EnvVar = "MAILINABOX_EMAIL",
                    LabelKey = "Text.Dns.Field.Email",
                    PlaceholderKey = "Text.Dns.Placeholder.Email"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "MAILINABOX_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                },
                new DnsProviderField
                {
                    Key = "BaseUrl",
                    EnvVar = "MAILINABOX_BASE_URL",
                    LabelKey = "Text.Dns.Field.BaseUrl",
                    PlaceholderKey = "Text.Dns.Placeholder.BaseUrl"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = ManageEngine,
            LegoProvider = "manageengine",
            EnvPrefix = "MANAGEENGINE",
            DisplayName = "ManageEngine CloudDNS",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "manageengine/",
            PermissionHintKey = "Text.Dns.Permission.ManageEngine",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ClientId",
                    EnvVar = "MANAGEENGINE_CLIENT_ID",
                    LabelKey = "Text.Dns.Field.ClientId",
                    PlaceholderKey = "Text.Dns.Placeholder.ClientId"
                },
                new DnsProviderField
                {
                    Key = "ClientSecret",
                    EnvVar = "MANAGEENGINE_CLIENT_SECRET",
                    LabelKey = "Text.Dns.Field.ClientSecret",
                    PlaceholderKey = "Text.Dns.Placeholder.ClientSecret"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Metaname,
            LegoProvider = "metaname",
            EnvPrefix = "METANAME",
            DisplayName = "Metaname",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "metaname/",
            PermissionHintKey = "Text.Dns.Permission.Metaname",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "AccountReference",
                    EnvVar = "METANAME_ACCOUNT_REFERENCE",
                    LabelKey = "Text.Dns.Field.AccountReference",
                    PlaceholderKey = "Text.Dns.Placeholder.AccountReference"
                },
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "METANAME_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Metaregistrar,
            LegoProvider = "metaregistrar",
            EnvPrefix = "METAREGISTRAR",
            DisplayName = "Metaregistrar",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "metaregistrar/",
            PermissionHintKey = "Text.Dns.Permission.Metaregistrar",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiToken",
                    EnvVar = "METAREGISTRAR_API_TOKEN",
                    LabelKey = "Text.Dns.Field.ApiToken",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiToken"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = MijnHost,
            LegoProvider = "mijnhost",
            EnvPrefix = "MIJNHOST",
            DisplayName = "mijn.host",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "mijnhost/",
            PermissionHintKey = "Text.Dns.Permission.MijnHost",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "MIJNHOST_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Mittwald,
            LegoProvider = "mittwald",
            EnvPrefix = "MITTWALD",
            DisplayName = "Mittwald",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "mittwald/",
            PermissionHintKey = "Text.Dns.Permission.Mittwald",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Token",
                    EnvVar = "MITTWALD_TOKEN",
                    LabelKey = "Text.Dns.Field.Token",
                    PlaceholderKey = "Text.Dns.Placeholder.Token"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = MyAddr,
            LegoProvider = "myaddr",
            EnvPrefix = "MYADDR",
            DisplayName = "myaddr.{tools,dev,io}",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "myaddr/",
            PermissionHintKey = "Text.Dns.Permission.MyAddr",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "PrivateKeysMapping",
                    EnvVar = "MYADDR_PRIVATE_KEYS_MAPPING",
                    LabelKey = "Text.Dns.Field.PrivateKeysMapping",
                    PlaceholderKey = "Text.Dns.Placeholder.PrivateKeysMapping"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = MyDnsJp,
            LegoProvider = "mydnsjp",
            EnvPrefix = "MYDNSJP",
            DisplayName = "MyDNS.jp",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "mydnsjp/",
            PermissionHintKey = "Text.Dns.Permission.MyDnsJp",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "MasterId",
                    EnvVar = "MYDNSJP_MASTER_ID",
                    LabelKey = "Text.Dns.Field.MasterId",
                    PlaceholderKey = "Text.Dns.Placeholder.MasterId"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "MYDNSJP_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Myra,
            LegoProvider = "myra",
            EnvPrefix = "MYRA",
            DisplayName = "Myra",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "myra/",
            PermissionHintKey = "Text.Dns.Permission.Myra",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "MYRA_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                },
                new DnsProviderField
                {
                    Key = "ApiSecret",
                    EnvVar = "MYRA_API_SECRET",
                    LabelKey = "Text.Dns.Field.ApiSecret",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiSecret"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = MythicBeasts,
            LegoProvider = "mythicbeasts",
            EnvPrefix = "MYTHICBEASTS",
            DisplayName = "MythicBeasts",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "mythicbeasts/",
            PermissionHintKey = "Text.Dns.Permission.MythicBeasts",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "MYTHICBEASTS_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "MYTHICBEASTS_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = NameDotCom,
            LegoProvider = "namedotcom",
            EnvPrefix = "NAMECOM",
            DisplayName = "Name.com",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "namedotcom/",
            PermissionHintKey = "Text.Dns.Permission.NameDotCom",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "NAMECOM_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "ApiToken",
                    EnvVar = "NAMECOM_API_TOKEN",
                    LabelKey = "Text.Dns.Field.ApiToken",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiToken"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = NameSilo,
            LegoProvider = "namesilo",
            EnvPrefix = "NAMESILO",
            DisplayName = "Namesilo",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "namesilo/",
            PermissionHintKey = "Text.Dns.Permission.NameSilo",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "NAMESILO_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = NearlyFreeSpeech,
            LegoProvider = "nearlyfreespeech",
            EnvPrefix = "NEARLYFREESPEECH",
            DisplayName = "NearlyFreeSpeech.NET",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "nearlyfreespeech/",
            PermissionHintKey = "Text.Dns.Permission.NearlyFreeSpeech",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "NEARLYFREESPEECH_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                },
                new DnsProviderField
                {
                    Key = "Login",
                    EnvVar = "NEARLYFREESPEECH_LOGIN",
                    LabelKey = "Text.Dns.Field.Login",
                    PlaceholderKey = "Text.Dns.Placeholder.Login"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = NederHost,
            LegoProvider = "nederhost",
            EnvPrefix = "NEDERHOST",
            DisplayName = "NederHost",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "nederhost/",
            PermissionHintKey = "Text.Dns.Permission.NederHost",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "NEDERHOST_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Neodigit,
            LegoProvider = "neodigit",
            EnvPrefix = "NEODIGIT",
            DisplayName = "Neodigit",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "neodigit/",
            PermissionHintKey = "Text.Dns.Permission.Neodigit",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Token",
                    EnvVar = "NEODIGIT_TOKEN",
                    LabelKey = "Text.Dns.Field.Token",
                    PlaceholderKey = "Text.Dns.Placeholder.Token"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Netlify,
            LegoProvider = "netlify",
            EnvPrefix = "NETLIFY",
            DisplayName = "Netlify",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "netlify/",
            PermissionHintKey = "Text.Dns.Permission.Netlify",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Token",
                    EnvVar = "NETLIFY_TOKEN",
                    LabelKey = "Text.Dns.Field.Token",
                    PlaceholderKey = "Text.Dns.Placeholder.Token"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Netnod,
            LegoProvider = "netnod",
            EnvPrefix = "NETNOD",
            DisplayName = "Netnod",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "netnod/",
            PermissionHintKey = "Text.Dns.Permission.Netnod",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Token",
                    EnvVar = "NETNOD_TOKEN",
                    LabelKey = "Text.Dns.Field.Token",
                    PlaceholderKey = "Text.Dns.Placeholder.Token"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = NexDns,
            LegoProvider = "nexdns",
            EnvPrefix = "NEXDNS",
            DisplayName = "NexDNS",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "nexdns/",
            PermissionHintKey = "Text.Dns.Permission.NexDns",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiToken",
                    EnvVar = "NEXDNS_API_TOKEN",
                    LabelKey = "Text.Dns.Field.ApiToken",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiToken"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Ngenix,
            LegoProvider = "ngenix",
            EnvPrefix = "NGENIX",
            DisplayName = "Ngenix",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "ngenix/",
            PermissionHintKey = "Text.Dns.Permission.Ngenix",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "NGENIX_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Token",
                    EnvVar = "NGENIX_TOKEN",
                    LabelKey = "Text.Dns.Field.Token",
                    PlaceholderKey = "Text.Dns.Placeholder.Token"
                },
                new DnsProviderField
                {
                    Key = "CustomerId",
                    EnvVar = "NGENIX_CUSTOMER_ID",
                    LabelKey = "Text.Dns.Field.CustomerId",
                    PlaceholderKey = "Text.Dns.Placeholder.CustomerId"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Nicmanager,
            LegoProvider = "nicmanager",
            EnvPrefix = "NICMANAGER",
            DisplayName = "Nicmanager",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "nicmanager/",
            PermissionHintKey = "Text.Dns.Permission.Nicmanager",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Login",
                    EnvVar = "NICMANAGER_API_LOGIN",
                    LabelKey = "Text.Dns.Field.Login",
                    PlaceholderKey = "Text.Dns.Placeholder.Login",
                    Required = false
                },
                new DnsProviderField
                {
                    Key = "ApiUsername",
                    EnvVar = "NICMANAGER_API_USERNAME",
                    LabelKey = "Text.Dns.Field.ApiUsername",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiUsername",
                    Required = false
                },
                new DnsProviderField
                {
                    Key = "Email",
                    EnvVar = "NICMANAGER_API_EMAIL",
                    LabelKey = "Text.Dns.Field.Email",
                    PlaceholderKey = "Text.Dns.Placeholder.Email",
                    Required = false
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "NICMANAGER_API_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = NifCloud,
            LegoProvider = "nifcloud",
            EnvPrefix = "NIFCLOUD",
            DisplayName = "NIFCloud",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "nifcloud/",
            PermissionHintKey = "Text.Dns.Permission.NifCloud",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "AccessKeyId",
                    EnvVar = "NIFCLOUD_ACCESS_KEY_ID",
                    LabelKey = "Text.Dns.Field.AccessKeyId",
                    PlaceholderKey = "Text.Dns.Placeholder.AccessKeyId"
                },
                new DnsProviderField
                {
                    Key = "SecretAccessKey",
                    EnvVar = "NIFCLOUD_SECRET_ACCESS_KEY",
                    LabelKey = "Text.Dns.Field.SecretAccessKey",
                    PlaceholderKey = "Text.Dns.Placeholder.SecretAccessKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Njalla,
            LegoProvider = "njalla",
            EnvPrefix = "NJALLA",
            DisplayName = "Njalla",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "njalla/",
            PermissionHintKey = "Text.Dns.Permission.Njalla",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Token",
                    EnvVar = "NJALLA_TOKEN",
                    LabelKey = "Text.Dns.Field.Token",
                    PlaceholderKey = "Text.Dns.Placeholder.Token"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Nodion,
            LegoProvider = "nodion",
            EnvPrefix = "NODION",
            DisplayName = "Nodion",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "nodion/",
            PermissionHintKey = "Text.Dns.Permission.Nodion",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiToken",
                    EnvVar = "NODION_API_TOKEN",
                    LabelKey = "Text.Dns.Field.ApiToken",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiToken"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Octenium,
            LegoProvider = "octenium",
            EnvPrefix = "OCTENIUM",
            DisplayName = "Octenium",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "octenium/",
            PermissionHintKey = "Text.Dns.Permission.Octenium",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "OCTENIUM_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = OmgLol,
            LegoProvider = "omglol",
            EnvPrefix = "OMGLOL",
            DisplayName = "omg.lol",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "omglol/",
            PermissionHintKey = "Text.Dns.Permission.OmgLol",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "OMGLOL_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = OnlineNet,
            LegoProvider = "onlinenet",
            EnvPrefix = "ONLINENET",
            DisplayName = "Online.net",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "onlinenet/",
            PermissionHintKey = "Text.Dns.Permission.OnlineNet",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiToken",
                    EnvVar = "ONLINENET_API_TOKEN",
                    LabelKey = "Text.Dns.Field.ApiToken",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiToken"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = OpenTelekomCloud,
            LegoProvider = "otc",
            EnvPrefix = "OTC",
            DisplayName = "Open Telekom Cloud",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "otc/",
            PermissionHintKey = "Text.Dns.Permission.OpenTelekomCloud",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "OTC_USER_NAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "OTC_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                },
                new DnsProviderField
                {
                    Key = "ProjectName",
                    EnvVar = "OTC_PROJECT_NAME",
                    LabelKey = "Text.Dns.Field.ProjectName",
                    PlaceholderKey = "Text.Dns.Placeholder.ProjectName"
                },
                new DnsProviderField
                {
                    Key = "DomainName",
                    EnvVar = "OTC_DOMAIN_NAME",
                    LabelKey = "Text.Dns.Field.DomainName",
                    PlaceholderKey = "Text.Dns.Placeholder.DomainName"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Openprovider,
            LegoProvider = "openprovider",
            EnvPrefix = "OPENPROVIDER",
            DisplayName = "Openprovider",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "openprovider/",
            PermissionHintKey = "Text.Dns.Permission.Openprovider",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "OPENPROVIDER_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "OPENPROVIDER_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = OpusDns,
            LegoProvider = "opusdns",
            EnvPrefix = "OPUSDNS",
            DisplayName = "OpusDNS",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "opusdns/",
            PermissionHintKey = "Text.Dns.Permission.OpusDns",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "OPUSDNS_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Plesk,
            LegoProvider = "plesk",
            EnvPrefix = "PLESK",
            DisplayName = "plesk.com",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "plesk/",
            PermissionHintKey = "Text.Dns.Permission.Plesk",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ServerBaseUrl",
                    EnvVar = "PLESK_SERVER_BASE_URL",
                    LabelKey = "Text.Dns.Field.ServerBaseUrl",
                    PlaceholderKey = "Text.Dns.Placeholder.ServerBaseUrl"
                },
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "PLESK_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "PLESK_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = PointDns,
            LegoProvider = "pointdns",
            EnvPrefix = "POINTDNS",
            DisplayName = "PointDNS/PointHQ",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "pointdns/",
            PermissionHintKey = "Text.Dns.Permission.PointDns",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "POINTDNS_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "POINTDNS_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = PowerAdmin,
            LegoProvider = "poweradmin",
            EnvPrefix = "POWERADMIN",
            DisplayName = "Poweradmin",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "poweradmin/",
            PermissionHintKey = "Text.Dns.Permission.PowerAdmin",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "BaseUrl",
                    EnvVar = "POWERADMIN_BASE_URL",
                    LabelKey = "Text.Dns.Field.BaseUrl",
                    PlaceholderKey = "Text.Dns.Placeholder.BaseUrl"
                },
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "POWERADMIN_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Rage4,
            LegoProvider = "rage4",
            EnvPrefix = "RAGE4",
            DisplayName = "Rage4",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "rage4/",
            PermissionHintKey = "Text.Dns.Permission.Rage4",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "RAGE4_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "RAGE4_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = RainYun,
            LegoProvider = "rainyun",
            EnvPrefix = "RAINYUN",
            DisplayName = "Rain Yun/雨云",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "rainyun/",
            PermissionHintKey = "Text.Dns.Permission.RainYun",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "RAINYUN_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = RcodeZero,
            LegoProvider = "rcodezero",
            EnvPrefix = "RCODEZERO",
            DisplayName = "RcodeZero",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "rcodezero/",
            PermissionHintKey = "Text.Dns.Permission.RcodeZero",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiToken",
                    EnvVar = "RCODEZERO_API_TOKEN",
                    LabelKey = "Text.Dns.Field.ApiToken",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiToken"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = RegRu,
            LegoProvider = "regru",
            EnvPrefix = "REGRU",
            DisplayName = "reg.ru",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "regru/",
            PermissionHintKey = "Text.Dns.Permission.RegRu",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "REGRU_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "REGRU_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Regfish,
            LegoProvider = "regfish",
            EnvPrefix = "REGFISH",
            DisplayName = "Regfish",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "regfish/",
            PermissionHintKey = "Text.Dns.Permission.Regfish",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "REGFISH_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = RimuHosting,
            LegoProvider = "rimuhosting",
            EnvPrefix = "RIMUHOSTING",
            DisplayName = "RimuHosting",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "rimuhosting/",
            PermissionHintKey = "Text.Dns.Permission.RimuHosting",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "RIMUHOSTING_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = RuCenter,
            LegoProvider = "nicru",
            EnvPrefix = "NICRU",
            DisplayName = "RU CENTER",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "nicru/",
            PermissionHintKey = "Text.Dns.Permission.RuCenter",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "User",
                    EnvVar = "NICRU_USER",
                    LabelKey = "Text.Dns.Field.User",
                    PlaceholderKey = "Text.Dns.Placeholder.User"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "NICRU_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                },
                new DnsProviderField
                {
                    Key = "ServiceId",
                    EnvVar = "NICRU_SERVICE_ID",
                    LabelKey = "Text.Dns.Field.ServiceId",
                    PlaceholderKey = "Text.Dns.Placeholder.ServiceId"
                },
                new DnsProviderField
                {
                    Key = "Secret",
                    EnvVar = "NICRU_SECRET",
                    LabelKey = "Text.Dns.Field.Secret",
                    PlaceholderKey = "Text.Dns.Placeholder.Secret"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = SakuraCloud,
            LegoProvider = "sakuracloud",
            EnvPrefix = "SAKURACLOUD",
            DisplayName = "Sakura Cloud",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "sakuracloud/",
            PermissionHintKey = "Text.Dns.Permission.SakuraCloud",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "AccessToken",
                    EnvVar = "SAKURACLOUD_ACCESS_TOKEN",
                    LabelKey = "Text.Dns.Field.AccessToken",
                    PlaceholderKey = "Text.Dns.Placeholder.AccessToken"
                },
                new DnsProviderField
                {
                    Key = "AccessTokenSecret",
                    EnvVar = "SAKURACLOUD_ACCESS_TOKEN_SECRET",
                    LabelKey = "Text.Dns.Field.AccessTokenSecret",
                    PlaceholderKey = "Text.Dns.Placeholder.AccessTokenSecret"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Scaleway,
            LegoProvider = "scaleway",
            EnvPrefix = "SCW",
            DisplayName = "Scaleway",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "scaleway/",
            PermissionHintKey = "Text.Dns.Permission.Scaleway",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "SecretKey",
                    EnvVar = "SCW_SECRET_KEY",
                    LabelKey = "Text.Dns.Field.SecretKey",
                    PlaceholderKey = "Text.Dns.Placeholder.SecretKey",
                    Required = false
                },
                new DnsProviderField
                {
                    Key = "ProjectId",
                    EnvVar = "SCW_PROJECT_ID",
                    LabelKey = "Text.Dns.Field.ProjectId",
                    PlaceholderKey = "Text.Dns.Placeholder.ProjectId",
                    Required = false
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = ScanNet,
            LegoProvider = "scannet",
            EnvPrefix = "SCANNET",
            DisplayName = "ScanNet",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "scannet/",
            PermissionHintKey = "Text.Dns.Permission.ScanNet",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "SCANNET_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Selectel,
            LegoProvider = "selectel",
            EnvPrefix = "SELECTEL",
            DisplayName = "Selectel",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "selectel/",
            PermissionHintKey = "Text.Dns.Permission.Selectel",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiToken",
                    EnvVar = "SELECTEL_API_TOKEN",
                    LabelKey = "Text.Dns.Field.ApiToken",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiToken"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = SelectelV2,
            LegoProvider = "selectelv2",
            EnvPrefix = "SELECTELV2",
            DisplayName = "Selectel v2",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "selectelv2/",
            PermissionHintKey = "Text.Dns.Permission.SelectelV2",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "SELECTELV2_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "SELECTELV2_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                },
                new DnsProviderField
                {
                    Key = "AccountId",
                    EnvVar = "SELECTELV2_ACCOUNT_ID",
                    LabelKey = "Text.Dns.Field.AccountId",
                    PlaceholderKey = "Text.Dns.Placeholder.AccountId"
                },
                new DnsProviderField
                {
                    Key = "ProjectId",
                    EnvVar = "SELECTELV2_PROJECT_ID",
                    LabelKey = "Text.Dns.Field.ProjectId",
                    PlaceholderKey = "Text.Dns.Placeholder.ProjectId"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = SelfHostDe,
            LegoProvider = "selfhostde",
            EnvPrefix = "SELFHOSTDE",
            DisplayName = "SelfHost.(de|eu)",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "selfhostde/",
            PermissionHintKey = "Text.Dns.Permission.SelfHostDe",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "SELFHOSTDE_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "SELFHOSTDE_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                },
                new DnsProviderField
                {
                    Key = "RecordsMapping",
                    EnvVar = "SELFHOSTDE_RECORDS_MAPPING",
                    LabelKey = "Text.Dns.Field.RecordsMapping",
                    PlaceholderKey = "Text.Dns.Placeholder.RecordsMapping"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Servercow,
            LegoProvider = "servercow",
            EnvPrefix = "SERVERCOW",
            DisplayName = "Servercow",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "servercow/",
            PermissionHintKey = "Text.Dns.Permission.Servercow",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "SERVERCOW_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "SERVERCOW_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Shellrent,
            LegoProvider = "shellrent",
            EnvPrefix = "SHELLRENT",
            DisplayName = "Shellrent",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "shellrent/",
            PermissionHintKey = "Text.Dns.Permission.Shellrent",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "SHELLRENT_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Token",
                    EnvVar = "SHELLRENT_TOKEN",
                    LabelKey = "Text.Dns.Field.Token",
                    PlaceholderKey = "Text.Dns.Placeholder.Token"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Simply,
            LegoProvider = "simply",
            EnvPrefix = "SIMPLY",
            DisplayName = "Simply.com",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "simply/",
            PermissionHintKey = "Text.Dns.Permission.Simply",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "AccountName",
                    EnvVar = "SIMPLY_ACCOUNT_NAME",
                    LabelKey = "Text.Dns.Field.AccountName",
                    PlaceholderKey = "Text.Dns.Placeholder.AccountName"
                },
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "SIMPLY_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Sonic,
            LegoProvider = "sonic",
            EnvPrefix = "SONIC",
            DisplayName = "Sonic",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "sonic/",
            PermissionHintKey = "Text.Dns.Permission.Sonic",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "UserId",
                    EnvVar = "SONIC_USER_ID",
                    LabelKey = "Text.Dns.Field.UserId",
                    PlaceholderKey = "Text.Dns.Placeholder.UserId"
                },
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "SONIC_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Spaceship,
            LegoProvider = "spaceship",
            EnvPrefix = "SPACESHIP",
            DisplayName = "Spaceship",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "spaceship/",
            PermissionHintKey = "Text.Dns.Permission.Spaceship",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "SPACESHIP_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                },
                new DnsProviderField
                {
                    Key = "ApiSecret",
                    EnvVar = "SPACESHIP_API_SECRET",
                    LabelKey = "Text.Dns.Field.ApiSecret",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiSecret"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Syse,
            LegoProvider = "syse",
            EnvPrefix = "SYSE",
            DisplayName = "Syse",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "syse/",
            PermissionHintKey = "Text.Dns.Permission.Syse",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Credentials",
                    EnvVar = "SYSE_CREDENTIALS",
                    LabelKey = "Text.Dns.Field.Credentials",
                    PlaceholderKey = "Text.Dns.Placeholder.Credentials"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Technitium,
            LegoProvider = "technitium",
            EnvPrefix = "TECHNITIUM",
            DisplayName = "Technitium",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "technitium/",
            PermissionHintKey = "Text.Dns.Permission.Technitium",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ServerBaseUrl",
                    EnvVar = "TECHNITIUM_SERVER_BASE_URL",
                    LabelKey = "Text.Dns.Field.ServerBaseUrl",
                    PlaceholderKey = "Text.Dns.Placeholder.ServerBaseUrl"
                },
                new DnsProviderField
                {
                    Key = "ApiToken",
                    EnvVar = "TECHNITIUM_API_TOKEN",
                    LabelKey = "Text.Dns.Field.ApiToken",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiToken"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Tele3,
            LegoProvider = "tele3",
            EnvPrefix = "TELE3",
            DisplayName = "Tele3",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "tele3/",
            PermissionHintKey = "Text.Dns.Permission.Tele3",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Key",
                    EnvVar = "TELE3_KEY",
                    LabelKey = "Text.Dns.Field.Key",
                    PlaceholderKey = "Text.Dns.Placeholder.Key"
                },
                new DnsProviderField
                {
                    Key = "Secret",
                    EnvVar = "TELE3_SECRET",
                    LabelKey = "Text.Dns.Field.Secret",
                    PlaceholderKey = "Text.Dns.Placeholder.Secret"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = EdgeOne,
            LegoProvider = "edgeone",
            EnvPrefix = "EDGEONE",
            DisplayName = "Tencent EdgeOne",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "edgeone/",
            PermissionHintKey = "Text.Dns.Permission.EdgeOne",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "AccessKeyId",
                    EnvVar = "EDGEONE_SECRET_ID",
                    LabelKey = "Text.Dns.Field.AccessKeyId",
                    PlaceholderKey = "Text.Dns.Placeholder.AccessKeyId"
                },
                new DnsProviderField
                {
                    Key = "AccessKeySecret",
                    EnvVar = "EDGEONE_SECRET_KEY",
                    LabelKey = "Text.Dns.Field.AccessKeySecret",
                    PlaceholderKey = "Text.Dns.Placeholder.AccessKeySecret"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = TimewebCloud,
            LegoProvider = "timewebcloud",
            EnvPrefix = "TIMEWEBCLOUD",
            DisplayName = "Timeweb Cloud",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "timewebcloud/",
            PermissionHintKey = "Text.Dns.Permission.TimewebCloud",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "AuthToken",
                    EnvVar = "TIMEWEBCLOUD_AUTH_TOKEN",
                    LabelKey = "Text.Dns.Field.AuthToken",
                    PlaceholderKey = "Text.Dns.Placeholder.AuthToken"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = TodayNic,
            LegoProvider = "todaynic",
            EnvPrefix = "TODAYNIC",
            DisplayName = "TodayNIC/时代互联",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "todaynic/",
            PermissionHintKey = "Text.Dns.Permission.TodayNic",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "AuthUserId",
                    EnvVar = "TODAYNIC_AUTH_USER_ID",
                    LabelKey = "Text.Dns.Field.AuthUserId",
                    PlaceholderKey = "Text.Dns.Placeholder.AuthUserId"
                },
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "TODAYNIC_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = UCloud,
            LegoProvider = "ucloud",
            EnvPrefix = "UCLOUD",
            DisplayName = "UCloud",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "ucloud/",
            PermissionHintKey = "Text.Dns.Permission.UCloud",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "PublicKey",
                    EnvVar = "UCLOUD_PUBLIC_KEY",
                    LabelKey = "Text.Dns.Field.PublicKey",
                    PlaceholderKey = "Text.Dns.Placeholder.PublicKey"
                },
                new DnsProviderField
                {
                    Key = "PrivateKey",
                    EnvVar = "UCLOUD_PRIVATE_KEY",
                    LabelKey = "Text.Dns.Field.PrivateKey",
                    PlaceholderKey = "Text.Dns.Placeholder.PrivateKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = UltraDns,
            LegoProvider = "ultradns",
            EnvPrefix = "ULTRADNS",
            DisplayName = "Ultradns",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "ultradns/",
            PermissionHintKey = "Text.Dns.Permission.UltraDns",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "ULTRADNS_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "ULTRADNS_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = UnitedDomains,
            LegoProvider = "uniteddomains",
            EnvPrefix = "UNITEDDOMAINS",
            DisplayName = "United-Domains",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "uniteddomains/",
            PermissionHintKey = "Text.Dns.Permission.UnitedDomains",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "UNITEDDOMAINS_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Variomedia,
            LegoProvider = "variomedia",
            EnvPrefix = "VARIOMEDIA",
            DisplayName = "Variomedia",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "variomedia/",
            PermissionHintKey = "Text.Dns.Permission.Variomedia",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiToken",
                    EnvVar = "VARIOMEDIA_API_TOKEN",
                    LabelKey = "Text.Dns.Field.ApiToken",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiToken"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Veesp,
            LegoProvider = "veesp",
            EnvPrefix = "VEESP",
            DisplayName = "Veesp",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "veesp/",
            PermissionHintKey = "Text.Dns.Permission.Veesp",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "VEESP_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "VEESP_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = VegaDns,
            LegoProvider = "vegadns",
            EnvPrefix = "VEGADNS",
            DisplayName = "VegaDNS",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "vegadns/",
            PermissionHintKey = "Text.Dns.Permission.VegaDns",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "SECRET_VEGADNS_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey",
                    Required = false
                },
                new DnsProviderField
                {
                    Key = "ApiSecret",
                    EnvVar = "SECRET_VEGADNS_SECRET",
                    LabelKey = "Text.Dns.Field.ApiSecret",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiSecret",
                    Required = false
                },
                new DnsProviderField
                {
                    Key = "ApiUrl",
                    EnvVar = "VEGADNS_URL",
                    LabelKey = "Text.Dns.Field.ApiUrl",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiUrl"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Vercel,
            LegoProvider = "vercel",
            EnvPrefix = "VERCEL",
            DisplayName = "Vercel",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "vercel/",
            PermissionHintKey = "Text.Dns.Permission.Vercel",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiToken",
                    EnvVar = "VERCEL_API_TOKEN",
                    LabelKey = "Text.Dns.Field.ApiToken",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiToken"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Versio,
            LegoProvider = "versio",
            EnvPrefix = "VERSIO",
            DisplayName = "Versio.[nl|eu|uk]",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "versio/",
            PermissionHintKey = "Text.Dns.Permission.Versio",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "VERSIO_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "VERSIO_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = VinylDns,
            LegoProvider = "vinyldns",
            EnvPrefix = "VINYLDNS",
            DisplayName = "VinylDNS",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "vinyldns/",
            PermissionHintKey = "Text.Dns.Permission.VinylDns",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "AccessKey",
                    EnvVar = "VINYLDNS_ACCESS_KEY",
                    LabelKey = "Text.Dns.Field.AccessKey",
                    PlaceholderKey = "Text.Dns.Placeholder.AccessKey"
                },
                new DnsProviderField
                {
                    Key = "SecretKey",
                    EnvVar = "VINYLDNS_SECRET_KEY",
                    LabelKey = "Text.Dns.Field.SecretKey",
                    PlaceholderKey = "Text.Dns.Placeholder.SecretKey"
                },
                new DnsProviderField
                {
                    Key = "Host",
                    EnvVar = "VINYLDNS_HOST",
                    LabelKey = "Text.Dns.Field.Host",
                    PlaceholderKey = "Text.Dns.Placeholder.Host"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Virtualname,
            LegoProvider = "virtualname",
            EnvPrefix = "VIRTUALNAME",
            DisplayName = "Virtualname",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "virtualname/",
            PermissionHintKey = "Text.Dns.Permission.Virtualname",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Token",
                    EnvVar = "VIRTUALNAME_TOKEN",
                    LabelKey = "Text.Dns.Field.Token",
                    PlaceholderKey = "Text.Dns.Placeholder.Token"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = VkCloud,
            LegoProvider = "vkcloud",
            EnvPrefix = "VK_CLOUD",
            DisplayName = "VK Cloud",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "vkcloud/",
            PermissionHintKey = "Text.Dns.Permission.VkCloud",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ProjectId",
                    EnvVar = "VK_CLOUD_PROJECT_ID",
                    LabelKey = "Text.Dns.Field.ProjectId",
                    PlaceholderKey = "Text.Dns.Placeholder.ProjectId"
                },
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "VK_CLOUD_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "VK_CLOUD_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = VolcEngine,
            LegoProvider = "volcengine",
            EnvPrefix = "VOLC",
            DisplayName = "Volcano Engine/火山引擎",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "volcengine/",
            PermissionHintKey = "Text.Dns.Permission.VolcEngine",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "AccessKeyId",
                    EnvVar = "VOLC_ACCESSKEY",
                    LabelKey = "Text.Dns.Field.AccessKeyId",
                    PlaceholderKey = "Text.Dns.Placeholder.AccessKeyId"
                },
                new DnsProviderField
                {
                    Key = "AccessKeySecret",
                    EnvVar = "VOLC_SECRETKEY",
                    LabelKey = "Text.Dns.Field.AccessKeySecret",
                    PlaceholderKey = "Text.Dns.Placeholder.AccessKeySecret"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Vscale,
            LegoProvider = "vscale",
            EnvPrefix = "VSCALE",
            DisplayName = "Vscale",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "vscale/",
            PermissionHintKey = "Text.Dns.Permission.Vscale",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiToken",
                    EnvVar = "VSCALE_API_TOKEN",
                    LabelKey = "Text.Dns.Field.ApiToken",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiToken"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Wannafind,
            LegoProvider = "wannafind",
            EnvPrefix = "WANNAFIND",
            DisplayName = "Wannafind",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "wannafind/",
            PermissionHintKey = "Text.Dns.Permission.Wannafind",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "WANNAFIND_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Webglobe,
            LegoProvider = "webglobe",
            EnvPrefix = "WEBGLOBE",
            DisplayName = "Webglobe",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "webglobe/",
            PermissionHintKey = "Text.Dns.Permission.Webglobe",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Login",
                    EnvVar = "WEBGLOBE_LOGIN",
                    LabelKey = "Text.Dns.Field.Login",
                    PlaceholderKey = "Text.Dns.Placeholder.Login"
                },
                new DnsProviderField
                {
                    Key = "ApiToken",
                    EnvVar = "WEBGLOBE_TOKEN",
                    LabelKey = "Text.Dns.Field.ApiToken",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiToken"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = WebNamesCa,
            LegoProvider = "webnamesca",
            EnvPrefix = "WEBNAMESCA",
            DisplayName = "webnames.ca",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "webnamesca/",
            PermissionHintKey = "Text.Dns.Permission.WebNamesCa",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiUser",
                    EnvVar = "WEBNAMESCA_API_USER",
                    LabelKey = "Text.Dns.Field.ApiUser",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiUser"
                },
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "WEBNAMESCA_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = WebNamesRu,
            LegoProvider = "webnamesru",
            EnvPrefix = "WEBNAMESRU",
            DisplayName = "webnames.ru",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "webnamesru/",
            PermissionHintKey = "Text.Dns.Permission.WebNamesRu",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "WEBNAMESRU_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Websupport,
            LegoProvider = "websupport",
            EnvPrefix = "WEBSUPPORT",
            DisplayName = "Websupport",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "websupport/",
            PermissionHintKey = "Text.Dns.Permission.Websupport",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "WEBSUPPORT_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                },
                new DnsProviderField
                {
                    Key = "Secret",
                    EnvVar = "WEBSUPPORT_SECRET",
                    LabelKey = "Text.Dns.Field.Secret",
                    PlaceholderKey = "Text.Dns.Placeholder.Secret"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Wedos,
            LegoProvider = "wedos",
            EnvPrefix = "WEDOS",
            DisplayName = "WEDOS",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "wedos/",
            PermissionHintKey = "Text.Dns.Permission.Wedos",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "WEDOS_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "WapiPassword",
                    EnvVar = "WEDOS_WAPI_PASSWORD",
                    LabelKey = "Text.Dns.Field.WapiPassword",
                    PlaceholderKey = "Text.Dns.Placeholder.WapiPassword"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = WestCn,
            LegoProvider = "westcn",
            EnvPrefix = "WESTCN",
            DisplayName = "West.cn/西部数码",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "westcn/",
            PermissionHintKey = "Text.Dns.Permission.WestCn",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Username",
                    EnvVar = "WESTCN_USERNAME",
                    LabelKey = "Text.Dns.Field.Username",
                    PlaceholderKey = "Text.Dns.Placeholder.Username"
                },
                new DnsProviderField
                {
                    Key = "Password",
                    EnvVar = "WESTCN_PASSWORD",
                    LabelKey = "Text.Dns.Field.Password",
                    PlaceholderKey = "Text.Dns.Placeholder.Password"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Xinnet,
            LegoProvider = "xinnet",
            EnvPrefix = "XINNET",
            DisplayName = "Xinnet",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "xinnet/",
            PermissionHintKey = "Text.Dns.Permission.Xinnet",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "Secret",
                    EnvVar = "XINNET_SECRET",
                    LabelKey = "Text.Dns.Field.Secret",
                    PlaceholderKey = "Text.Dns.Placeholder.Secret"
                },
                new DnsProviderField
                {
                    Key = "AgentId",
                    EnvVar = "XINNET_AGENT_ID",
                    LabelKey = "Text.Dns.Field.AgentId",
                    PlaceholderKey = "Text.Dns.Placeholder.AgentId"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Yandex360,
            LegoProvider = "yandex360",
            EnvPrefix = "YANDEX360",
            DisplayName = "Yandex 360",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "yandex360/",
            PermissionHintKey = "Text.Dns.Permission.Yandex360",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "OauthToken",
                    EnvVar = "YANDEX360_OAUTH_TOKEN",
                    LabelKey = "Text.Dns.Field.OauthToken",
                    PlaceholderKey = "Text.Dns.Placeholder.OauthToken"
                },
                new DnsProviderField
                {
                    Key = "OrgId",
                    EnvVar = "YANDEX360_ORG_ID",
                    LabelKey = "Text.Dns.Field.OrgId",
                    PlaceholderKey = "Text.Dns.Placeholder.OrgId"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = YandexCloud,
            LegoProvider = "yandexcloud",
            EnvPrefix = "YANDEX_CLOUD",
            DisplayName = "Yandex Cloud",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "yandexcloud/",
            PermissionHintKey = "Text.Dns.Permission.YandexCloud",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "IamToken",
                    EnvVar = "YANDEX_CLOUD_IAM_TOKEN",
                    LabelKey = "Text.Dns.Field.IamToken",
                    PlaceholderKey = "Text.Dns.Placeholder.IamToken"
                },
                new DnsProviderField
                {
                    Key = "FolderId",
                    EnvVar = "YANDEX_CLOUD_FOLDER_ID",
                    LabelKey = "Text.Dns.Field.FolderId",
                    PlaceholderKey = "Text.Dns.Placeholder.FolderId"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Zilore,
            LegoProvider = "zilore",
            EnvPrefix = "ZILORE",
            DisplayName = "Zilore",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "zilore/",
            PermissionHintKey = "Text.Dns.Permission.Zilore",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "AccessKey",
                    EnvVar = "ZILORE_ACCESS_KEY",
                    LabelKey = "Text.Dns.Field.AccessKey",
                    PlaceholderKey = "Text.Dns.Placeholder.AccessKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = ZoneEe,
            LegoProvider = "zoneee",
            EnvPrefix = "ZONEEE",
            DisplayName = "Zone.ee",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "zoneee/",
            PermissionHintKey = "Text.Dns.Permission.ZoneEe",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiUser",
                    EnvVar = "ZONEEE_API_USER",
                    LabelKey = "Text.Dns.Field.ApiUser",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiUser"
                },
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "ZONEEE_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = ZoneEdit,
            LegoProvider = "zoneedit",
            EnvPrefix = "ZONEEDIT",
            DisplayName = "ZoneEdit",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "zoneedit/",
            PermissionHintKey = "Text.Dns.Permission.ZoneEdit",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "User",
                    EnvVar = "ZONEEDIT_USER",
                    LabelKey = "Text.Dns.Field.User",
                    PlaceholderKey = "Text.Dns.Placeholder.User"
                },
                new DnsProviderField
                {
                    Key = "AuthToken",
                    EnvVar = "ZONEEDIT_AUTH_TOKEN",
                    LabelKey = "Text.Dns.Field.AuthToken",
                    PlaceholderKey = "Text.Dns.Placeholder.AuthToken"
                }
            ]
        },
        new DnsProviderDescriptor
        {
            Id = Zonomi,
            LegoProvider = "zonomi",
            EnvPrefix = "ZONOMI",
            DisplayName = "Zonomi",
            DocumentationUrl = "https://go-acme.github.io/lego/dns/" + "zonomi/",
            PermissionHintKey = "Text.Dns.Permission.Zonomi",
            Fields =
            [
                new DnsProviderField
                {
                    Key = "ApiKey",
                    EnvVar = "ZONOMI_API_KEY",
                    LabelKey = "Text.Dns.Field.ApiKey",
                    PlaceholderKey = "Text.Dns.Placeholder.ApiKey"
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