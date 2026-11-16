using System.Collections.Concurrent;

// =============================================================================
// AsyncVoidVerify —— PML 2 async void 异常兜底自检（26.5.1 C4 回归）
//
// 背景（C4）：
//   UserCenterPage.UserControl_Loaded 与 TrafficStatusControl.UpdateTrafficData
//   都是 async void。async void 的异常不经由 Task 传递，而是直接抛回
//   SynchronizationContext：
//     · Release 构建：App.axaml.cs 注册了 Dispatcher.UIThread.UnhandledException
//       兜底（但那段代码在 #if !DEBUG 里）。
//     · Debug 构建：完全没有兜底，异常直接崩进程。
//   此外 UpdateTrafficData 原先有 5 处手工 IsLoading = false 分散在各 return
//   分支 —— 任何抛异常的路径都会让进度环永久卡住。
//
// 验证策略：
//   A. 用一个自定义 SynchronizationContext 实证「未捕获的 async void 异常
//      会逃逸到同步上下文」与「try/catch 能拦住」—— 说明修复为什么必要且有效。
//   B. 源码级断言：两处 async void 都有 catch；IsLoading 只在 finally 复位。
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

Console.WriteLine("==> AsyncVoidVerify：async void 异常兜底自检");
Console.WriteLine();

// =============================================================================
// A. 实证 async void 的异常语义
// =============================================================================
Console.WriteLine("== 1. 实证：未捕获的 async void 异常会逃逸到同步上下文 ==");

var ctx = new CapturingSynchronizationContext();
var prevCtx = SynchronizationContext.Current;
SynchronizationContext.SetSynchronizationContext(ctx);

try
{
    // 故意在 await 之后抛异常 —— 这是 async void 最典型的故障形态
    FireAndForgetUnsafe();
    await Task.Delay(300);
}
finally
{
    SynchronizationContext.SetSynchronizationContext(prevCtx);
}

Check("未捕获异常被抛到同步上下文（这正是 Debug 构建崩溃的原因）",
    ctx.Captured.Count > 0,
    $"捕获到 {ctx.Captured.Count} 个异常");

if (ctx.Captured.Count > 0)
{
    var ex = ctx.Captured.First();
    Check("逃逸的异常类型正确", ex is InvalidOperationException,
        $"实际 {ex.GetType().Name}");
    Console.WriteLine($"    逃逸异常：{ex.GetType().Name}: {ex.Message}");
}

// -----------------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("== 2. 实证：try/catch 能拦住（修复形态） ==");

var ctx2 = new CapturingSynchronizationContext();
prevCtx = SynchronizationContext.Current;
SynchronizationContext.SetSynchronizationContext(ctx2);

var safeHandlerWasHit = false;

try
{
    FireAndForgetSafe();
    await Task.Delay(300);
}
finally
{
    SynchronizationContext.SetSynchronizationContext(prevCtx);
}

Check("加了 catch 后异常不再逃逸", ctx2.Captured.Count == 0,
    $"仍逃逸 {ctx2.Captured.Count} 个");
Check("catch 内确实被走到", safeHandlerWasHit);

// 局部函数：修复形态（自行兜底）。闭包捕获 safeHandlerWasHit 以便断言。
async void FireAndForgetSafe()
{
    try
    {
        await Task.Yield();
        throw new InvalidOperationException("模拟 async void 中的异常（会被 catch 拦住）");
    }
    catch (InvalidOperationException)
    {
        safeHandlerWasHit = true;
    }
}


// =============================================================================
// B. 源码级断言
// =============================================================================
Console.WriteLine();
Console.WriteLine("== 3. 源码断言：UserCenterPage.UserControl_Loaded 有 catch ==");

var repoRoot = FindRepoRoot();
var userCenterPath = Path.Combine(repoRoot, "MEFrpLauncherX", "Views", "UserCenterPage.axaml.cs");
Check("能定位到 UserCenterPage.axaml.cs", File.Exists(userCenterPath), userCenterPath);

if (File.Exists(userCenterPath))
{
    var src = File.ReadAllText(userCenterPath);
    var body = ExtractMethodBody(src, "private async void UserControl_Loaded");
    Check("能定位到 UserControl_Loaded", body is not null);

    if (body is not null)
    {
        Check("UserControl_Loaded 有 catch 块", body.Contains("catch (Exception", StringComparison.Ordinal));
        Check("UserControl_Loaded 有 finally 块", body.Contains("finally", StringComparison.Ordinal));
        Check("catch 里记录日志", body.Contains("CurrentLogger.Error", StringComparison.Ordinal));
        Check("catch 里给用户可见提示",
            body.Contains("Growl.Error", StringComparison.Ordinal));
        Check("finally 里复位 IsLoading",
            body.Contains("IsLoading = false", StringComparison.Ordinal));
    }
}

