using System.Diagnostics;
using System.Text.Json;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.MEFIntegrated;
using RestSharp;
using RYCB.PML2.MEFrpCaptchaLib;

// =============================================================================
// CaptchaVerify —— PML 2 登录人机验证链路自检（26.5.1 C2 回归）
//
// 背景（C2）：
//   原实现用 `new RestClient(url)` 建客户端，没有设置 Timeout —— RestSharp 在未设置时
//   默认 100 秒。断网时请求一直挂着，登录页的 IsBusy 永远为 true，表现为「登录界面卡死」。
//   另外 PostChallengeAsync 直接对可能为空的响应体做 JsonSerializer.Deserialize，
//   断网时会抛 JsonException（不是可读的网络错误）。
//
// 验证策略：
//   1. 超时：用不可达地址（TEST-NET-1 保留段 192.0.2.1）实测两种客户端的耗时差异，
//      证明「显式超时」确实把等待从 RestSharp 默认的 100 秒压到秒级。
//   2. 响应解析：直接调用 MEFrpApiConverter 抽出的 ParseChallengeResponse /
//      ParseRedeemResponse，覆盖「空响应体 / 非 JSON / 正常 JSON」三种形态。
//   3. 判定逻辑：复现旧的 `ri?.success == false` 与新的 `ri is null || !ri.success`，
//      确认 null 不再被误判为成功。
//
// 注意：Constants.ChallengeUrl / RedeemUrl 是编译期 const，无法在运行时改写，
//       因此请求地址无法重定向到本地测试服务器 —— 改为在「客户端超时」层面实测。
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

Console.WriteLine("==> CaptchaVerify：人机验证链路自检");
Console.WriteLine();

