namespace MEFrpLauncherX.Core.UrlProtocol;

public static class UrlProtocolHelper
{
    private static readonly List<IUrlProtocolHandler> _handlers = [];

    public static void RegisterHandler(IUrlProtocolHandler handler) => _handlers.Add(handler);

    public static void HandleUrl(string url)
    {
        if (string.IsNullOrEmpty(url) || !url.StartsWith("pml2://"))
        {
            return;
        }

        Console.WriteLine($"[URL Protocol] 处理: {url}");

        foreach (var handler in _handlers)
        {
            if (handler.CanHandle(url))
            {
                handler.HandleUrl(url);
                return;
            }
        }

        Console.WriteLine($"[URL Protocol] 没有找到能处理 {url} 的处理器");
    }

    public static void ParsePml2Url(string url, out string command, out string[] parameters)
    {
        command = string.Empty;
        parameters = [];

        try
        {
            var uri = new Uri(url);
            command = uri.Host.ToLower();

            // 解析路径参数
            parameters = uri.AbsolutePath
                .Split('/', StringSplitOptions.RemoveEmptyEntries);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[URL Protocol] 解析URL失败: {ex.Message}");
        }
    }
}