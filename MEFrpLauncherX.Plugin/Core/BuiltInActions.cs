using System.Diagnostics;
using System.Text;
using System.Text.Json;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Plugin.Condition;
using MEFrpLauncherX.Plugin.Engine;

namespace MEFrpLauncherX.Plugin.Core;

public class LogAction : IAction
{
    public Task ExecuteAsync(ExecutionContext ctx, Dictionary<string, object>? args)
    {
        var msg = args?.GetValueOrDefault("msg")?.ToString() ?? "No message";
        Console.WriteLine($"[{ctx.PluginId}] {msg}");
        App.CurrentLogger.Log($"[{ctx.PluginId}] {msg}", module: EnumLogModule.Plugin);
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

        var sw = Stopwatch.StartNew();
        try
        {
            using var client = new HttpClient();
            var request = new HttpRequestMessage(new HttpMethod(method), url);
            if (body != null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            ctx.Variables["http_response"] = content;
            sw.Stop();

            if (!response.IsSuccessStatusCode)
            {
                App.CurrentLogger.Warning(
                    $"插件 {ctx.PluginId} http_request 返回非成功状态: {(int)response.StatusCode} {response.StatusCode}, URL: {url}",
                    module: EnumLogModule.Plugin);
            }
            else
            {
                App.CurrentLogger.LogDebug(
                    $"插件 {ctx.PluginId} http_request 完成: {method} {url} -> {(int)response.StatusCode}, 耗时 {sw.ElapsedMilliseconds}ms",
                    module: EnumLogModule.Plugin);
            }
        }
        catch (Exception ex)
        {
            App.CurrentLogger.Warning(
                $"插件 {ctx.PluginId} http_request 失败: {method} {url}, {ex.Message}",
                module: EnumLogModule.Plugin);
            throw;
        }
    }
}

public class NotifyAction : IAction
{
    public async Task ExecuteAsync(ExecutionContext ctx, Dictionary<string, object>? args)
    {
        var msg = args?.GetValueOrDefault("msg")?.ToString() ?? "No message";
        var request = App.NotificationService.RequestNotification($"{ctx.PluginId} | PML 2", msg);
        if (request == null) return;
        await App.NotificationService.ShowAsync(request);
    }
}

/// <summary>打开 URL 动作（26.3.1 M5）：用系统默认浏览器打开链接</summary>
public class OpenUrlAction : IAction
{
    public Task ExecuteAsync(ExecutionContext ctx, Dictionary<string, object>? args)
    {
        var url = args?.GetValueOrDefault("url")?.ToString() ?? "";
        if (string.IsNullOrEmpty(url))
        {
            App.CurrentLogger.Warning($"插件 {ctx.PluginId} open_url 缺少 url 参数", module: EnumLogModule.Plugin);
            return Task.CompletedTask;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            App.CurrentLogger.LogDebug($"插件 {ctx.PluginId} open_url: {url}", module: EnumLogModule.Plugin);
        }
        catch (Exception ex)
        {
            App.CurrentLogger.Warning($"插件 {ctx.PluginId} open_url 失败: {url}, {ex.Message}",
                module: EnumLogModule.Plugin);
        }

        return Task.CompletedTask;
    }
}

public class LocalRunAction : IAction
{
    public async Task ExecuteAsync(ExecutionContext ctx, Dictionary<string, object>? args)
    {
        var exe = args?["exe"]?.ToString() ?? "";
        var argsList = args?.GetValueOrDefault("args")?.ToString() ?? "";
        var createNoWindow = args?.GetValueOrDefault("create_no_window")?.ToString() ?? "true";
        var useShellExecute = args?.GetValueOrDefault("use_shell_execute")?.ToString() ?? "false";

        try
        {
            await Process.Start(new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = string.Join(' ', argsList),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = useShellExecute == "true",
                CreateNoWindow = createNoWindow == "true"
            })?.WaitForExitAsync()!;
        }
        catch (Exception ex)
        {
            App.CurrentLogger.Warning($"插件 {ctx.PluginId} local_run 执行失败: {exe} {argsList}: \n{ex}",
                module: EnumLogModule.Plugin);
        }
    }
}

