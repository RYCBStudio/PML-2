namespace MEFrpLauncherX.Core.Messaging;

public static class MessageBus
{
    private static readonly Dictionary<Type, List<Action<object>>> _handlers = new();

    public static void SendMessage<T>(T message) where T : class
    {
        var messageType = typeof(T);
        if (_handlers.ContainsKey(messageType))
        {
            foreach (var handler in _handlers[messageType])
            {
                handler(message);
            }
        }
    }

    public static void RegisterHandler<T>(Action<T> handler) where T : class
    {
        var messageType = typeof(T);
        if (!_handlers.ContainsKey(messageType))
        {
            _handlers[messageType] = [];
        }

        _handlers[messageType].Add(obj => handler((T)obj));
    }
}

// 消息定义
public class StartProxyMessage { public string Id { get; } public StartProxyMessage(string id) => Id = id; }
public class NavigateToAboutMessage { }
public class StartMCMessage { }