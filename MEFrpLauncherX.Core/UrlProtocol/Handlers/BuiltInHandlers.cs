// YourApp.Core/UrlProtocol/Handlers/StartProxyHandler.cs

using MEFrpLauncherX.Core.Messaging;

namespace MEFrpLauncherX.Core.UrlProtocol.Handlers;

public class StartProxyHandler : IUrlProtocolHandler
{
    public bool CanHandle(string url)
    {
        return url.StartsWith("pml2://startProxy/");
    }

    public void HandleUrl(string url)
    {
        UrlProtocolHelper.ParsePml2Url(url, out _, out var parameters);
        
        if (parameters.Length > 0)
        {
            var id = parameters[0];
            // 触发启动代理事件
            ProxyStarted?.Invoke(this, new ProxyEventArgs(id));
            
            // 或者使用消息总线
            MessageBus.SendMessage(new StartProxyMessage(id));
        }
    }

    public static event EventHandler<ProxyEventArgs>? ProxyStarted;
}

public class ProxyEventArgs : EventArgs
{
    public string Id { get; }
    public ProxyEventArgs(string id)
    {
        Id = id;
    }
}


public class AboutHandler : IUrlProtocolHandler
{
    public bool CanHandle(string url)
    {
        return url.StartsWith("pml2://about");
    }

    public void HandleUrl(string url)
    {
        // 打开关于页面
        MessageBus.SendMessage(new NavigateToAboutMessage());
    }
}


public class StartMCHandler : IUrlProtocolHandler
{
    public bool CanHandle(string url)
    {
        return url.StartsWith("pml2://startMC");
    }

    public void HandleUrl(string url)
    {
        // 启动MC逻辑
        MessageBus.SendMessage(new StartMCMessage());
    }
}