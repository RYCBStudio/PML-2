using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Plugin.Condition;
using MEFrpLauncherX.Plugin.Core;
using ExecutionContext = MEFrpLauncherX.Plugin.Core.ExecutionContext;

namespace MEFrpLauncherX.Plugin.Engine;

public partial class PluginEngine : IAction
{
    private const int MaxLogEntries = 200;
    private readonly Dictionary<string, List<PluginDefinition>> _triggerMap = new();
    private readonly FunctionRegistry _funcRegistry = new();
    private readonly Dictionary<string, IAction> _builtinActions;
    private readonly CallFunctionAction _callFuncAction;
    private readonly ConcurrentQueue<PluginExecutionLogEntry> _executionLogs = new();

    /// <summary>
    ///     插件注册表（触发器 + 函数）的写锁（26.5.1）。
    ///     <para>
    ///         <see cref="Reload" /> = <see cref="Unload" /> + <see cref="LoadAll" />，
    ///         必须原子执行 —— 否则热重载线程与 UI 线程的显式重载交错时，
    ///         会出现「Unload 清空后、LoadAll 追加前，另一方又 LoadAll 一次」，
    ///         导致同一插件的触发器被注册两次，事件被触发两遍。
    ///     </para>
    /// </summary>
    private readonly object _registryLock = new();

    /// <summary>执行日志新增事件（UI 订阅刷新）</summary>
    public event Action<PluginExecutionLogEntry>? ExecutionLogAdded;

    /// <summary>执行日志只读快照（26.3.1 S4）</summary>
    public IReadOnlyList<PluginExecutionLogEntry> ExecutionLogs => [.. _executionLogs];

    /// <summary>清空执行日志</summary>
    public void ClearExecutionLogs()
    {
        while (_executionLogs.TryDequeue(out _))
        {
        }
    }

    private void AddExecutionLog(PluginExecutionLogEntry entry)
    {
        _executionLogs.Enqueue(entry);
        while (_executionLogs.Count > MaxLogEntries) _executionLogs.TryDequeue(out _);
        ExecutionLogAdded?.Invoke(entry);
    }

    public PluginEngine()
    {
        // 注册内置指令
        _builtinActions = new Dictionary<string, IAction>
        {
            ["log"] = new LogAction(),
            //["http_request"] = new HttpRequestAction(),
            ["python_run"] = new PythonAction(),
            ["notify"] = new NotifyAction(),
            ["local_run"] = new LocalRunAction(),
            // 26.3.1 S2：重启隧道（能力由主程序经 ProxyActionBridge 注册）
            ["proxy.restart"] = new ProxyRestartAction(),
            // 26.3.1 M5：打开 URL（系统默认浏览器）
            ["open_url"] = new OpenUrlAction(),
            ["wait"] = new WaitAction()
        };
        // call_function 指令：通过 this (IAction) 作为子动作分发器
        _callFuncAction = new CallFunctionAction(_funcRegistry, this);
        _builtinActions["call_function"] = _callFuncAction;
        // 条件指令包裹
        _builtinActions["conditional"] = new ConditionalAction(this);
    }

    /// <summary>
    /// IAction 实现：作为子动作分发器，args 中需包含 "__actionName" 键用于查找内置指令
    /// </summary>
    Task IAction.ExecuteAsync(ExecutionContext ctx, Dictionary<string, object>? args)
    {
        if (args == null || !args.TryGetValue("__actionName", out var nameObj))
            return Task.CompletedTask;
        var def = new ActionDefinition
        {
            Name = nameObj.ToString() ?? "",
            Params = args
        };
        return ExecuteAction(def, ctx);
    }

    public void LoadAll(string pluginsFolder) =>
        LoadAll([pluginsFolder], null);

