using System.Diagnostics;
using System.Text.Json;
using AvaloniaEdit.Utils;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Analysis;
using MEFrpLauncherX.Plugin.Engine;
using YamlDotNet.Serialization;
using ExecutionContext = MEFrpLauncherX.Plugin.Core.ExecutionContext;

namespace MEFrpLauncherX.Plugin.Services;

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
        _pluginsFolder = Path.Combine(App.StartupPath, "Config", "Plugins");
        Directory.CreateDirectory(_pluginsFolder);
    }

    /// <summary>
    /// 加载所有插件
    /// </summary>
    public void LoadPlugins()
    {
        var sw = Stopwatch.StartNew();
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
            sw.Stop();
            var enabledCount = _plugins.Count(p => p.IsEnabled);
            App.CurrentLogger.Log(
                $"插件系统已加载 {_plugins.Count} 个插件(启用 {enabledCount} 个, 禁用 {_disabledPlugins.Count} 个), 耗时 {sw.ElapsedMilliseconds}ms",
                module: EnumLogModule.Plugin);
            AppAnalytics.TrackAction("plugin.load", new Dictionary<string, string>
            {
                ["count"] = _plugins.Count.ToString(),
                ["enabled"] = enabledCount.ToString(),
                ["costMs"] = sw.ElapsedMilliseconds.ToString()
            });
        }
        catch (Exception ex)
        {
            App.CurrentLogger.Error(ex, "插件加载失败", module: EnumLogModule.Plugin);
            AppAnalytics.CaptureException(ex, "plugin.load");
        }
    }

    /// <summary>
    /// 禁用热重载
    /// </summary>
    public void DisableHotReload()
    {
        _hotReload?.Stop();
    }
    
    /// <summary>
    /// 启用热重载
    /// </summary>
    public void EnableHotReload()
    {
        _hotReload?.Start(_pluginsFolder);
    }

    /// <summary>
    /// 重新加载所有插件
    /// </summary>
    public void ReloadPlugins()
    {
        App.CurrentLogger.Log("正在重新加载所有插件", module: EnumLogModule.Plugin);
        AppAnalytics.TrackAction("plugin.reload");
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
        App.CurrentLogger.Log($"已启用插件: {pluginId}", module: EnumLogModule.Plugin);
        AppAnalytics.TrackAction("plugin.enable", new Dictionary<string, string> { ["pluginId"] = pluginId });
        // 触发插件事件：插件启用
        _ = TriggerAsync("plugin.enable", new Dictionary<string, object> { ["pluginId"] = pluginId });
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
        App.CurrentLogger.Log($"已禁用插件: {pluginId}", module: EnumLogModule.Plugin);
        AppAnalytics.TrackAction("plugin.disable", new Dictionary<string, string> { ["pluginId"] = pluginId });
        // 触发插件事件：插件禁用
        _ = TriggerAsync("plugin.disable", new Dictionary<string, object> { ["pluginId"] = pluginId });
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

        var sw = Stopwatch.StartNew();
        try
        {
            await _engine.TriggerAsync(eventName, ctx);
            sw.Stop();
            App.CurrentLogger.LogDebug($"插件事件 {eventName} 执行完成, 耗时 {sw.ElapsedMilliseconds}ms",
                module: EnumLogModule.Plugin);
        }
        catch (Exception ex)
        {
            App.CurrentLogger.Error(ex, $"插件事件 {eventName} 执行失败", module: EnumLogModule.Plugin);
            AppAnalytics.CaptureException(ex, $"plugin.trigger:{eventName}");
        }
    }

    /// <summary>
    /// 安装插件文件
    /// </summary>
    public bool InstallPlugin(string sourcePath, bool backup = true, bool overwrite = false)
    {
        try
        {
            var destFileName = Path.GetFileName(sourcePath);
            var destPath = Path.Combine(_pluginsFolder, destFileName);

            if (File.Exists(destPath) && backup)
            {
                // 备份旧文件
                var bakPath = destPath + ".bak";
                if (File.Exists(bakPath)) File.Delete(bakPath);
                File.Move(destPath, bakPath);
            }

            File.Copy(sourcePath, destPath, overwrite);
            ReloadPlugins();
            App.CurrentLogger.Log($"已安装插件文件: {destFileName}", module: EnumLogModule.Plugin);
            AppAnalytics.TrackAction("plugin.install", new Dictionary<string, string>
            {
                ["file"] = destFileName,
                ["overwrite"] = overwrite.ToString()
            });
            // 触发插件事件：插件安装
            _ = TriggerAsync("plugin.install", new Dictionary<string, object> { ["file"] = destFileName });
            return true;
        }
        catch (Exception ex)
        {
            App.CurrentLogger.Error(ex, "安装插件失败", module: EnumLogModule.Plugin);
            AppAnalytics.CaptureException(ex, "plugin.install");
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
            App.CurrentLogger.Log($"已卸载插件: {info.Name} ({pluginId})", module: EnumLogModule.Plugin);
            AppAnalytics.TrackAction("plugin.uninstall", new Dictionary<string, string> { ["pluginId"] = pluginId });
            // 触发插件事件：插件卸载
            _ = TriggerAsync("plugin.uninstall", new Dictionary<string, object>
            {
                ["pluginId"] = pluginId,
                ["pluginName"] = info.Name
            });
            return true;
        }
        catch (Exception ex)
        {
            App.CurrentLogger.Error(ex, "卸载插件失败", module: EnumLogModule.Plugin);
            AppAnalytics.CaptureException(ex, "plugin.uninstall");
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

    public PluginInfo ExtractPluginInfoFromContent(string content)
    {
        var deserializer = new StaticDeserializerBuilder(new YamlModelStaticContext())
            .WithCaseInsensitivePropertyMatching()
            .IgnoreUnmatchedProperties()
            .Build();
        var raw = deserializer.Deserialize<RawPluginMeta>(content);

        return new PluginInfo
        {
            Id = raw.Id ?? "unknown",
            Name = raw.Name ?? "",
            Description = raw.Description ?? "",
            Author = raw.Author ?? "",
            Version = raw.Version ?? "1.0",
            FilePath = "",
            TriggerCount = raw.Triggers?.Count ?? 0,
            FunctionCount = raw.Functions?.Count ?? 0,
            IsEnabled = true
        };
    }

    private PluginInfo? ExtractPluginInfo(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            var deserializer = new StaticDeserializerBuilder(new YamlModelStaticContext())
                .WithCaseInsensitivePropertyMatching()
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
            App.CurrentLogger.Warning($"解析插件元数据失败: {filePath}, {ex.Message}", module: EnumLogModule.Plugin);
            AppAnalytics.CaptureException(ex, "plugin.parse-meta");
            return null;
        }
    }

    private void SaveDisabledList()
    {
        try
        {
            var path = Path.Combine(_pluginsFolder, ".disabled");
            File.WriteAllText(path,
                JsonSerializer.Serialize([.. _disabledPlugins], App.AppJsonSerializerContext.ListString));
        }
        catch (Exception ex)
        {
            App.CurrentLogger.Error(ex, "保存插件禁用列表失败", module: EnumLogModule.Plugin);
        }
    }

    private void LoadDisabledList()
    {
        var path = Path.Combine(_pluginsFolder, ".disabled");
        if (File.Exists(path))
        {
            try
            {
                var list = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path), App.AppJsonSerializerContext.ListString);
                if (list != null)
                {
                    _disabledPlugins.Clear();
                    _disabledPlugins.AddRange(list);
                }
            }
            catch (Exception ex)
            {
                App.CurrentLogger.Warning($"读取插件禁用列表失败: {ex.Message}", module: EnumLogModule.Plugin);
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