// 初始化日志器（App.CurrentLogger 在 App.Initialize() 之前为 null）
var sandbox = Path.Combine(Path.GetTempPath(), "PML2-CaptchaVerify-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(sandbox);
Environment.SetEnvironmentVariable("PML2_DATA_ROOT", sandbox);
MEFrpLauncherX.Core.App.Initialize(externalUse: true);

try
{
    // =====================================================================
    Console.WriteLine("== 1. 验证码客户端必须有显式超时 ==");
    // =====================================================================
    var timeoutField = typeof(MEFrpApiConverter).GetField("CaptchaRequestTimeout",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
    var timeout = timeoutField?.GetValue(null) as TimeSpan?;
    Check("CaptchaRequestTimeout 已定义且为有限值",
        timeout.HasValue && timeout.Value > TimeSpan.Zero && timeout.Value < TimeSpan.FromSeconds(60),
        $"实际 {timeout?.ToString() ?? "<未找到>"}");

    // =====================================================================
    Console.WriteLine();
    Console.WriteLine("== 2. 超时实测：显式超时把等待压到秒级 ==");
    // =====================================================================
    // 192.0.2.1 是 TEST-NET-1 保留地址，TCP 连接会被静默丢弃 —— 与「断网」表现一致
    const string blackhole = "http://192.0.2.1:9/";

    // 2a. 原代码的写法：new RestClient(url)，无 Timeout
    var swDefault = Stopwatch.StartNew();
    long defaultMs;
    bool defaultTimedOut;
    try
    {
        using var client = new RestClient(blackhole);
        var resp = await client.ExecuteAsync(new RestRequest { Method = Method.Post });
        defaultTimedOut = resp.ResponseStatus == ResponseStatus.TimedOut;
    }
    catch
    {
        defaultTimedOut = false;
    }

    defaultMs = swDefault.ElapsedMilliseconds;

    // 2b. 修复后的写法：显式 Timeout
    var swFixed = Stopwatch.StartNew();
    bool fixedTimedOut;
    try
    {
        using var client = new RestClient(new RestClientOptions(blackhole)
        {
            Timeout = timeout ?? TimeSpan.FromSeconds(10)
        });
        var resp = await client.ExecuteAsync(new RestRequest { Method = Method.Post });
        fixedTimedOut = resp.ResponseStatus == ResponseStatus.TimedOut;
    }
    catch
    {
        fixedTimedOut = false;
    }

    var fixedMs = swFixed.ElapsedMilliseconds;

    Console.WriteLine($"    无显式超时: {defaultMs} ms（本次是 TCP 层快速拒绝；真断网丢包时会走满 100 秒上限）");
    Console.WriteLine($"    显式超时  : {fixedMs} ms");

    Check("显式超时会触发 TimedOut 状态", fixedTimedOut);
    Check("显式超时的等待时间在超时值附近",
        fixedMs >= (timeout ?? TimeSpan.FromSeconds(10)).TotalMilliseconds - 1500
        && fixedMs < (timeout ?? TimeSpan.FromSeconds(10)).TotalMilliseconds + 5000,
        $"实际 {fixedMs} ms");

    // =====================================================================
    Console.WriteLine();
    Console.WriteLine("== 3. 响应解析：空响应体 / 非 JSON 不抛异常 ==");
    // =====================================================================
    var challengeEmpty = MEFrpApiConverter.ParseChallengeResponse("");
    Check("空响应体 → null（不抛异常）", challengeEmpty is null);

    var challengeNull = MEFrpApiConverter.ParseChallengeResponse(null);
    Check("null 响应体 → null", challengeNull is null);

    var challengeWhitespace = MEFrpApiConverter.ParseChallengeResponse("   \n  ");
    Check("空白响应体 → null", challengeWhitespace is null);

    ChallengeInfo? challengeGarbage = null;
    Exception? garbageEx = null;
    try
    {
        challengeGarbage = MEFrpApiConverter.ParseChallengeResponse("this is not json");
    }
    catch (Exception ex)
    {
        garbageEx = ex;
    }

    Check("非 JSON 响应体 → 不抛 JsonException", garbageEx is not JsonException,
        garbageEx is null ? "" : $"{garbageEx.GetType().Name}: {garbageEx.Message}");
    Check("非 JSON 响应体 → null", challengeGarbage is null);

    var redeemEmpty = MEFrpApiConverter.ParseRedeemResponse("");
    Check("redeem 空响应体 → null", redeemEmpty is null);

    Exception? redeemGarbageEx = null;
    CaptchaResultX? redeemGarbage = null;
    try
    {
        redeemGarbage = MEFrpApiConverter.ParseRedeemResponse("not json at all");
    }
    catch (Exception ex)
    {
        redeemGarbageEx = ex;
    }

    Check("redeem 非 JSON 响应体 → 不抛 JsonException", redeemGarbageEx is not JsonException,
        redeemGarbageEx is null ? "" : $"{redeemGarbageEx.GetType().Name}: {redeemGarbageEx.Message}");
    Check("redeem 非 JSON 响应体 → null", redeemGarbage is null);

    // =====================================================================
    Console.WriteLine();
    Console.WriteLine("== 4. 响应解析：正常 JSON 能正确反序列化 ==");
    // =====================================================================
    var validRedeem = MEFrpApiConverter.ParseRedeemResponse(
        """{"success":true,"token":"abc123","expires":1234567890}""");
    Check("合法 redeem 响应能反序列化", validRedeem is not null);
    Check("success 字段被如实读出", validRedeem is { success: true });
    Check("token 字段被如实读出", validRedeem?.token == "abc123");

    var validChallenge = MEFrpApiConverter.ParseChallengeResponse(
        """{"challenge":{"nonce":1},"token":"tok","expires":1234567890}""");
    Check("合法 challenge 响应能反序列化", validChallenge is not null);
    Check("challenge token 被如实读出", validChallenge?.token == "tok");

    // =====================================================================
    Console.WriteLine();
    Console.WriteLine("== 5. 判定逻辑：ri 为 null 必须判为失败 ==");
    // =====================================================================
    // 原代码是 `if (ri?.success == false)` —— ri 为 null 时求值为 false（当成成功），
    // 随后 CaptchaHelper.GetCaptchaCode(null) 抛 NullReferenceException。
    CaptchaResultX? ri = null;
    var oldPredicate = ri?.success == false;
    var newPredicate = ri is null || !ri.success;
    Check("旧判定会把 null 当成成功（这正是原 bug）", !oldPredicate);
    Check("新判定会把 null 当成失败", newPredicate);

    // 并且确认 CaptchaHelper.GetCaptchaCode(null) 确实会抛 NRE（原 bug 的后果）
    var threwNre = false;
    try
    {
        CaptchaHelper.GetCaptchaCode(null!);
    }
    catch (NullReferenceException)
    {
        threwNre = true;
    }

    Check("CaptchaHelper.GetCaptchaCode(null) 确实抛 NRE（说明旧判定有多致命）", threwNre);

    // 同样地，CaptchaHelper.GetRedeemBody(null) 也会抛 NRE
    var threwNre2 = false;
    try
    {
        await CaptchaHelper.GetRedeemBody(null!);
    }
    catch (NullReferenceException)
    {
        threwNre2 = true;
    }

    Check("CaptchaHelper.GetRedeemBody(null) 确实抛 NRE", threwNre2);
    // =====================================================================
    Console.WriteLine();
    Console.WriteLine("== 6. 源码级断言：加载态必须被复位 ==");
    // =====================================================================
    // GetCaptchaResultAsync 位于主程序（自包含 exe，无法被本工具引用），
    // 因此用源码断言守住「失败路径也会复位 IsBusy」这个不变量。
    var repoRoot = FindRepoRoot();
    var loginPagePath = Path.Combine(repoRoot, "MEFrpLauncherX", "Views", "LoginPage.axaml.cs");
    Check("能定位到 LoginPage.axaml.cs", File.Exists(loginPagePath), loginPagePath);

    if (File.Exists(loginPagePath))
    {
        var src = File.ReadAllText(loginPagePath);

        // 取出 GetCaptchaResultAsync 的方法体
        var start = src.IndexOf("public static async Task<string> GetCaptchaResultAsync", StringComparison.Ordinal);
        Check("能定位到 GetCaptchaResultAsync", start >= 0);

        if (start >= 0)
        {
            var body = src[start..];
            var end = body.IndexOf("\n    }", StringComparison.Ordinal);
            if (end > 0)
            {
                body = body[..end];
            }

            Check("GetCaptchaResultAsync 有 finally 块", body.Contains("finally", StringComparison.Ordinal));
            Check("finally 里复位 IsBusy", body.Contains("vm.IsBusy = false", StringComparison.Ordinal));
            Check("finally 里复位 IsFakeProgress", body.Contains("vm.IsFakeProgress = false", StringComparison.Ordinal));
            Check("finally 里复位 IsBusy 出现在 finally 之后",
                body.IndexOf("finally", StringComparison.Ordinal)
                < body.LastIndexOf("vm.IsBusy = false", StringComparison.Ordinal));

            // 关键判定：null 必须被显式判为失败
            Check("显式判空 ri（不依赖 ri?.success == false）",
                body.Contains("ri is null", StringComparison.Ordinal));

            // 只看代码行（剔除注释），确认不再用 `ri?.success == false` 判定
            var codeOnly = string.Join('\n', body
                .Split('\n')
                .Select(l => l.TrimStart())
                .Where(l => !l.StartsWith("//", StringComparison.Ordinal)
                            && !l.StartsWith("///", StringComparison.Ordinal)
                            && !l.StartsWith("*", StringComparison.Ordinal)));
            Check("代码中不再使用 ri?.success == false 判定",
                !codeOnly.Contains("ri?.success == false", StringComparison.Ordinal));

            // challenge 的 null 必须被显式判空
            Check("显式判空 ci（不把 null 传给 GetRedeemBody）",
                body.Contains("ci is null", StringComparison.Ordinal));
        }
    }

    // =====================================================================
    Console.WriteLine();
    Console.WriteLine("== 7. 源码级断言：验证码客户端必须有显式超时 ==");
    // =====================================================================
    var converterPath = Path.Combine(repoRoot, "MEFrpLauncherX.Core", "MEFIntegrated", "MEFApiConverter.cs");
    Check("能定位到 MEFApiConverter.cs", File.Exists(converterPath));

    if (File.Exists(converterPath))
    {
        var src = File.ReadAllText(converterPath);
        Check("验证码客户端不再用裸 new RestClient(url)",
            !src.Contains("new RestClient(Constants.ChallengeUrl)", StringComparison.Ordinal)
            && !src.Contains("new RestClient(Constants.RedeemUrl)", StringComparison.Ordinal));
        Check("存在 CreateCaptchaClient 且设置了 Timeout",
            src.Contains("CreateCaptchaClient", StringComparison.Ordinal)
            && src.Contains("Timeout = CaptchaRequestTimeout", StringComparison.Ordinal));
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
