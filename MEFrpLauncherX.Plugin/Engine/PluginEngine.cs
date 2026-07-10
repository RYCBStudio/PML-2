using MEFrpLauncherX.Plugin.Condition;
using MEFrpLauncherX.Plugin.Core;
using ExecutionContext = MEFrpLauncherX.Plugin.Core.ExecutionContext;

namespace MEFrpLauncherX.Plugin.Engine;


public class PluginEngine
{
    private readonly Dictionary<string, List<PluginDefinition>> _triggerMap = new();
    private readonly FunctionRegistry _funcRegistry = new();
    private readonly Dictionary<string, IAction> _builtinActions;
    private readonly IAction _callFuncAction;

    public PluginEngine()
    {
        // 注册内置指令
        _builtinActions = new Dictionary<string, IAction>
        {
            ["log"] = new LogAction(),
            ["http_request"] = new HttpRequestAction(),
            ["python_run"] = new PythonAction(),
        };
        // 递归执行指令（可注入其他 action）
        _callFuncAction = new CallFunctionAction(_funcRegistry, this._callFuncAction);
        // 将 call_function 注册为内置指令
        _builtinActions["call_function"] = _callFuncAction;
        // 条件指令包裹
        _builtinActions["conditional"] = new ConditionalAction(this._callFuncAction);
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