public class PythonAction : IAction
{
    public async Task ExecuteAsync(ExecutionContext ctx, Dictionary<string, object>? args)
    {
        var script = args?["script"]?.ToString() ?? "";
        var input = args?.GetValueOrDefault("input")?.ToString() ?? "{}";

        var sw = Stopwatch.StartNew();
        // 调用 Python 进程 (需要 python3 在 PATH 中)
        var psi = new ProcessStartInfo
        {
            FileName = "python3",
            Arguments =
                $"-c \"import sys, json, importlib.util; spec=importlib.util.spec_from_file_location('mod', '{script}'); mod=importlib.util.module_from_spec(spec); spec.loader.exec_module(mod); print(json.dumps(mod.main(json.loads(sys.stdin.read()))))\"",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = new Process { StartInfo = psi };
            process.Start();
            await process.StandardInput.WriteAsync(input);
            process.StandardInput.Close();

            var output = await process.StandardOutput.ReadToEndAsync();
            var errorOutput = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            sw.Stop();

            if (process.ExitCode != 0)
            {
                App.CurrentLogger.Warning(
                    $"插件 {ctx.PluginId} python_run 退出码 {process.ExitCode}, 耗时 {sw.ElapsedMilliseconds}ms, stderr: {errorOutput}",
                    module: EnumLogModule.Plugin);
            }
            else
            {
                App.CurrentLogger.LogDebug($"插件 {ctx.PluginId} python_run 完成, 耗时 {sw.ElapsedMilliseconds}ms",
                    module: EnumLogModule.Plugin);
            }

            // 将 Python 返回的 JSON 合并到 Variables
            if (!string.IsNullOrWhiteSpace(output))
            {
                try
                {
                    var result =
                        JsonSerializer.Deserialize<Dictionary<string, object>>(output,
                            App.AppJsonSerializerContext.DictionaryStringObject);
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
        catch (Exception ex)
        {
            App.CurrentLogger.Warning($"插件 {ctx.PluginId} python_run 执行失败: {ex.Message}",
                module: EnumLogModule.Plugin);
            throw;
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
        if (funcActions == null)
        {
            App.CurrentLogger.Warning($"插件 {ctx.PluginId} call_function 找不到函数: {funcName}",
                module: EnumLogModule.Plugin);
            return;
        }

        var newCtx = ctx.CloneWithArgs(funcArgs);
        foreach (var actionDef in funcActions)
        {
            // 将 action name 注入 params 以便 PluginEngine 分发
            var dispatchArgs = new Dictionary<string, object>(actionDef.Params)
            {
                ["__actionName"] = actionDef.Name
            };
            await _subActionExecutor.ExecuteAsync(newCtx, dispatchArgs);
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

/// <summary>
///     重启隧道动作（26.3.1 S2）：按 <c>proxyName</c> 停止并重新启动隧道。
///     实际能力由主程序通过 <see cref="ProxyActionBridge" /> 注册，插件引擎本身不持有隧道生命周期。
/// </summary>
public class ProxyRestartAction : IAction
{
    public async Task ExecuteAsync(ExecutionContext ctx, Dictionary<string, object>? args)
    {
        var proxyName = args?.GetValueOrDefault("proxyName")?.ToString() ?? "";
        if (string.IsNullOrEmpty(proxyName))
        {
            App.CurrentLogger.Warning($"插件 {ctx.PluginId} proxy.restart 缺少 proxyName 参数",
                module: EnumLogModule.Plugin);
            return;
        }

        if (ProxyActionBridge.RestartProxy == null)
        {
            App.CurrentLogger.Warning(
                $"插件 {ctx.PluginId} proxy.restart 不可用：主程序未注册隧道重启能力",
                module: EnumLogModule.Plugin);
            return;
        }

        var error = await ProxyActionBridge.RestartProxy(proxyName);
        if (error != null)
        {
            App.CurrentLogger.Warning($"插件 {ctx.PluginId} proxy.restart 失败: {proxyName}: {error}",
                module: EnumLogModule.Plugin);
        }
        else
        {
            // 权限记录：插件动作执行了隧道控制（无权限模型时的审计痕迹）
            App.CurrentLogger.Log($"插件 {ctx.PluginId} 执行了 proxy.restart: {proxyName}",
                module: EnumLogModule.Plugin);
        }
    }
}