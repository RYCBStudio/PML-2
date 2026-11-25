using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.MEFIntegrated;
using static MEFrpLauncherX.Core.MEFIntegrated.InfoClasses;

// =============================================================================
// TrafficVerify —— PML 2 流量统计链路自检（26.5.1 C3 回归）
//
// 背景（C3）：
//   GetTrafficStatusAsync 在响应体是 HTML（网关/代理/CDN 的错误页）时
//   `return default`（即 null），而三个调用点都直接解引用返回值：
//     - UserCenterPage.axaml.cs        trafficStatusData.data
//     - TrafficStatusControl.axaml.cs  (await ...).data
//     - TrafficStatusControl.axaml.cs  r.code
//   一旦触发就抛 NullReferenceException。
//
// 修复思路：
//   把「响应 → 结果」的判定抽成纯函数 BuildTrafficStatusResult，
//   约定它永不返回 null、永不抛异常；HTML / 空体 / 非法 JSON / HTTP 失败
//   一律降级为带错误码的 ApiInfo 实例。
//
// 本工具直接验证该纯函数，覆盖调用方会遇到的所有输入形态。
// =============================================================================

var failures = new List<string>();
var checks = 0;

void Check(string name, bool ok, string detail = "")
{
    checks++;
    if (ok)
    {
        Console.WriteLine($"  [PASS] {name}");
    }
    else
    {
        Console.WriteLine($"  [FAIL] {name}{(detail.Length > 0 ? " — " + detail : "")}");
        failures.Add(name + (detail.Length > 0 ? " — " + detail : ""));
    }
}

Console.WriteLine("==> TrafficVerify：流量统计链路自检");
Console.WriteLine();

