namespace MEFrpLauncherX.Core.Plugin;

/// <summary>
///     逻辑插件接口
/// </summary>
public interface ILogicalPlugin
{
    /// <summary>
    ///     插件名称
    /// </summary>
    string Name
    {
        get;
    }

    /// <summary>
    ///     插件描述
    /// </summary>
    string Description
    {
        get;
    }

    /// <summary>
    ///     插件版本
    /// </summary>
    Version Version
    {
        get;
    }

    Task<bool> InitializeAsync();
    Task<object?> ExecuteQueryAsync(string query, params object[] parameters);
    Task<int> ExecuteNonQueryAsync(string command, params object[] parameters);
    Task DisconnectAsync();
}

[AttributeUsage(AttributeTargets.Class)]
public class PluginMetadataAttribute : Attribute
{
    public PluginMetadataAttribute(string name, string description, string version)
    {
        Name = name;
        Description = description;
        Version = version;
    }

    public string Name
    {
        get;
    }

    public string Description
    {
        get;
    }

    public string Version
    {
        get;
    }
}