    /// <summary>
    /// 加载所有插件。
    /// <para>
    ///     <paramref name="pluginsFolders" />：要扫描的插件目录（可多个，数据目录优先）。
    ///     <paramref name="disabledPluginIds" />：已禁用的插件 ID 集合 —— 这些插件<b>不注册触发器</b>，
    ///     禁用才真正生效（原先禁用只是 UI 上的一个标记，插件照样被触发）。
    /// </para>
    /// </summary>
    public void LoadAll(IReadOnlyList<string> pluginsFolders, ISet<string>? disabledPluginIds = null)
    {
        var preprocessor = new PluginPreprocessor();
        var loaded = 0;
        var failed = 0;
        var skippedNonEvent = 0;
        var skippedDisabled = 0;
        var seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 26.5.1：注册表的写入必须与 Unload 互斥 —— 否则并发重载会让同一插件
        // 的触发器被注册两次（事件被触发两遍）。
        lock (_registryLock)
        {
            foreach (var pluginsFolder in pluginsFolders)
            {
                if (!Directory.Exists(pluginsFolder))
                {
                    continue;
                }

                foreach (var file in Directory.GetFiles(pluginsFolder, "*.yaml", SearchOption.AllDirectories))
                {
                    // 同一文件名在多个目录出现时只处理一次（数据目录优先）。
                    if (!seenFiles.Add(Path.GetFileName(file)))
                    {
                        continue;
                    }

                    try
                    {
                        var plugin = preprocessor.Process(file, _funcRegistry);
                        if (plugin.Id == "错误")
                        {
                            failed++;
                            continue;
                        }

                        // 仅事件类插件参与触发器注册；create-proxy-template 等资源型插件不进入事件引擎
                        if (!string.Equals(plugin.Type, "event", StringComparison.OrdinalIgnoreCase))
                        {
                            skippedNonEvent++;
                            continue;
                        }

                        // 26.5.1：被禁用的插件不注册触发器（禁用必须真正生效）。
                        if (disabledPluginIds is not null && disabledPluginIds.Contains(plugin.Id))
                        {
                            skippedDisabled++;
                            continue;
                        }

                        loaded++;
                        foreach (var trigger in plugin.Triggers)
                        {
                            if (!_triggerMap.ContainsKey(trigger.On))
                                _triggerMap[trigger.On] = [];
                            _triggerMap[trigger.On].Add(plugin);
                        }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        App.CurrentLogger.Warning($"加载插件文件失败: {file}, {ex.Message}",
                            module: EnumLogModule.Plugin);
                    }
                }
            }
        }

        App.CurrentLogger.Log(
            $"插件引擎加载完成: 成功 {loaded} 个, 失败 {failed} 个, 跳过非事件类型 {skippedNonEvent} 个, 跳过已禁用 {skippedDisabled} 个, 监听事件 {_triggerMap.Count} 种, 注册函数 {_funcRegistry.Count} 个",
            module: EnumLogModule.Plugin);
    }

    public async Task TriggerAsync(string eventName, ExecutionContext context)
    {
        if (!_triggerMap.TryGetValue(eventName, out var plugins))
        {
            App.CurrentLogger.LogDebug($"插件事件 {eventName} 无订阅插件, 跳过", module: EnumLogModule.Plugin);
            return;
        }

        App.CurrentLogger.LogDebug($"插件事件 {eventName} 触发, 命中 {plugins.Count} 个插件", module: EnumLogModule.Plugin);
        foreach (var plugin in plugins)
        {
            foreach (var trigger in plugin.Triggers.Where(trigger => trigger.On == eventName))
            {
                // 条件判断
                if (!string.IsNullOrEmpty(trigger.Condition))
                {
                    bool matched;
                    try
                    {
                        var condition = ConditionParser.Parse(trigger.Condition);
                        matched = condition.Evaluate(context);
                    }
                    catch (Exception ex)
                    {
                        App.CurrentLogger.Warning(
                            $"插件 {plugin.Id} 条件解析/求值失败: {trigger.Condition}, {ex.Message}",
                            module: EnumLogModule.Plugin);
                        AddExecutionLog(new PluginExecutionLogEntry
                        {
                            PluginId = plugin.Id,
                            EventName = eventName,
                            Condition = trigger.Condition,
                            Status = "failed",
                            Message = $"条件解析/求值失败: {ex.Message}"
                        });
                        continue;
                    }

                    if (!matched)
                    {
                        App.CurrentLogger.LogDebug($"插件 {plugin.Id} 条件不满足, 跳过事件 {eventName}",
                            module: EnumLogModule.Plugin);
                        AddExecutionLog(new PluginExecutionLogEntry
                        {
                            PluginId = plugin.Id,
                            EventName = eventName,
                            Condition = trigger.Condition,
                            ConditionMatched = false,
                            Status = "skipped",
                            Message = "条件不满足, 已跳过"
                        });
                        continue;
                    }
                }

                // 执行动作
                App.CurrentLogger.LogDebug($"插件 {plugin.Id} 开始执行事件 {eventName} 的 {trigger.Actions.Count} 个动作",
                    module: EnumLogModule.Plugin);
                AddExecutionLog(new PluginExecutionLogEntry
                {
                    PluginId = plugin.Id,
                    EventName = eventName,
                    Condition = trigger.Condition,
                    ConditionMatched = true,
                    Status = "info",
                    Message = $"事件命中, 执行 {trigger.Actions.Count} 个动作"
                });
                var ctx = new ExecutionContext
                {
                    PluginId = plugin.Name,
                    Variables = context.Variables,
                    Data = context.Data
                };
                foreach (var actionDef in trigger.Actions)
                {
                    await ExecuteAction(actionDef, ctx);
                }
            }
        }
    }