Console.WriteLine();
Console.WriteLine("== 4. 源码断言：TrafficStatusControl.UpdateTrafficData 有 catch/finally ==");

var trafficPath = Path.Combine(repoRoot, "MEFrpLauncherX", "Controls", "TrafficStatusControl.axaml.cs");
Check("能定位到 TrafficStatusControl.axaml.cs", File.Exists(trafficPath), trafficPath);

if (File.Exists(trafficPath))
{
    var src = File.ReadAllText(trafficPath);
    var body = ExtractMethodBody(src, "public async void UpdateTrafficData");
    Check("能定位到 UpdateTrafficData", body is not null);

    if (body is not null)
    {
        Check("UpdateTrafficData 有 catch 块", body.Contains("catch (Exception", StringComparison.Ordinal));
        Check("UpdateTrafficData 有 finally 块", body.Contains("finally", StringComparison.Ordinal));

        // IsLoading 的复位必须唯一地出现在 finally 里 ——
        // 原先 5 处分散赋值，任何异常路径都会漏掉。
        var isLoadingFalseCount = CountOccurrences(StripComments(body), "IsLoading = false");
        Check("IsLoading = false 在代码中只出现 1 处（统一由 finally 复位）",
            isLoadingFalseCount == 1, $"实际 {isLoadingFalseCount} 处");

        // finally 必须在最后一个 return 之后、方法体结束之前 —— 用「catch/finally
        // 出现在 IsLoading = false 之前」来近似确认复位发生在兜底块里。
        var catchIdx = body.IndexOf("catch (Exception", StringComparison.Ordinal);
        var finallyIdx = body.IndexOf("finally", StringComparison.Ordinal);
        var resetIdx = body.IndexOf("IsLoading = false", StringComparison.Ordinal);
        Check("IsLoading 复位位于 catch/finally 之后（确实在 finally 内）",
            catchIdx >= 0 && finallyIdx >= 0 && resetIdx > finallyIdx,
            $"catch={catchIdx} finally={finallyIdx} reset={resetIdx}");
    }
}

// =============================================================================
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

// -----------------------------------------------------------------------------
// 故意不兜底的 async void：异常会抛到 SynchronizationContext
// -----------------------------------------------------------------------------
static async void FireAndForgetUnsafe()
{
    await Task.Yield();
    throw new InvalidOperationException("模拟 async void 中的未捕获异常");
}

// 从源码里截出指定方法的方法体（按大括号配平）
static string? ExtractMethodBody(string src, string signature)
{
    var start = src.IndexOf(signature, StringComparison.Ordinal);
    if (start < 0)
    {
        return null;
    }

    var open = src.IndexOf('{', start);
    if (open < 0)
    {
        return null;
    }

    var depth = 0;
    for (var i = open; i < src.Length; i++)
    {
        if (src[i] == '{')
        {
            depth++;
        }
        else if (src[i] == '}')
        {
            depth--;
            if (depth == 0)
            {
                return src[open..(i + 1)];
            }
        }
    }

    return null;
}

// 剔除 // 与 /// 注释，避免注释里的示例文字干扰计数
static string StripComments(string body)
{
    var lines = body.Split('\n');
    var kept = lines.Where(l =>
    {
        var t = l.TrimStart();
        return !t.StartsWith("//", StringComparison.Ordinal) && !t.StartsWith("*", StringComparison.Ordinal);
    });
    return string.Join('\n', kept);
}

static int CountOccurrences(string haystack, string needle)
{
    var count = 0;
    var idx = 0;
    while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
    {
        count++;
        idx += needle.Length;
    }

    return count;
}

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

// 捕获 async void 抛到同步上下文的异常
sealed class CapturingSynchronizationContext : SynchronizationContext
{
    public ConcurrentQueue<Exception> Captured { get; } = new();

    public override void Post(SendOrPostCallback d, object? state)
    {
        // 让回调在独立线程执行，模拟 UI 线程的消息循环
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                d(state);
            }
            catch (Exception ex)
            {
                Captured.Enqueue(ex);
            }
        });
    }
}