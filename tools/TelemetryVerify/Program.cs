// TelemetryVerify：PML 2 26.5.0 遥测（Cloudflare 匿名使用统计）接入的运行时验收探针。
//
// 背景：遥测的核心约束是「不能影响应用、不能多收一点数据」。这类问题静态审查很难覆盖：
//   - 关闭遥测时是否真的不发请求、不生成 installation_id；
//   - 开启后是否只发一条 startup、字段是否与服务端 / 隐私政策完全一致；
//   - 网络异常时是否吞掉异常（不拖垮启动）、是否发生自动重试 / 离线排队。
//
// 因此本探针不做静态检查，而是：
//   1. 反射加载主程序已编译产物（真实执行 TelemetryService 的代码路径）；
//   2. 为每个用例准备独立的配置沙箱（产物目录 Config/Settings.json），真实走 ConfigManager 的持久化路径；
//   3. 注入本地假 HttpClient，离线捕获实际 POST 的 URL 与 body（不访问网络）；
//   4. 断言开关语义、payload 字段集、持久化、异常吞噬与「不重试 / 不排队」。
//
// 用法：dotnet run --project tools/TelemetryVerify
// 退出码：0 = 通过；1 = 存在失败项。
//
// 安全：本探针不访问网络，也不修改主程序产物以外的任何文件；
//       对 Config/Settings.json 的改动会在结束时还原（含 ProcessExit 兜底）。

using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

const string ServiceTypeName = "MEFrpLauncherX.Services.TelemetryService";
const string PayloadTypeName = "MEFrpLauncherX.Services.TelemetryEventPayload";
const string CoreAppTypeName = "MEFrpLauncherX.Core.App";
const string ConfigManagerTypeName = "MEFrpLauncherX.Core.ConfigManager";

const string ExpectedEndpoint = "https://telemetry.rycb.tech/v1/event";
var expectedFields = new[] { "event", "installation_id", "version", "platform", "architecture" };

var exitCode = 1;
try
{
    exitCode = await Run();
}
finally
{
    Sandbox.Restore();
}

return exitCode;

