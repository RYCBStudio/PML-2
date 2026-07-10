using System.Diagnostics;
using System.Text;
using System.Text.Json;
using MEFrpLauncherX.Plugin.Condition;
using MEFrpLauncherX.Plugin.Engine;

namespace MEFrpLauncherX.Plugin.Core;

public class LogAction : IAction
{
    public Task ExecuteAsync(ExecutionContext ctx, Dictionary<string, object>? args)
    {
        var msg = args?.GetValueOrDefault("msg")?.ToString() ?? "No message";
        Console.WriteLine($"[{ctx.PluginId}] {msg}");
        return Task.CompletedTask;
    }
}

public class HttpRequestAction : IAction
{
    public async Task ExecuteAsync(ExecutionContext ctx, Dictionary<string, object>? args)
    {
        var url = args?["url"]?.ToString() ?? "";
        var method = args?.GetValueOrDefault("method")?.ToString() ?? "GET";
        var body = args?.GetValueOrDefault("body")?.ToString();

        using var client = new HttpClient();
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (body != null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        ctx.Variables["http_response"] = content;
    }
}

public class PythonAction : IAction
{
    public async Task ExecuteAsync(ExecutionContext ctx, Dictionary<string, object>? args)
    {
        var script = args?["script"]?.ToString() ?? "";
        var input = args?.GetValueOrDefault("input")?.ToString() ?? "{}";

        // 调用 Python 进程 (需要 python3 在 PATH 中)
        var psi = new ProcessStartInfo
        {
            FileName = "python3",
            Arguments =
                $"-c \"import sys, json, importlib.util; spec=importlib.util.spec_from_file_location('mod', '{script}'); mod=importlib.util.module_from_spec(spec); spec.loader.exec_module(mod); print(json.dumps(mod.main(json.loads(sys.stdin.read()))))\"",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();
        await process.StandardInput.WriteAsync(input);
        process.StandardInput.Close();

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        // 将 Python 返回的 JSON 合并到 Variables
        if (!string.IsNullOrWhiteSpace(output))
        {
            try
            {
                var result =
                    JsonSerializer.Deserialize<Dictionary<string, object>>(output);
                if (result != null)
                    foreach (var kv in result)
                        ctx.Variables[kv.Key] = kv.Value;
            }
            catch
            {
                ctx.Variables["python_output"] = output;
            }
        }
    }
}

public class CallFunctionAction : IAction
{
    private readonly FunctionRegistry _funcRegistry;
    private readonly IAction _subActionExecutor;

    public CallFunctionAction(FunctionRegistry funcRegistry, IAction subActionExecutor)
    {
        _funcRegistry = funcRegistry;
        _subActionExecutor = subActionExecutor;
    }

    public async Task ExecuteAsync(ExecutionContext ctx, Dictionary<string, object>? args)
    {
        var funcName = args?["func"]?.ToString() ?? "";
        var funcArgs = args?.GetValueOrDefault("args") as Dictionary<string, object> ?? new();

        var funcActions = _funcRegistry.Get(funcName);
        if (funcActions == null) return;

        var newCtx = ctx.CloneWithArgs(funcArgs);
        foreach (var actionDef in funcActions)
        {
            await _subActionExecutor.ExecuteAsync(newCtx, actionDef.Params);
        }
    }
}

// 条件执行包装（用于单个 action）
public class ConditionalAction : IAction
{
    private readonly IAction _inner;
    public ConditionalAction(IAction inner) => _inner = inner;

    public async Task ExecuteAsync(ExecutionContext ctx, Dictionary<string, object>? args)
    {
        if (args == null) return;
        var conditionStr = args.GetValueOrDefault("condition")?.ToString();
        if (string.IsNullOrEmpty(conditionStr)) return;

        var condition = ConditionParser.Parse(conditionStr);
        if (condition.Evaluate(ctx))
        {
            // 执行 if_true 中的 action 列表
            if (args.TryGetValue("if_true", out var raw) && raw is List<ActionDefinition> actions)
            {
                foreach (var actionDef in actions)
                {
                    await _inner.ExecuteAsync(ctx, actionDef.Params);
                }
            }
        }
    }
}