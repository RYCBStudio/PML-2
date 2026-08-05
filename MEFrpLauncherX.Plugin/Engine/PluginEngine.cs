using System.IO;
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
        foreach (var file in Directory.GetFiles(pluginsFolder, "*.yaml", SearchOption.AllDirectories))
        {
            var plugin = preprocessor.Process(file, _funcRegistry);
            foreach (var trigger in plugin.Triggers)
            {
                if (!_triggerMap.ContainsKey(trigger.On))
                    _triggerMap[trigger.On] = new List<PluginDefinition>();
                _triggerMap[trigger.On].Add(plugin);
            }
        }
    }

    public async Task TriggerAsync(string eventName, ExecutionContext context)
    {
        if (!_triggerMap.TryGetValue(eventName, out var plugins)) return;
        foreach (var plugin in plugins)
        {
            foreach (var trigger in plugin.Triggers)
            {
                if (trigger.On != eventName) continue;
                // 条件判断
                if (!string.IsNullOrEmpty(trigger.Condition))
                {
                    var condition = ConditionParser.Parse(trigger.Condition);
                    if (!condition.Evaluate(context)) continue;
                }
                // 执行动作
                foreach (var actionDef in trigger.Actions)
                {
                    await ExecuteAction(actionDef, context);
                }
            }
        }
    }

    public async Task ExecuteAction(ActionDefinition def, ExecutionContext ctx)
    {
        if (_builtinActions.TryGetValue(def.Name, out var action))
        {
            // 模板替换（简单实现）
            var resolved = ResolveTemplates(def.Params, ctx);
            await action.ExecuteAsync(ctx, resolved);
        }
    }

    /// <summary>
    /// 卸载所有已加载的插件（保留内置指令）
    /// </summary>
    public void Unload()
    {
        _triggerMap.Clear();
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