async Task<int> Run()
{
    var failures = new List<string>();

    // ---- 0. 加载主程序产物 ----
    var appBinDir = FindAppBinDir();
    if (appBinDir is null)
    {
        Console.WriteLine("==> 失败：找不到主程序产物 MEFrpLauncherX.dll，请先构建主项目");
        return 1;
    }

    AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
    {
        var name = new AssemblyName(args.Name).Name;
        if (name is null)
        {
            return null;
        }

        foreach (var loaded in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (string.Equals(loaded.GetName().Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return loaded;
            }
        }

        var candidate = Path.Combine(appBinDir, name + ".dll");
        if (!File.Exists(candidate))
        {
            return null;
        }

        try
        {
            return Assembly.LoadFrom(candidate);
        }
        catch
        {
            return null;
        }
    };

    var appAssembly = Assembly.LoadFrom(Path.Combine(appBinDir, "MEFrpLauncherX.dll"));
    var coreAssembly = Assembly.LoadFrom(Path.Combine(appBinDir, "MEFrpLauncherX.Core.dll"));
    Console.WriteLine($"==> 已加载主程序集: {appAssembly.GetName().Name} {appAssembly.GetName().Version}");

    var serviceType = appAssembly.GetType(ServiceTypeName, throwOnError: false);
    var payloadType = appAssembly.GetType(PayloadTypeName, throwOnError: false);
    var coreAppType = coreAssembly.GetType(CoreAppTypeName, throwOnError: false);
    var configManagerType = coreAssembly.GetType(ConfigManagerTypeName, throwOnError: false);

    if (serviceType is null || payloadType is null || coreAppType is null || configManagerType is null)
    {
        Console.WriteLine("==> 失败：找不到遥测相关类型（TelemetryService / TelemetryEventPayload / Core.App / ConfigManager）");
        return 1;
    }

    var trackStartup = serviceType.GetMethod("TrackStartupAsync", BindingFlags.Public | BindingFlags.Static);
    if (trackStartup is null || !typeof(Task).IsAssignableFrom(trackStartup.ReturnType))
    {
        Console.WriteLine("==> 失败：TelemetryService.TrackStartupAsync 不存在或返回值不是 Task");
        return 1;
    }

    Console.WriteLine($"==> 已定位遥测入口: {ServiceTypeName}.TrackStartupAsync()");
    Console.WriteLine();

    // ---- 1. Endpoint 校验 ----
    var actualEndpoint = serviceType.GetField("Endpoint", BindingFlags.NonPublic | BindingFlags.Static)
        ?.GetRawConstantValue() as string;
    if (string.Equals(actualEndpoint, ExpectedEndpoint, StringComparison.Ordinal))
    {
        Console.WriteLine($"    OK  Endpoint = {actualEndpoint}");
    }
    else
    {
        failures.Add($"Endpoint 期望 {ExpectedEndpoint}，实际 {actualEndpoint ?? "(未找到)"}");
    }

    // ---- 2. payload 字段集校验（与服务端 v1 接口 / 隐私政策一致，且不得追加字段） ----
    var payloadFields = payloadType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Select(p => p.GetCustomAttribute<System.Text.Json.Serialization.JsonPropertyNameAttribute>()?.Name ?? p.Name)
        .OrderBy(n => n, StringComparer.Ordinal)
        .ToArray();
    if (payloadFields.SequenceEqual(expectedFields.OrderBy(n => n, StringComparer.Ordinal)))
    {
        Console.WriteLine($"    OK  payload 字段集 = {string.Join(", ", payloadFields)}（与服务端定义一致，无额外字段）");
    }
    else
    {
        failures.Add($"payload 字段集期望 [{string.Join(", ", expectedFields)}]，实际 [{string.Join(", ", payloadFields)}]");
    }

    // ---- 3. 用例执行 ----
    Sandbox.Initialize(coreAppType, configManagerType);

    // 用例 1：开启遥测 → 恰好 1 次 POST，payload 合法，installation_id 为随机 UUID 且落盘持久化。
    Sandbox.Configure(configManagerType, privacyAgreed: true, telemetryEnabled: true, installationId: string.Empty);
    var run1 = await RunCase(serviceType, trackStartup, "用例 1：开启遥测后的首次启动");
    Check(failures, "开启遥测时恰好产生一次 startup 请求", run1.Handler.CallCount == 1, run1.Handler.CallCount.ToString());
    Check(failures, "请求方法为 POST", run1.Handler.LastMethod == "POST", run1.Handler.LastMethod ?? "(无)");
    Check(failures, "请求 URL 正确", run1.Handler.LastUrl == ExpectedEndpoint, run1.Handler.LastUrl ?? "(无)");
    Check(failures, "请求 Content-Type 为 application/json",
        run1.Handler.LastContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) == true,
        run1.Handler.LastContentType ?? "(无)");
    CheckJson(failures, run1.Handler.LastBody, expectedFields, GetCoreAppVersion(coreAppType));

    var persistedId = Sandbox.ReadInstallationId();
    Check(failures, "installation_id 为随机 UUID 且已持久化",
        Guid.TryParse(persistedId, out var parsedId) && parsedId != Guid.Empty && parsedId.ToString("D") == persistedId,
        persistedId ?? "(未写入)");

    var firstId = ReadBodyField(run1.Handler.LastBody, "installation_id");

    // 用例 1b：同一安装再次启动 → 复用同一个 id，且仍只发一次。
    Sandbox.Configure(configManagerType, privacyAgreed: true, telemetryEnabled: true, installationId: persistedId!);
    var run1Rerun = await RunCase(serviceType, trackStartup, "用例 1b：同一安装再次启动");
    var secondId = ReadBodyField(run1Rerun.Handler.LastBody, "installation_id");
    Check(failures, "同一安装复用同一个 installation_id",
        !string.IsNullOrEmpty(firstId) && firstId == secondId, $"首次={firstId} 再次={secondId}");
    Check(failures, "重复启动仍只发送一次", run1Rerun.Handler.CallCount == 1, run1Rerun.Handler.CallCount.ToString());

    // 用例 2：关闭遥测 → 0 次请求，且不生成 installation_id。
    Sandbox.Configure(configManagerType, privacyAgreed: true, telemetryEnabled: false, installationId: string.Empty);
    var run2 = await RunCase(serviceType, trackStartup, "用例 2：关闭遥测后的启动");
    Check(failures, "关闭遥测时产生 0 次网络请求", run2.Handler.CallCount == 0, run2.Handler.CallCount.ToString());
    var idWhenDisabled = Sandbox.ReadInstallationId();
    Check(failures, "关闭遥测时不生成 installation_id", string.IsNullOrEmpty(idWhenDisabled), idWhenDisabled ?? "(空)");

    // 用例 3：服务器返回 503 → 不抛异常、不重试。
    Sandbox.Configure(configManagerType, privacyAgreed: true, telemetryEnabled: true, installationId: string.Empty);
    var run3 = await RunCase(serviceType, trackStartup, "用例 3：服务器返回 503", respondWithServerError: true);
    Check(failures, "非 2xx 时未抛出异常（遥测不影响应用）", run3.Exception is null, run3.Exception?.Message ?? "无异常");
    Check(failures, "非 2xx 时不自动重试", run3.Handler.CallCount == 1, run3.Handler.CallCount.ToString());

    // 用例 4：网络异常 → 不抛异常、不重试。
    Sandbox.Configure(configManagerType, privacyAgreed: true, telemetryEnabled: true, installationId: string.Empty);
    var run4 = await RunCase(serviceType, trackStartup, "用例 4：网络异常", throwNetworkException: true);
    Check(failures, "网络异常被吞掉、未影响调用方", run4.Exception is null, run4.Exception?.Message ?? "无异常");
    Check(failures, "网络异常时不自动重试", run4.Handler.CallCount == 1, run4.Handler.CallCount.ToString());

    Console.WriteLine();

    if (failures.Count == 0)
    {
        Console.WriteLine("==> 通过：遥测开关语义、payload 字段、持久化与异常隔离均符合 26.5.0 规格");
        return 0;
    }

    Console.WriteLine($"==> 失败：{failures.Count} 项");
    foreach (var f in failures)
    {
        Console.WriteLine($"    - {f}");
    }

    return 1;
}

