namespace MEFrpLauncherX.Core.Models;

/// <summary>
///     创建隧道模板：保存常用隧道参数，供创建表单一键套用。
///     持久化于 Settings.json（<c>ProxyTemplates</c>），重启后仍在。
/// </summary>
public class ProxyTemplate
{
    /// <summary>模板名称（唯一标识）</summary>
    public string Name
    {
        get;
        set;
    } = string.Empty;

    /// <summary>本地地址</summary>
    public string LocalAddress
    {
        get;
        set;
    } = "127.0.0.1";

    /// <summary>本地端口</summary>
    public int LocalPort
    {
        get;
        set;
    }

    /// <summary>协议类型（tcp/udp/http/https），为空表示沿用当前所选</summary>
    public string? Protocol
    {
        get;
        set;
    }

    /// <summary>远程端口（仅 tcp/udp 有效），为空表示不预填</summary>
    public int? RemotePort
    {
        get;
        set;
    }

    /// <summary>启用加密</summary>
    public bool UseEncryption
    {
        get;
        set;
    }

    /// <summary>启用压缩</summary>
    public bool UseCompression
    {
        get;
        set;
    }
}

/// <summary>
///     创建隧道表单默认值配置
/// </summary>
public class CreateProxyDefaults
{
    /// <summary>本地地址默认值</summary>
    public string LocalAddress
    {
        get;
        set;
    } = "127.0.0.1";
}