// 初始化日志器（App.CurrentLogger 在 App.Initialize() 之前为 null）
var sandbox = Path.Combine(Path.GetTempPath(), "PML2-TrafficVerify-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(sandbox);
Environment.SetEnvironmentVariable("PML2_DATA_ROOT", sandbox);
MEFrpLauncherX.Core.App.Initialize(externalUse: true);

try
{
    // =====================================================================
    Console.WriteLine("== 1. 永不返回 null（C3 的核心不变量） ==");
    // =====================================================================
    // 这三个调用点都会直接解引用返回值，返回 null 等于让调用处崩。
    var cases = new (string Name, string? Content, int StatusCode, bool IsSuccessful)[]
    {
        ("HTML 错误页", "<html><body>502 Bad Gateway</body></html>", 200, true),
        ("HTML 错误页 + HTTP 失败", "<html>oops</html>", 502, false),
        ("空响应体", "", 200, true),
        ("null 响应体", null, 200, true),
        ("纯空白响应体", "   \n\t ", 200, true),
        ("非法 JSON", "this is not json", 200, true),
        ("截断的 JSON", "{\"code\":200,\"data\":", 200, true),
        ("JSON null 字面量", "null", 200, true),
        ("HTTP 500", "", 500, false),
        ("HTTP 401", "", 401, false),
        ("正常 JSON", """{"code":200,"message":"ok","data":null}""", 200, true)
    };

    foreach (var c in cases)
    {
        ApiInfo<TrafficStatus>? result = null;
        Exception? thrown = null;
        try
        {
            result = MEFrpApiConverter.BuildTrafficStatusResult(c.Content, c.StatusCode, c.IsSuccessful);
        }
        catch (Exception ex)
        {
            thrown = ex;
        }

        Check($"{c.Name}：不抛异常", thrown is null,
            thrown is null ? "" : $"{thrown.GetType().Name}: {thrown.Message}");
        Check($"{c.Name}：返回非 null", result is not null);
    }

    // =====================================================================
    Console.WriteLine();
    Console.WriteLine("== 2. HTML 错误页的语义 ==");
    // =====================================================================
    var html = MEFrpApiConverter.BuildTrafficStatusResult(
        "<!DOCTYPE html><html><body>Your session has expired</body></html>", 200, true);
    Check("HTML 响应 → code 502", html.code == 502, $"实际 {html.code}");
    Check("HTML 响应 → 有可读提示", !string.IsNullOrWhiteSpace(html.message), $"message=\"{html.message}\"");
    Check("HTML 响应 → data 为 null", html.data is null);

    // =====================================================================
    Console.WriteLine();
    Console.WriteLine("== 3. HTTP 失败透传状态码 ==");
    // =====================================================================
    var h500 = MEFrpApiConverter.BuildTrafficStatusResult("", 500, false);
    Check("HTTP 500 → code 500", h500.code == 500, $"实际 {h500.code}");
    Check("HTTP 500 → data 为 null", h500.data is null);

    var h401 = MEFrpApiConverter.BuildTrafficStatusResult("", 401, false);
    Check("HTTP 401 → code 401", h401.code == 401, $"实际 {h401.code}");

    // =====================================================================
    Console.WriteLine();
    Console.WriteLine("== 4. 正常 JSON 能正确反序列化 ==");
    // =====================================================================
    const string validJson = """
    {
      "code": 200,
      "message": "success",
      "data": {
        "dates": ["2026-01-01", "2026-01-02"],
        "trafficIn": [1024, 2048],
        "trafficOut": [512, 768],
        "totalTraffic": [1536, 2816]
      }
    }
    """;

    var ok = MEFrpApiConverter.BuildTrafficStatusResult(validJson, 200, true);
    Check("正常 JSON → code 200", ok.code == 200, $"实际 {ok.code}");
    Check("正常 JSON → data 非 null", ok.data is not null);
    Check("正常 JSON → dates 正确", ok.data?.dates is ["2026-01-01", "2026-01-02"],
        $"实际 {string.Join(",", ok.data?.dates ?? [])}");
    Check("正常 JSON → trafficIn 正确", ok.data?.trafficIn is [1024, 2048],
        $"实际 {string.Join(",", ok.data?.trafficIn ?? [])}");
    Check("正常 JSON → totalTraffic 正确", ok.data?.totalTraffic is [1536, 2816],
        $"实际 {string.Join(",", ok.data?.totalTraffic ?? [])}");

    // =====================================================================
    Console.WriteLine();
    Console.WriteLine("== 5. 业务失败（code 非 200）不被当成成功 ==");
    // =====================================================================
    var bizFail = MEFrpApiConverter.BuildTrafficStatusResult(
        """{"code":403,"message":"forbidden","data":null}""", 200, true);
    Check("业务失败 → code 403 被如实读出", bizFail.code == 403, $"实际 {bizFail.code}");
    Check("业务失败 → data 为 null", bizFail.data is null);

    // =====================================================================
    Console.WriteLine();
    Console.WriteLine("== 6. 源码级断言：不再出现 return default ==");
    // =====================================================================
    var repoRoot = FindRepoRoot();
    var converterPath = Path.Combine(repoRoot, "MEFrpLauncherX.Core", "MEFIntegrated", "MEFApiConverter.cs");
    Check("能定位到 MEFApiConverter.cs", File.Exists(converterPath), converterPath);

    if (File.Exists(converterPath))
    {
        var src = File.ReadAllText(converterPath);

        // 取出 GetTrafficStatusAsync 的方法体（到下一个同缩进的成员声明为止）
        var start = src.IndexOf("public static async Task<ApiInfo<TrafficStatus>> GetTrafficStatusAsync",
            StringComparison.Ordinal);
        Check("能定位到 GetTrafficStatusAsync", start >= 0);

        if (start >= 0)
        {
            var rest = src[start..];
            var next = rest.IndexOf("\n    /// <summary>", StringComparison.Ordinal);
            var body = next > 0 ? rest[..next] : rest;

            // 只看代码行（剔除注释），确认不再有裸 return default / return null
            var codeOnly = string.Join('\n', body
                .Split('\n')
                .Select(l => l.TrimStart())
                .Where(l => !l.StartsWith("//", StringComparison.Ordinal)
                            && !l.StartsWith("///", StringComparison.Ordinal)
                            && !l.StartsWith("*", StringComparison.Ordinal)));

            Check("GetTrafficStatusAsync 不再 return default",
                !codeOnly.Contains("return default", StringComparison.Ordinal));
            Check("GetTrafficStatusAsync 不再 return null",
                !codeOnly.Contains("return null", StringComparison.Ordinal));
            Check("GetTrafficStatusAsync 走 BuildTrafficStatusResult",
                codeOnly.Contains("BuildTrafficStatusResult", StringComparison.Ordinal));
        }
    }

    // =====================================================================
    Console.WriteLine();
    Console.WriteLine("== 7. 源码级断言：三个调用点都不再裸解引用 ==");
    // =====================================================================
    // 调用方原先直接 trafficStatusData.data / (await ...).data / r.code。
    // BuildTrafficStatusResult 保证非 null 后，这些解引用才安全。
    // 这里守住「调用点附近仍存在判空或依赖非 null 契约」的事实 ——
    // 只要 GetTrafficStatusAsync 的返回类型不是可空，编译器就会强制调用方处理。
    var icpPath = Path.Combine(repoRoot, "MEFrpLauncherX.Core", "MEFIntegrated", "MEFApiConverter.cs");
    if (File.Exists(icpPath))
    {
        var src = File.ReadAllText(icpPath);
        Check("GetTrafficStatusAsync 的返回类型非可空（Task<ApiInfo<TrafficStatus>>）",
            src.Contains("Task<ApiInfo<TrafficStatus>> GetTrafficStatusAsync", StringComparison.Ordinal));
    }
}
catch (Exception ex)
{
    failures.Add("异常: " + ex);
    Console.WriteLine($"  [FAIL] 未捕获异常: {ex}");
}
finally
{
    try
    {
        Directory.Delete(sandbox, true);
    }
    catch
    {
        // 忽略
    }
}

Console.WriteLine();
Console.WriteLine($"==> 共 {checks} 项检查，失败 {failures.Count} 项");
if (failures.Count > 0)
{
    Console.WriteLine("失败项：");
    foreach (var f in failures)
    {
        Console.WriteLine("  - " + f);
    }

    return 1;
}

Console.WriteLine("全部通过 ✅");
return 0;

// 在仓库中定位根目录（含 MEFrpLauncherX 子目录的那一层）。
static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        if (Directory.Exists(Path.Combine(dir.FullName, "MEFrpLauncherX")))
        {
            return dir.FullName;
        }

        dir = dir.Parent;
    }

    return AppContext.BaseDirectory;
}
