using MEFrpLauncherX.Core;
using MEFrpLauncherX.Plugin.Condition;
using MEFrpLauncherX.Plugin.Core;
using ExecutionContext = MEFrpLauncherX.Plugin.Core.ExecutionContext;

namespace MEFrpLauncherX.Plugin.Engine;

public class PluginEngine : IAction
{
    private readonly Dictionary<string, List<PluginDefinition>> _triggerMap = new();
    private readonly FunctionRegistry _funcRegistry = new();
    private readonly Dictionary<string, IAction> _builtinActions;
    private readonly CallFunctionAction _callFuncAction;

    public PluginEngine()
    {
        // 注册内置指令
        _builtinActions = new Dictionary<string, IAction>
        {
            ["log"] = new LogAction(),
            ["http_request"] = new HttpRequestAction(),
            ["python_run"] = new PythonAction(),
            ["notify"] = new NotifyAction()
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

    public void LoadAll(string pluginsFolder)
    {
        var preprocessor = new PluginPreprocessor();
        var loaded = 0;
        var failed = 0;
        foreach (var file in Directory.GetFiles(pluginsFolder, "*.yaml", SearchOption.AllDirectories))
        {
            try
            {
                var plugin = preprocessor.Process(file, _funcRegistry);
                if (plugin.Id == "错误")
                {
                    failed++;
                    continue;
                }

                loaded++;
                foreach (var trigger in plugin.Triggers)
                {
                    if (!_triggerMap.ContainsKey(trigger.On))
                        _triggerMap[trigger.On] = new List<PluginDefinition>();
                    _triggerMap[trigger.On].Add(plugin);
                }
            }
            catch (Exception ex)
            {
                failed++;
                App.CurrentLogger.Warning($"加载插件文件失败: {file}, {ex.Message}", module: EnumLogModule.Plugin);
            }
        }

        App.CurrentLogger.Log(
            $"插件引擎加载完成: 成功 {loaded} 个, 失败 {failed} 个, 监听事件 {_triggerMap.Count} 种, 注册函数 {_funcRegistry.Count} 个",
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
                        continue;
                    }

                    if (!matched)
                    {
                        App.CurrentLogger.LogDebug($"插件 {plugin.Id} 条件不满足, 跳过事件 {eventName}",
                            module: EnumLogModule.Plugin);
                        continue;
                    }
                }

                // 执行动作
                App.CurrentLogger.LogDebug($"插件 {plugin.Id} 开始执行事件 {eventName} 的 {trigger.Actions.Count} 个动作",
                    module: EnumLogModule.Plugin);
                var ctx = new ExecutionContext()
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
            return;
        }

        try
        {
            // 模板替换（简单实现）
            var resolved = ResolveTemplates(def.Params, ctx);
            await action.ExecuteAsync(ctx, resolved);
        }
        catch (Exception ex)
        {
            App.CurrentLogger.Error(ex, $"插件指令 {def.Name} 执行失败 (插件: {ctx.PluginId})",
                module: EnumLogModule.Plugin);
            throw;
        }
    }

    /// <summary>
    /// 卸载所有已加载的插件（保留内置指令）
    /// </summary>
    public void Unload()
    {
        _triggerMap.Clear();
        App.CurrentLogger.LogDebug("插件引擎已卸载所有插件", module: EnumLogModule.Plugin);
    }

    /// <summary>
    /// 重新加载插件（先卸载再加载）
    /// </summary>
    public void Reload(string pluginsFolder)
    {
        Unload();
        LoadAll(pluginsFolder);
    }

    private Dictionary<string, object> ResolveTemplates(Dictionary<string, object> args, ExecutionContext ctx)
    {
        var resolved = new Dictionary<string, object>();
        foreach (var kv in args)
        {
            if (kv.Value is string str && str.Contains("{{") && str.Contains("}}"))
            {
                // 仅支持 {{ctx.variables.xxx}} 或 {{ctx.data.xxx}}
                str = str.Substring(str.IndexOf("{{", StringComparison.Ordinal),
                    str.LastIndexOf("}}", StringComparison.Ordinal) - str.IndexOf("{{", StringComparison.Ordinal) + 2);
                var path = str.Replace("{{", "").Replace("}}", "");
                var val = PropertyAccessor.GetValue(ctx, path);
                resolved[kv.Key] = val ?? str;
            }
            else
            {
                resolved[kv.Key] = kv.Value;
            }
        }

        return resolved;
    }
}