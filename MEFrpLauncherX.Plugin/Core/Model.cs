namespace MEFrpLauncherX.Plugin.Core;

public class PluginDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<TriggerDefinition> Triggers { get; set; } = new();
}

public class TriggerDefinition
{
    public string On { get; set; } = "";
    public string? Condition { get; set; }
    public List<ActionDefinition> Actions { get; set; } = new();
}

public class ActionDefinition
{
    public string Name { get; set; } = "";
    public Dictionary<string, object> Params { get; set; } = new();
}

// 用于 #include 解析的中间模型
public class RawPlugin
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Author { get; set; } = "";
    public string Version { get; set; } = "";
    public List<TriggerDefinition> Triggers { get; set; } = new();
    public Dictionary<string, List<ActionDefinition>> Functions { get; set; } = new();
}