// ===================== 用例执行 =====================

async Task<CaseResult> RunCase(Type serviceType, MethodInfo trackStartup, string title,
    bool respondWithServerError = false, bool throwNetworkException = false)
{
    Console.WriteLine($"-- {title} --");

    var handler = new CapturingHandler(respondWithServerError, throwNetworkException);
    var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(3) };
    serviceType.GetField("HttpClientOverride", BindingFlags.NonPublic | BindingFlags.Static)!
        .SetValue(null, client);

    Exception? captured = null;
    try
    {
        var task = (Task)trackStartup.Invoke(null, null)!;
        await task;
    }
    catch (Exception ex)
    {
        captured = ex;
    }
    finally
    {
        serviceType.GetField("HttpClientOverride", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, null);
        client.Dispose();
    }

    if (handler.LastBody is not null)
    {
        Console.WriteLine($"    实际 payload: {Compact(handler.LastBody)}");
    }

    return new CaseResult(handler, captured);
}

static void Check(List<string> failures, string what, bool ok, string actual)
{
    if (ok)
    {
        Console.WriteLine($"    OK  {what}");
    }
    else
    {
        failures.Add($"{what}（实际：{actual}）");
        Console.WriteLine($"    !!  {what}（实际：{actual}）");
    }
}

static void CheckJson(List<string> failures, string? body, string[] expectedFields, string? appVersion)
{
    if (string.IsNullOrWhiteSpace(body))
    {
        failures.Add("未捕获到请求 body");
        return;
    }

    JsonNode? node;
    try
    {
        node = JsonNode.Parse(body);
    }
    catch (Exception ex)
    {
        failures.Add($"请求 body 不是合法 JSON：{ex.Message}");
        return;
    }

    if (node is not JsonObject obj)
    {
        failures.Add("请求 body 不是 JSON 对象");
        return;
    }

    var keys = obj.Select(kv => kv.Key).OrderBy(k => k, StringComparer.Ordinal).ToArray();
    Check(failures, "payload 仅包含服务端定义的 5 个字段",
        keys.SequenceEqual(expectedFields.OrderBy(n => n, StringComparer.Ordinal)),
        string.Join(", ", keys));

    var eventName = obj["event"]?.GetValue<string>();
    Check(failures, "event 为 startup", eventName == "startup", eventName ?? "(空)");

    var version = obj["version"]?.GetValue<string>();
    Check(failures, "version 使用 Core.App.Version", version == appVersion, $"{version ?? "(空)"} / 期望 {appVersion}");

    var platform = obj["platform"]?.GetValue<string>();
    Check(failures, "platform 取值合法", platform is "windows" or "linux" or "macos", platform ?? "(空)");

    var architecture = obj["architecture"]?.GetValue<string>();
    Check(failures, "architecture 取值合法", architecture is "x64" or "arm64" or "x86" or "arm",
        architecture ?? "(空)");

    var installationId = obj["installation_id"]?.GetValue<string>();
    Check(failures, "installation_id 为合法 UUID",
        Guid.TryParse(installationId, out var gid) && gid != Guid.Empty, installationId ?? "(空)");

    // payload 中不得出现任何个人信息类键名（防御性检查，防止未来追加字段越界）。
    var forbidden = new[] { "user", "email", "ip", "mac", "sid", "token", "cookie", "path", "host" };
    var hit = keys.Where(k => forbidden.Any(f => k.Contains(f, StringComparison.OrdinalIgnoreCase))).ToArray();
    Check(failures, "payload 不含个人信息类字段（与隐私政策一致）", hit.Length == 0, string.Join(", ", hit));
}

