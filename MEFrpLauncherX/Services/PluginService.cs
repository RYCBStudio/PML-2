using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Plugin.Engine;
using ExecutionContext = MEFrpLauncherX.Plugin.Core.ExecutionContext;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MEFrpLauncherX.Services;

public class PluginService
{
    private static PluginService? _instance;
    public static PluginService Instance => _instance ??= new PluginService();

    private readonly PluginEngine _engine = new();
    private HotReloadService? _hotReload;
    private readonly HashSet<string> _disabledPlugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PluginInfo> _plugins = new();
    private string _pluginsFolder = "";

    public IReadOnlyList<PluginInfo> Plugins => _plugins;

    public bool IsLoaded { get; private set; }

    public string PluginsFolder => _pluginsFolder;

    private PluginService()
    {
        _pluginsFolder = Path.Combine(Core.App.StartupPath, "Config", "Plugins");
        Directory.CreateDirectory(_pluginsFolder);
    }

    /// <summary>
    /// 加载所有插件
    /// </summary>
    public void LoadPlugins()
    {
        try
        {
            _plugins.Clear();
            _engine.LoadAll(_pluginsFolder);

            // 收集插件元数据
            foreach (var file in Directory.GetFiles(_pluginsFolder, "*.yaml", SearchOption.AllDirectories))
            {
                var info = ExtractPluginInfo(file);
                if (info != null)
                {
                    info.IsEnabled = !_disabledPlugins.Contains(info.Id);
                    _plugins.Add(info);
                }
            }

            // 启动热重载
            _hotReload = new HotReloadService(_engine);
            _hotReload.Start(_pluginsFolder);

            IsLoaded = true;
            Core.App.CurrentLogger.Log($"插件系统已加载 {_plugins.Count} 个插件", module: Core.EnumLogModule.Custom,
                customModuleName: "Plugin");
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger.Error(ex, "插件加载失败");
        }
    }

    /// <summary>
    /// 重新加载所有插件
    /// </summary>
    public void ReloadPlugins()
    {
        _hotReload?.Stop();
        LoadPlugins();
    }

    /// <summary>
    /// 启用插件
    /// </summary>
    public void EnablePlugin(string pluginId)
    {
        _disabledPlugins.Remove(pluginId);
        var info = _plugins.FirstOrDefault(p => p.Id == pluginId);
        if (info != null) info.IsEnabled = true;
        SaveDisabledList();
    }

    /// <summary>
    /// 禁用插件（下次加载生效）
    /// </summary>
    public void DisablePlugin(string pluginId)
    {
        _disabledPlugins.Add(pluginId);
        var info = _plugins.FirstOrDefault(p => p.Id == pluginId);
        if (info != null) info.IsEnabled = false;
        SaveDisabledList();
    }

    /// <summary>
    /// 触发事件
    /// </summary>
    public async Task TriggerAsync(string eventName, Dictionary<string, object>? data = null)
    {
        if (!IsLoaded) return;

        var ctx = new ExecutionContext
        {
            PluginId = $"system:{eventName}",
            Data = data ?? new Dictionary<string, object>()
        };

        try
        {
            await _engine.TriggerAsync(eventName, ctx);
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger.Error(ex, $"插件事件 {eventName} 执行失败");
        }
    }

    /// <summary>
    /// 安装插件文件
    /// </summary>
    public bool InstallPlugin(string sourcePath)
    {
        try
        {
            var destFileName = Path.GetFileName(sourcePath);
            var destPath = Path.Combine(_pluginsFolder, destFileName);

            if (File.Exists(destPath))
            {
                // 备份旧文件
                var bakPath = destPath + ".bak";
                if (File.Exists(bakPath)) File.Delete(bakPath);
                File.Move(destPath, bakPath);
            }

            File.Copy(sourcePath, destPath);
            ReloadPlugins();
            return true;
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger.Error(ex, "安装插件失败");
            return false;
        }
    }

    /// <summary>
    /// 卸载插件
    /// </summary>
    public bool UninstallPlugin(string pluginId)
    {
        var info = _plugins.FirstOrDefault(p => p.Id == pluginId);
        if (info == null) return false;

        try
        {
            if (File.Exists(info.FilePath))
                File.Delete(info.FilePath);

            _plugins.Remove(info);
            _disabledPlugins.Remove(pluginId);
            SaveDisabledList();
            ReloadPlugins();
            return true;
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger.Error(ex, "卸载插件失败");
            return false;
        }
    }

    /// <summary>
    /// 查看插件日志
    /// </summary>
    public string? GetPluginYaml(string pluginId)
    {
        var info = _plugins.FirstOrDefault(p => p.Id == pluginId);
        if (info == null || !File.Exists(info.FilePath)) return null;
        return File.ReadAllText(info.FilePath);
    }

    private PluginInfo? ExtractPluginInfo(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
            var raw = deserializer.Deserialize<RawPluginMeta>(content);

            return new PluginInfo
            {
                Id = raw.Id ?? Path.GetFileNameWithoutExtension(filePath),
                Name = raw.Name ?? "",
                Description = raw.Description ?? "",
                Author = raw.Author ?? "",
                Version = raw.Version ?? "1.0",
                FilePath = filePath,
                TriggerCount = raw.Triggers?.Count ?? 0,
                FunctionCount = raw.Functions?.Count ?? 0,
                IsEnabled = true
            };
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger.Warning($"解析插件元数据失败: {filePath}, {ex.Message}");
            return null;
        }
    }

    private void SaveDisabledList()
    {
        var path = Path.Combine(_pluginsFolder, ".disabled");
        File.WriteAllText(path, JsonSerializer.Serialize(_disabledPlugins.ToList()));
    }

    private void LoadDisabledList()
    {
        var path = Path.Combine(_pluginsFolder, ".disabled");
        if (File.Exists(path))
        {
            try
            {
                var list = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path));
                if (list != null)
                {
                    _disabledPlugins.Clear();
                    foreach (var id in list) _disabledPlugins.Add(id);
                }
            }
            catch
            {
                // 忽略
            }
        }
    }
}

public class PluginInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Author { get; set; } = "";
    public string Version { get; set; } = "1.0";
    public string FilePath { get; set; } = "";
    public int TriggerCount { get; set; }
    public int FunctionCount { get; set; }
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// 用于反序列化插件 YAML 元数据
/// </summary>
internal class RawPluginMeta
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Author { get; set; }
    public string? Version { get; set; }
    public List<object>? Triggers { get; set; }
    public Dictionary<string, object>? Functions { get; set; }
}
