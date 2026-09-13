namespace MEFrpLauncherX.Plugin.Core;

public class PluginDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>插件类型（缺省 event）。<c>create-proxy-template</c> 等类型不参与事件引擎。</summary>
    public string Type { get; set; } = "event";

    /// <summary>要求的最低核心版本（如 "26.3.2"），为空表示不限制</summary>
    public string? MinCoreVersion { get; set; }

    /// <summary>核心版本是否满足 minCoreVersion（版本兼容才注册/提供能力）</summary>
    public bool IsCompatible { get; set; } = true;

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

    /// <summary>插件类型；缺省 event（旧插件无 type 字段时保持原有事件插件行为）</summary>
    public string Type { get; set; } = "event";

    /// <summary>要求的最低核心版本（camelCase 键 <c>minCoreVersion</c>），为空表示不限制</summary>
    public string? MinCoreVersion { get; set; }

    public List<TriggerDefinition> Triggers { get; set; } = new();
    public Dictionary<string, List<ActionDefinition>> Functions { get; set; } = new();

    /// <summary>隧道模板声明（仅 type=create-proxy-template 时解析）</summary>
    public List<ProxyTemplateDefinition> Templates { get; set; } = new();
}