static string? GetCoreAppVersion(Type coreAppType) =>
    coreAppType.GetField("Version", BindingFlags.Public | BindingFlags.Static)?.GetRawConstantValue() as string;

static string? ReadBodyField(string? body, string field)
{
    if (string.IsNullOrWhiteSpace(body))
    {
        return null;
    }

    try
    {
        return JsonNode.Parse(body)?[field]?.GetValue<string>();
    }
    catch
    {
        return null;
    }
}

static string Compact(string json)
{
    try
    {
        return JsonNode.Parse(json)!.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }
    catch
    {
        return json;
    }
}

static string? FindAppBinDir()
{
    // 允许显式指定，便于在 bin 被占用（例如启动器正在运行）时改测 obj 产物
    var overridden = Environment.GetEnvironmentVariable("PML2_VERIFY_APPDIR");
    if (!string.IsNullOrWhiteSpace(overridden) && File.Exists(Path.Combine(overridden, "MEFrpLauncherX.dll")))
    {
        Console.WriteLine($"==> 使用 PML2_VERIFY_APPDIR 指定的产物目录: {overridden}");
        return overridden;
    }

    // 同时检查 bin 与 obj，取 MEFrpLauncherX.dll 较新的一份。
    // 原因：dotnet build -t:Compile 只更新 obj 而不复制到 bin；
    // 且 bin 可能因应用正在运行而被锁定，此时 obj 是唯一可测的产物。
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    for (var i = 0; i < 8 && dir is not null; i++)
    {
        var projectDir = Path.Combine(dir.FullName, "MEFrpLauncherX");
        if (Directory.Exists(projectDir))
        {
            string? best = null;
            var bestTime = DateTime.MinValue;
            foreach (var sub in new[]
                     {
                         Path.Combine(projectDir, "bin", "Debug", "net10.0"),
                         Path.Combine(projectDir, "obj", "Debug", "net10.0")
                     })
            {
                var dll = Path.Combine(sub, "MEFrpLauncherX.dll");
                if (!File.Exists(dll))
                {
                    continue;
                }

                var time = File.GetLastWriteTimeUtc(dll);
                if (time > bestTime)
                {
                    bestTime = time;
                    best = sub;
                }
            }

            if (best is not null)
            {
                Console.WriteLine($"==> 选中产物目录: {best}（{bestTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}）");
                return best;
            }
        }

        dir = dir.Parent;
    }

    return null;
}

/// <summary>用例结果：假客户端捕获的请求 + TrackStartupAsync 是否向调用方抛出了异常。</summary>
internal sealed record CaseResult(CapturingHandler Handler, Exception? Exception);

