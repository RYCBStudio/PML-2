namespace MEFrpLauncherX.Plugin.Core;

public class ExecutionContext
{
    public string PluginId { get; set; } = "";
    public Dictionary<string, object> Variables { get; set; } = new();
    public Dictionary<string, object> Data { get; set; } = new();  // 外部注入数据

    public ExecutionContext CloneWithArgs(Dictionary<string, object>? args)
    {
        var clone = new ExecutionContext
        {
            PluginId = this.PluginId,
            Variables = new Dictionary<string, object>(this.Variables),
            Data = new Dictionary<string, object>(this.Data)
        };
        if (args != null)
            foreach (var kv in args) clone.Variables[kv.Key] = kv.Value;
        return clone;
    }
}