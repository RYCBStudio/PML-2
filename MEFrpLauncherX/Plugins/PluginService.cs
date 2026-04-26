// PluginService.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using MEFrpLauncherX.Core.Plugin;

namespace MEFrpLauncherX.Plugins;

public class PluginService
{
    private readonly List<ILogicalPlugin> _loadedPlugins = [];
    private readonly string _pluginsDirectory;

    public PluginService()
    {
        // 插件目录：在程序所在目录的 Plugins 文件夹
        _pluginsDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "Plugins");

        EnsurePluginsDirectory();
    }

    private void EnsurePluginsDirectory()
    {
        if (!Directory.Exists(_pluginsDirectory))
        {
            Directory.CreateDirectory(_pluginsDirectory);
        }
    }

    public IEnumerable<ILogicalPlugin> GetLoadedPlugins() => _loadedPlugins;

    public async Task LoadPluginsAsync()
    {
        _loadedPlugins.Clear();

        if (!Directory.Exists(_pluginsDirectory))
        {
            return;
        }

        var pluginFiles = Directory.GetFiles(_pluginsDirectory, "*.dll");

        foreach (var file in pluginFiles)
        {
            try
            {
                var plugin = await LoadPluginAsync(file);
                if (plugin != null)
                {
                    _loadedPlugins.Add(plugin);
                }
            }
            catch (Exception ex)
            {
                Core.App.CurrentLogger.Error(ex);
                Core.App.CurrentLogger.Log($"加载插件 {file} 失败: {ex.Message}");
            }
        }
    }

    private async Task<ILogicalPlugin> LoadPluginAsync(string assemblyPath)
    {
        var loadContext = new PluginLoadContext(assemblyPath);
        var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);

        foreach (var type in assembly.GetTypes())
        {
            if (typeof(ILogicalPlugin).IsAssignableFrom(type) && !type.IsInterface)
            {
                if (Activator.CreateInstance(type) is ILogicalPlugin plugin)
                {
                    var initialized = await plugin.InitializeAsync();
                    return initialized ? plugin : null;
                }
            }
        }

        return null;
    }
}

// 自定义AssemblyLoadContext用于插件卸载
public class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath) : base(true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly Load(AssemblyName assemblyName)
    {
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        return assemblyPath != null ? LoadFromAssemblyPath(assemblyPath) : null;
    }
}