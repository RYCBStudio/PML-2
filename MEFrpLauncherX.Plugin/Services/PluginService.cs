using System.Diagnostics;
using System.Text.Json;
using AvaloniaEdit.Utils;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Analysis;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Plugin.Core;
using MEFrpLauncherX.Plugin.Engine;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using ExecutionContext = MEFrpLauncherX.Plugin.Core.ExecutionContext;

namespace MEFrpLauncherX.Plugin.Services;

public class PluginService
{
    private static PluginService? _instance;
    public static PluginService Instance => _instance ??= new PluginService();

    private readonly PluginEngine _engine = new();
    private HotReloadService? _hotReload;
    private readonly HashSet<string> _disabledPlugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PluginInfo> _plugins = [];

    /// <summary>
    ///     插件目录（用户数据，可写）：<c>data\Config\Plugins\</c>。
    ///     <para>
    ///         26.5.1：原先用的是 <c>App.StartupPath\Config\Plugins</c>，而
    ///         <see cref="App.StartupPath" /> 在 26.5.0 已被重定义为<b>版本（代码）目录</b>
    ///         （<c>vXXX\</c>），在发布版是只读的 —— 于是插件安装 / 保存 / 禁用列表写入
    ///         全部失败（Linux 的 <c>/opt/pml-2</c> 更由 root 拥有，直接抛
    ///         <see cref="UnauthorizedAccessException" /> 导致应用无法启动）。
    ///     </para>
    /// </summary>
    private readonly string _pluginsFolder;

    /// <summary>
    ///     读取插件时扫描的目录集合。
    ///     <para>
    ///         读取与写入分离：<b>写入</b>只发生在 <see cref="_pluginsFolder" />（用户数据目录）；
    ///         <b>读取</b>同时扫描「随包分发的只读目录」与「用户数据目录」，
    ///         以兼容 26.5.0 及更早版本把插件随代码目录发布的布局（此时旧插件在
    ///         <c>vXXX\Config\Plugins</c>，升级后不能因为改成读数据目录就全部消失）。
    ///     </para>
    /// </summary>
    private readonly string[] _readFolders;

    public IReadOnlyList<PluginInfo> Plugins => _plugins;

    public bool IsLoaded { get; private set; }

    /// <summary>插件目录（用户可写的数据目录，供 UI「打开插件文件夹」使用）。</summary>
    public string PluginsFolder => _pluginsFolder;

    // ---- 执行日志（26.3.1 S4）----

    /// <summary>执行日志只读快照</summary>
    public IReadOnlyList<PluginExecutionLogEntry> ExecutionLogs => _engine.ExecutionLogs;

    /// <summary>执行日志新增事件（UI 订阅刷新）</summary>
    public event Action<PluginExecutionLogEntry>? ExecutionLogAdded;

    /// <summary>清空执行日志</summary>
    public void ClearExecutionLogs() => _engine.ClearExecutionLogs();

    private void OnExecutionLogAdded(PluginExecutionLogEntry entry) => ExecutionLogAdded?.Invoke(entry);

    private PluginService()
    {
        // 写入目标：用户数据目录（可写、升级不覆盖）。
        _pluginsFolder = AppPaths.PluginsDirectory;

        // 读取目标：数据目录优先，其后是版本目录（兼容旧版把插件随代码发布的布局）。
        // 两者去重，避免开发态下两个路径指向同一处时重复加载同一插件。
        var bundledFolder = Path.Combine(App.StartupPath, "Config", "Plugins");
        _readFolders = Path.GetFullPath(bundledFolder) == Path.GetFullPath(_pluginsFolder)
            ? [_pluginsFolder]
            : [_pluginsFolder, bundledFolder];

        EnsurePluginsDirectory();
        _engine.ExecutionLogAdded += OnExecutionLogAdded;
    }