/// <summary>
///     配置沙箱。
///     <para>
///         <c>ConfigManager.ConfigDirectory</c> 基于 <see cref="AppDomain.CurrentDomain.BaseDirectory" />，
///         即本探针自己的产物目录；因此沙箱直接改写 <c>ConfigManager.ConfigPath</c> 指向的
///         <c>Config/Settings.json</c> 并重新初始化，让遥测走真实的「读配置 → 写配置」路径。
///     </para>
///     <para>结束时（含进程退出）还原原始文件，避免留下测试数据。</para>
/// </summary>
internal static class Sandbox
{
    private static string? _configPath;
    private static string? _originalContent;
    private static bool _captured;

    public static void Initialize(Type coreAppType, Type configManagerType)
    {
        // externalUse: true —— 跳过主题 / 通知服务 / RYCBApiConverter（含网络）初始化，保持离线。
        var initialize = coreAppType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static)!;
        ((Task)initialize.Invoke(null, [true])!).GetAwaiter().GetResult();

        _configPath = (string)configManagerType
            .GetProperty("ConfigPath", BindingFlags.Public | BindingFlags.Static)!
            .GetValue(null)!;

        AppDomain.CurrentDomain.ProcessExit += (_, _) => Restore();
    }

    public static void Configure(Type configManagerType, bool privacyAgreed, bool telemetryEnabled,
        string installationId)
    {
        var directory = Path.GetDirectoryName(_configPath!)!;
        Directory.CreateDirectory(directory);

        if (!_captured)
        {
            _originalContent = File.Exists(_configPath!) ? File.ReadAllText(_configPath!) : null;
            _captured = true;
        }

        // 以现有配置为基线，仅改写与遥测相关的开关（尽量保持真实配置结构）。
        JsonObject config;
        try
        {
            config = File.Exists(_configPath!)
                ? JsonNode.Parse(File.ReadAllText(_configPath!)) as JsonObject ?? new JsonObject()
                : new JsonObject();
        }
        catch
        {
            config = new JsonObject();
        }

        config["PrivacyAgreed"] = privacyAgreed;
        config["IsTelemetryEnabled"] = telemetryEnabled;
        config["TelemetryInstallationId"] = installationId;
        File.WriteAllText(_configPath!, config.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        // 重新加载配置（真实走 ConfigManager 的读取 / schema 归一化 / 落盘路径）。
        configManagerType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null);
    }

    public static string? ReadInstallationId()
    {
        try
        {
            if (_configPath is null || !File.Exists(_configPath))
            {
                return null;
            }

            return JsonNode.Parse(File.ReadAllText(_configPath))?["TelemetryInstallationId"]?.GetValue<string>();
        }
        catch
        {
            return null;
        }
    }

    public static void Restore()
    {
        try
        {
            if (_configPath is null || !_captured)
            {
                return;
            }

            if (_originalContent is null)
            {
                if (File.Exists(_configPath))
                {
                    File.Delete(_configPath);
                }
            }
            else
            {
                File.WriteAllText(_configPath, _originalContent);
            }

            _captured = false;
        }
        catch
        {
            // 还原失败不影响验收结论，仅避免留下测试数据。
        }
    }
}

/// <summary>本地假 HttpClient：捕获请求，不访问网络。</summary>
internal sealed class CapturingHandler : HttpMessageHandler
{
    private readonly bool _throwNetworkException;
    private readonly bool _respondWithServerError;

    public CapturingHandler(bool respondWithServerError, bool throwNetworkException)
    {
        _respondWithServerError = respondWithServerError;
        _throwNetworkException = throwNetworkException;
    }

    public int CallCount { get; private set; }

    public string? LastUrl { get; private set; }

    public string? LastMethod { get; private set; }

    public string? LastContentType { get; private set; }

    public string? LastBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        CallCount++;
        LastUrl = request.RequestUri?.ToString();
        LastMethod = request.Method.Method;
        LastContentType = request.Content?.Headers.ContentType?.ToString();
        if (request.Content is not null)
        {
            LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        if (_throwNetworkException)
        {
            throw new HttpRequestException("模拟网络异常：无法连接到遥测服务");
        }

        return new HttpResponseMessage(
            _respondWithServerError ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK);
    }
}
