namespace MEFrpLauncherX.Core.UrlProtocol;

public interface IUrlProtocolHandler
{
    void HandleUrl(string url);
    bool CanHandle(string url);
}