    public async Task ExecuteAction(ActionDefinition def, ExecutionContext ctx)
    {
        if (!_builtinActions.TryGetValue(def.Name, out var action))
        {
            App.CurrentLogger.Warning($"未知插件指令: {def.Name} (插件: {ctx.PluginId})", module: EnumLogModule.Plugin);
            AddExecutionLog(new PluginExecutionLogEntry
            {
                PluginId = ctx.PluginId,
                ActionName = def.Name,
                Status = "failed",
                Message = "未知插件指令"
            });
            return;
        }

        try
        {
            // 模板替换（简单实现）
            var resolved = ResolveTemplates(def.Params, ctx);
            await action.ExecuteAsync(ctx, resolved);
            AddExecutionLog(new PluginExecutionLogEntry
            {
                PluginId = ctx.PluginId,
                ActionName = def.Name,
                Status = "success",
                Message = "执行成功"
            });
        }
        catch (Exception ex)
        {
            App.CurrentLogger.Error(ex, $"插件指令 {def.Name} 执行失败 (插件: {ctx.PluginId})",
                module: EnumLogModule.Plugin);
            AddExecutionLog(new PluginExecutionLogEntry
            {
                PluginId = ctx.PluginId,
                ActionName = def.Name,
                Status = "failed",
                Message = $"执行失败: {ex.Message}"
            });
            throw;
        }
    }

    /// <summary>
    /// 卸载所有已加载的插件（保留内置指令）
    /// </summary>
    public void Unload()
    {
        lock (_registryLock)
        {
            _triggerMap.Clear();
            // 26.5.1：函数注册表也要清 —— 否则被卸载插件定义的函数仍然可被 call_function 调用，
            // 且注册表会随每次重载单调增长。
            _funcRegistry.Clear();
        }

        App.CurrentLogger.LogDebug("插件引擎已卸载所有插件", module: EnumLogModule.Plugin);
    }

    /// <summary>
    /// 重新加载插件（先卸载再加载）
    /// </summary>
    public void Reload(string pluginsFolder) =>
        Reload([pluginsFolder], null);

    /// <summary>
    /// 重新加载插件（先卸载再加载）。
    /// <para>
    ///     26.5.1：这是「替换」而非「追加」—— 直接调用 <see cref="LoadAll(string)" />
    ///     会把 trigger 追加到已有列表上，导致重载后事件被触发 N 次。
    /// </para>
    /// </summary>
    public void Reload(IReadOnlyList<string> pluginsFolders, ISet<string>? disabledPluginIds = null)
    {
        // 26.5.1：整个「卸载 + 重载」必须原子执行 ——
        // Monitor 可重入，所以这里持锁后调用 Unload/LoadAll 不会自锁，
        // 但能防止另一线程（热重载）在 Unload 与 LoadAll 之间插入 LoadAll，
        // 导致同一插件的触发器被注册两次。
        lock (_registryLock)
        {
            Unload();
            LoadAll(pluginsFolders, disabledPluginIds);
        }
    }

    private static readonly Regex TemplateRegex = MyRegex();

    private Dictionary<string, object> ResolveTemplates(Dictionary<string, object> args, ExecutionContext ctx)
    {
        var resolved = new Dictionary<string, object>();
        foreach (var kv in args)
        {
            if (kv.Value is string str && str.Contains("{{"))
            {
                // 支持一条字符串内嵌多个 {{...}} 模板；每个模板可为 ctx 路径或值表达式（含内置函数）
                var sb = new StringBuilder();
                var last = 0;
                foreach (Match m in TemplateRegex.Matches(str))
                {
                    sb.Append(str, last, m.Index - last);
                    var expr = m.Groups[1].Value.Trim();
                    sb.Append(ResolveTemplate(expr, ctx) ?? m.Value);
                    last = m.Index + m.Length;
                }

                sb.Append(str, last, str.Length - last);
                resolved[kv.Key] = sb.ToString();
            }
            else
            {
                resolved[kv.Key] = kv.Value;
            }
        }

        return resolved;
    }

    private static object? ResolveTemplate(string expr, ExecutionContext ctx)
    {
        // 路径模板：{{ctx.data.xxx}} / {{ctx.variables.xxx}}
        if (expr.StartsWith("ctx.data.", StringComparison.Ordinal) ||
            expr.StartsWith("ctx.variables.", StringComparison.Ordinal))
        {
            return PropertyAccessor.GetValue(ctx, expr);
        }

        // 其他视为值表达式（26.3.1 M5）：{{len(ctx.data.proxyName)}} / {{3 + 2}} 等
        try
        {
            var node = ConditionParser.ParseValue(expr);
            return node.Evaluate(ctx)?.ToString();
        }
        catch
        {
            return null; // 求值失败保留原始模板文本
        }
    }

    [GeneratedRegex(@"\{\{(.+?)\}\}", RegexOptions.Compiled)]
    private static partial Regex MyRegex();
}