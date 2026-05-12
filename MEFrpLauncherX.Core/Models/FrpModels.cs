namespace MEFrpLauncherX.Core.Models;

public class FrpConfig
{
    public string ServerAddr
    {
        get;
        set;
    } = string.Empty;

    public int ServerPort
    {
        get;
        set;
    }

    public string User
    {
        get;
        set;
    } = string.Empty;

    public AuthConfig Auth
    {
        get;
        set;
    } = new();

    public List<ProxyConfig> Proxies
    {
        get;
        set;
    } = [];
}

public class AuthConfig
{
    public string Method
    {
        get;
        set;
    } = "token";

    public string Token
    {
        get;
        set;
    } = string.Empty;
}

public class ProxyConfig
{
    public string Name
    {
        get;
        set;
    } = string.Empty;

    public string Type
    {
        get;
        set;
    } = "tcp";

    public string LocalIP
    {
        get;
        set;
    } = "127.0.0.1";

    public int LocalPort
    {
        get;
        set;
    }

    public int RemotePort
    {
        get;
        set;
    }

    public List<string> CustomDomains
    {
        get;
        set;
    } = [];

    public PluginConfig Plugin
    {
        get;
        set;
    } = new();

    public TransportConfig Transport
    {
        get;
        set;
    } = new();
}

public class PluginConfig
{
    public string Type
    {
        get;
        set;
    } = string.Empty;

    public string LocalAddr
    {
        get;
        set;
    } = string.Empty;

    public string CrtPath
    {
        get;
        set;
    } = string.Empty;

    public string KeyPath
    {
        get;
        set;
    } = string.Empty;
}

public class TransportConfig
{
    public bool UseEncryption
    {
        get;
        set;
    } = true;

    public bool UseCompression
    {
        get;
        set;
    } = true;
}