    /// <summary>
    ///     确保插件目录存在。
    ///     <para>
    ///         失败只记录日志、不抛出：目录不可写（只读介质 / 权限不足）不应导致应用
    ///         无法启动，插件子系统的降级交给调用方（加载 0 个插件即可）。
    ///     </para>
    /// </summary>
    private void EnsurePluginsDirectory()
    {
        try
        {
            Directory.CreateDirectory(_pluginsFolder);
        }
        catch (Exception ex)
        {
            App.CurrentLogger?.Error(ex, $"创建插件目录失败（插件功能不可用）: {_pluginsFolder}",
                module: EnumLogModule.Plugin);
        }
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

            // 26.5.1：先读回「已禁用插件」列表 —— 原先只写不读，禁用状态重启即失效。
            LoadDisabledList();

            // 26.5.1：用 Reload（先 Unload 再 LoadAll）而非直接 LoadAll，
            // 否则每次重载都会把 trigger 追加到已有列表上，事件被触发 N 次。
            // 被禁用的插件不注册 trigger（禁用必须真正生效）。
            _engine.Reload(_readFolders, _disabledPlugins);

            // 收集插件元数据
            foreach (var file in EnumeratePluginFiles())
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
            _hotReload.Start(_readFolders);

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
            // 26.5.1：这里必须判空 —— App.CurrentLogger 在 App.Initialize() 之前是 null，
            // 而 PluginService.Instance 正是在 Initialize() 里被构造的；
            // 若不判空，catch 块自身抛 NRE 会把真正的原始异常掩盖掉。
            App.CurrentLogger?.Error(ex, "插件加载失败", module: EnumLogModule.Plugin);
            AppAnalytics.CaptureException(ex, "plugin.load");
        }
    }

    /// <summary>
    /// 枚举全部插件文件（扫描所有读取目录）。
    /// <para>同一文件名在多个目录出现时，只取优先级更高的那一个（数据目录优先）。</para>
    /// </summary>
    private List<string> EnumeratePluginFiles()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new List<string>();
        foreach (var folder in _readFolders)
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            foreach (var file in Directory.GetFiles(folder, "*.yaml", SearchOption.AllDirectories))
            {
                // 用相对路径 + 文件名做去重键：不同目录下的同名插件只加载一次。
                var key = Path.GetFileName(file);
                if (seen.Add(key))
                {
                    files.Add(file);
                }
            }
        }

        return files;
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
        _hotReload?.Start(_readFolders);
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
        // 26.5.1：立即重载，让「启用」真正生效（原先只在下次启动才生效）。
        ReloadPlugins();
        App.CurrentLogger.Log($"已启用插件: {pluginId}", module: EnumLogModule.Plugin);
        AppAnalytics.TrackAction("plugin.enable", new Dictionary<string, string> { ["pluginId"] = pluginId });
        // 触发插件事件：插件启用
        _ = TriggerAsync("plugin.enable", new Dictionary<string, object> { ["pluginId"] = pluginId });
    }

    /// <summary>
    /// 禁用插件
    /// </summary>
    public void DisablePlugin(string pluginId)
    {
        _disabledPlugins.Add(pluginId);
        var info = _plugins.FirstOrDefault(p => p.Id == pluginId);
        if (info != null) info.IsEnabled = false;
        SaveDisabledList();
        // 26.5.1：立即重载，让「禁用」真正生效 —— 被禁插件的触发器必须立刻注销，
        // 否则它仍会继续响应事件。
        ReloadPlugins();
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
    /// 用给定内容覆盖已安装插件：就地写入原文件（保留文件名，避免出现同名不同文件的重复插件），
    /// 写入前把旧文件备份为 <c>.bak</c>。不主动触发重载，批量更新后由调用方统一 <see cref="ReloadPlugins" />。
    /// <para>
    ///     26.5.1：写入目标统一为<b>用户数据目录</b>。若插件来自随包分发的只读版本目录，
    ///     则在数据目录写入同名文件「遮蔽」它（读取时数据目录优先），而不是尝试写只读目录。
    /// </para>
    /// </summary>
    public bool UpdatePluginContent(string pluginId, string content)
    {
        var info = _plugins.FirstOrDefault(p => p.Id == pluginId);
        if (info == null || string.IsNullOrEmpty(info.FilePath) || !File.Exists(info.FilePath))
        {
            return false;
        }

        try
        {
            // 目标始终位于可写的数据目录；随包插件则在此「遮蔽」同名文件。
            var targetPath = Path.Combine(_pluginsFolder, Path.GetFileName(info.FilePath));

            // 先写临时文件再原子替换，避免写到一半崩溃留下半截 YAML
            var tmp = targetPath + ".tmp";
            File.WriteAllText(tmp, content);

            if (File.Exists(targetPath))
            {
                var bak = targetPath + ".bak";
                File.Copy(targetPath, bak, true);
            }

            File.Move(tmp, targetPath, true);

            App.CurrentLogger.Log($"已更新插件: {info.Name} ({pluginId})", module: EnumLogModule.Plugin);
            AppAnalytics.TrackAction("plugin.update", new Dictionary<string, string>
            {
                ["pluginId"] = pluginId,
                ["from"] = info.Version
            });
            // 触发插件事件：插件更新（沿用安装事件语义，便于监听方刷新缓存）
            _ = TriggerAsync("plugin.install", new Dictionary<string, object> { ["file"] = Path.GetFileName(targetPath) });
            return true;
        }
        catch (Exception ex)
        {
            App.CurrentLogger.Error(ex, $"更新插件失败: {pluginId}", module: EnumLogModule.Plugin);
            AppAnalytics.CaptureException(ex, "plugin.update");
            return false;
        }
    }

    /// <summary>
    /// 卸载插件
    /// <para>
    ///     26.5.1：只删除「用户数据目录」里的插件文件。随包分发的插件位于只读的版本目录，
    ///     删不掉也不该删 —— 用户想让它不再生效应当使用「禁用」。
    /// </para>
    /// </summary>
    public bool UninstallPlugin(string pluginId)
    {
        var info = _plugins.FirstOrDefault(p => p.Id == pluginId);
        if (info == null) return false;

        try
        {
            // 仅当插件文件确实位于可写的数据目录时才删除。
            var isUserPlugin = !string.IsNullOrEmpty(info.FilePath) &&
                               Path.GetFullPath(info.FilePath)
                                   .StartsWith(Path.GetFullPath(_pluginsFolder) + Path.DirectorySeparatorChar,
                                       StringComparison.OrdinalIgnoreCase);
            if (isUserPlugin && File.Exists(info.FilePath))
            {
                File.Delete(info.FilePath);
            }

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

    // ---- 表单编辑器支持（26.3.1 S6）----

    private static readonly IDeserializer YamlDeserializer =
        new StaticDeserializerBuilder(new YamlModelStaticContext())
            .WithCaseInsensitivePropertyMatching()
            .IgnoreUnmatchedProperties()
            .Build();

    private static readonly ISerializer YamlSerializer =
        new StaticSerializerBuilder(new YamlModelStaticContext())
            .WithNamingConvention(CamelCaseNamingConvention.Instance) // 输出文档 schema 小写字段：id/name/on/actions/params
            .Build();

    /// <summary>将 YAML 字符串反序列化为插件原始模型（失败抛异常）</summary>
    public RawPlugin DeserializePluginYaml(string content) => YamlDeserializer.Deserialize<RawPlugin>(content);

    /// <summary>将插件原始模型序列化为 YAML 字符串</summary>
    public string SerializePluginYaml(RawPlugin plugin) => YamlSerializer.Serialize(plugin);

    /// <summary>
    ///     校验插件文件名为合法相对文件名（防路径穿越），并一次性完整写入 Config/Plugins。
    ///     写入后由热重载自动拾取。
    /// </summary>
    public bool SavePluginContent(string fileName, string content, out string? error)
    {
        error = null;
        var safeName = Path.GetFileName(fileName);
        if (safeName != fileName ||
            !(safeName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
              safeName.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)))
        {
            error = $"非法文件名: {fileName}";
            return false;
        }

        try
        {
            var dest = Path.Combine(_pluginsFolder, safeName);
            // 完整内容一次性写入，避免保存一半触发重载
            File.WriteAllText(dest, content);
            App.CurrentLogger.Log($"插件已保存: {safeName}", module: EnumLogModule.Plugin);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            App.CurrentLogger.Error(ex, $"保存插件失败: {safeName}", module: EnumLogModule.Plugin);
            return false;
        }
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
            Type = raw.Type ?? "event",
            MinCoreVersion = raw.MinCoreVersion,
            IsCompatible = PluginPreprocessor.IsCoreSatisfied(raw.MinCoreVersion),
            TemplateCount = raw.Templates?.Count ?? 0,
            TriggerCount = raw.Triggers?.Count ?? 0,
            FunctionCount = raw.Functions?.Count ?? 0,
            IsEnabled = true
        };
    }

    /// <summary>
    /// 收集当前可用的隧道模板条目：仅包含 type=create-proxy-template、未禁用、核心版本兼容的插件。
    /// 实时扫描全部插件目录（调用频率低：进入引导页/热重载/启停时），无需维护缓存。
    /// </summary>
    public List<ProxyTemplateEntry> GetEnabledProxyTemplateEntries()
    {
        var result = new List<ProxyTemplateEntry>();
        try
        {
            foreach (var file in EnumeratePluginFiles())
            {
                try
                {
                    var content = File.ReadAllText(file);
                    var raw = YamlDeserializer.Deserialize<RawPlugin>(content);
                    if (!string.Equals(raw.Type, "create-proxy-template", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var pluginId = string.IsNullOrWhiteSpace(raw.Id)
                        ? Path.GetFileNameWithoutExtension(file)
                        : raw.Id;
                    if (_disabledPlugins.Contains(pluginId) || !PluginPreprocessor.IsCoreSatisfied(raw.MinCoreVersion))
                    {
                        continue;
                    }

                    foreach (var template in raw.Templates ?? [])
                    {
                        result.Add(new ProxyTemplateEntry
                        {
                            PluginId = pluginId,
                            PluginName = raw.Name,
                            Definition = template
                        });
                    }
                }
                catch (Exception ex)
                {
                    App.CurrentLogger.Warning($"读取模板插件失败: {file}, {ex.Message}", module: EnumLogModule.Plugin);
                }
            }
        }
        catch (Exception ex)
        {
            App.CurrentLogger.Error(ex, "读取模板插件目录失败", module: EnumLogModule.Plugin);
        }

        return result;
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
                Type = raw.Type ?? "event",
                MinCoreVersion = raw.MinCoreVersion,
                IsCompatible = PluginPreprocessor.IsCoreSatisfied(raw.MinCoreVersion),
                TemplateCount = raw.Templates?.Count ?? 0,
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

    /// <summary>插件类型：event（默认，缺省 type 字段的旧插件）或 create-proxy-template</summary>
    public string Type { get; set; } = "event";

    /// <summary>要求的最低核心版本（为空表示不限制）</summary>
    public string? MinCoreVersion { get; set; }

    /// <summary>核心版本是否满足 minCoreVersion（不兼容插件不加载、不提供能力）</summary>
    public bool IsCompatible { get; set; } = true;

    /// <summary>隧道模板条数（仅 create-proxy-template 类型有意义）</summary>
    public int TemplateCount { get; set; }

    public int TriggerCount { get; set; }
    public int FunctionCount { get; set; }
    public bool IsEnabled { get; set; } = true;

    /// <summary>在线市场中的最新版本（未比对或无需更新时为 null）</summary>
    public string? LatestVersion { get; set; }

    /// <summary>在线市场存在比已安装版本更高的版本</summary>
    public bool UpdateAvailable { get; set; }

    /// <summary>是否为资源型插件（模板插件等，非事件插件）</summary>
    public bool IsTemplatePlugin =>
        !string.Equals(Type, "event", StringComparison.OrdinalIgnoreCase);

    /// <summary>核心版本是否不满足插件要求（UI 展示不兼容提示）</summary>
    public bool IsIncompatible => !IsCompatible;

    /// <summary>不兼容提示（如：需要核心版本 26.3.2 或更高）</summary>
    public string IncompatibleTip => string.Format(Languages.Text_PluginList_RequireCoreFormat, MinCoreVersion ?? "");
}

/// <summary>
/// 来自模板插件的隧道模板条目（含来源插件信息）
/// </summary>
public class ProxyTemplateEntry
{
    public string PluginId { get; init; } = "";
    public string PluginName { get; init; } = "";
    public ProxyTemplateDefinition Definition { get; init; } = new();
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
    public string? Type { get; set; }
    public string? MinCoreVersion { get; set; }
    public List<ProxyTemplateDefinition>? Templates { get; set; }
    public List<object>? Triggers { get; set; }
    public Dictionary<string, object>? Functions { get; set; }
}
