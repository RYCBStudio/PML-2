using System.Diagnostics;

// =============================================================================
// ProxyStopVerify —— PML 2 隧道停止的进程精确性自检（26.5.1 C6 回归）
//
// 背景（C6）：
//   UserProxyViewModel.ForceOfflineProxy 原实现对 Windows 用
//   `taskkill /im mefrpc.exe /T /F`、对 Linux 用 `pkill mefrpc` ——
//   两者都按「进程名」匹配，会把本机所有隧道的 mefrpc 进程一起杀掉。
//   而该方法也被 StopProxy 调用，于是停掉一个隧道会连带断开其他所有隧道。
//
// 验证策略：
//   A. OS 级实证：起两个同名进程，分别用 /im（按名）与 /PID（按进程号）终止，
//      实测「按名会全杀、按进程号只杀一个」—— 这是本缺陷的物理根因。
//   B. 源码断言：ForceOfflineProxy 不再按进程名杀、改为按隧道名精确定位；
//      StopProxy 不重复终止进程。
//
// 本工具只启动并清理自己创建的 pml2verifytarget.exe 临时进程，
// 不触碰系统上的任何其他进程。
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

Console.WriteLine("==> ProxyStopVerify：隧道停止的进程精确性自检");
Console.WriteLine();

var repoRoot = FindRepoRoot();

// =============================================================================
// B. 源码断言（先做，不依赖平台）
// =============================================================================
Console.WriteLine("== 1. 源码断言：ForceOfflineProxy 不再按进程名杀 ==");

var vmPath = Path.Combine(repoRoot, "MEFrpLauncherX", "ViewModels", "UserProxyViewModel.cs");
Check("能定位到 UserProxyViewModel.cs", File.Exists(vmPath), vmPath);

if (File.Exists(vmPath))
{
    var src = File.ReadAllText(vmPath);
    var codeOnly = StripComments(src);

    // 按进程名杀的两个具体写法
    Check("不再使用 taskkill /im mefrpc.exe（会杀掉所有隧道）",
        !codeOnly.Contains("/im mefrpc.exe", StringComparison.Ordinal));
    Check("不再使用 pkill mefrpc（同样按名匹配，会杀掉所有隧道）",
        !codeOnly.Contains("pkill", StringComparison.Ordinal)
        || !codeOnly.Contains("mefrpc", StringComparison.Ordinal));

    // 改为按隧道名精确定位
    var stopLocalBody = ExtractMethodBody(src, "private static async Task StopLocalClientAsync");
    Check("存在按隧道名停止本地进程的方法 StopLocalClientAsync", stopLocalBody is not null);
    if (stopLocalBody is not null)
    {
        var body = StripComments(stopLocalBody);
        Check("StopLocalClientAsync 走 SendCtrlCCommandToSelected（按标签头 = 隧道名定位）",
            body.Contains("SendCtrlCCommandToSelected", StringComparison.Ordinal));
        Check("StopLocalClientAsync 接收隧道名参数",
            stopLocalBody.Contains("proxyName", StringComparison.Ordinal));
    }

    // ForceOfflineProxy 应委托给统一实现（它是表达式体，没有 { } 方法体）
    var forceIdx = src.IndexOf("private async void ForceOfflineProxy", StringComparison.Ordinal);
    Check("存在 ForceOfflineProxy 入口", forceIdx >= 0);

    if (forceIdx >= 0)
    {
        // 表达式体以分号结束，只截到第一个分号 —— 避免把后续方法的注释也纳入
        var semi = src.IndexOf(';', forceIdx);
        var snippet = semi > forceIdx ? src[forceIdx..(semi + 1)] : src[forceIdx..];

        Check("ForceOfflineProxy 委托给 ForceOfflineProxyCoreAsync",
            snippet.Contains("ForceOfflineProxyCoreAsync", StringComparison.Ordinal));
        Check("ForceOfflineProxy 自身不再直接调用 taskkill / pkill",
            !snippet.Contains("taskkill", StringComparison.Ordinal)
            && !snippet.Contains("pkill", StringComparison.Ordinal));
    }

    var coreBody = ExtractMethodBody(src, "private async Task ForceOfflineProxyCoreAsync");
    Check("存在 ForceOfflineProxyCoreAsync 实现", coreBody is not null);
    if (coreBody is not null)
    {
        var body = StripComments(coreBody);
        Check("核心实现按隧道停止本地进程（StopLocalClientAsync）",
            body.Contains("StopLocalClientAsync", StringComparison.Ordinal));
        Check("核心实现有 stopLocalClient 开关（供 StopProxy 复用避免重复终止）",
            coreBody.Contains("stopLocalClient", StringComparison.Ordinal));
    }

    // StopProxy 已在调用前精确停止过，不应让核心实现再停一次
    var stopProxyBody = ExtractMethodBody(src, "private async void StopProxy");
    Check("存在 StopProxy", stopProxyBody is not null);
    if (stopProxyBody is not null)
    {
        var body = StripComments(stopProxyBody);
        Check("StopProxy 先精确停止本隧道进程",
            body.Contains("StopLocalClientAsync", StringComparison.Ordinal));
        Check("StopProxy 调用核心实现时传 stopLocalClient: false（不重复终止）",
            body.Contains("stopLocalClient: false", StringComparison.Ordinal));
    }
}

// =============================================================================
// A. OS 级实证：/im 按名会全杀，/PID 只杀一个
// =============================================================================
Console.WriteLine();
Console.WriteLine("== 2. OS 级实证：按进程名 vs 按进程号 ==");

if (!OperatingSystem.IsWindows())
{
    Console.WriteLine("    SKIP：本项仅验证 Windows 的 taskkill 语义（Linux 的 pkill 同理）");
}
else
{
    // 用 ping 的副本充当「同名进程」—— 复制到临时目录并改名，
    // 这样不会与系统上真实的进程重名。
    var sandbox = Path.Combine(Path.GetTempPath(), "PML2-ProxyStopVerify-" + Guid.NewGuid().ToString("N")[..8]);
    Directory.CreateDirectory(sandbox);

    var pingPath = Path.Combine(Environment.SystemDirectory, "ping.exe");
    var targetName = "pml2verifytarget.exe";
    var targetPath = Path.Combine(sandbox, targetName);

    var startedPids = new List<int>();

    try
    {
        if (!File.Exists(pingPath))
        {
            Console.WriteLine("    SKIP：找不到 ping.exe，无法构造同名进程");
        }
        else
        {
            File.Copy(pingPath, targetPath, overwrite: true);

            // ---- 2a. 按进程名：两个都该死 ----
            startedPids.Clear();
            startedPids.Add(StartTarget(targetPath));
            startedPids.Add(StartTarget(targetPath));
            Thread.Sleep(600);

            var aliveBefore = CountAlive(startedPids);
            Check("（前置）两个同名进程都已启动", aliveBefore == 2, $"实际存活 {aliveBefore}");

            RunTaskkill($"/im {targetName} /T /F");
            Thread.Sleep(800);

            var aliveAfterIm = CountAlive(startedPids);
            Check("按进程名 taskkill /im 会把两个同名进程都杀掉（这正是 C6 的物理根因）",
                aliveAfterIm == 0, $"仍有 {aliveAfterIm} 个存活");

            // ---- 2b. 按进程号：只杀一个 ----
            startedPids.Clear();
            startedPids.Add(StartTarget(targetPath));
            startedPids.Add(StartTarget(targetPath));
            Thread.Sleep(600);

            var aliveBeforePid = CountAlive(startedPids);
            Check("（前置）两个同名进程已重新启动", aliveBeforePid == 2, $"实际存活 {aliveBeforePid}");

            var victim = startedPids[0];
            var survivor = startedPids[1];

            RunTaskkill($"/PID {victim} /T /F");
            Thread.Sleep(800);

            Check("按进程号 taskkill /PID 只杀掉目标进程", !IsAlive(victim));
            Check("另一个同名进程不受影响（这是修复后的行为）", IsAlive(survivor));
        }
    }
    catch (Exception ex)
    {
        failures.Add("OS 实证失败: " + ex.Message);
        Console.WriteLine($"  [FAIL] OS 实证异常: {ex.GetType().Name}: {ex.Message}");
    }
    finally
    {
        // 清理：只杀本工具自己启动的进程
        foreach (var pid in startedPids)
        {
            try
            {
                if (IsAlive(pid))
                {
                    RunTaskkill($"/PID {pid} /T /F");
                }
            }
            catch
            {
                // 忽略清理失败
            }
        }

        try
        {
            Directory.Delete(sandbox, true);
        }
        catch
        {
            // 忽略
        }
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
// 启动一个长时间运行的副本进程（用它自己的 exe 名）
// -----------------------------------------------------------------------------
static int StartTarget(string exePath)
{
    var psi = new ProcessStartInfo
    {
        FileName = exePath,
        // ping 一个不存在的地址并给足次数，让它保持运行
        Arguments = "-n 60 -w 1000 192.0.2.1",
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true
    };

    using var p = Process.Start(psi)!;
    return p.Id;
}

static bool IsAlive(int pid)
{
    try
    {
        using var p = Process.GetProcessById(pid);
        return !p.HasExited;
    }
    catch
    {
        return false;
    }
}

static int CountAlive(List<int> pids) => pids.Count(IsAlive);

// 调用 taskkill，忽略其退出码
static void RunTaskkill(string arguments)
{
    var psi = new ProcessStartInfo
    {
        FileName = "taskkill",
        Arguments = arguments,
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };

    using var p = Process.Start(psi);
    p?.WaitForExit(5000);
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

// 剔除行注释，避免注释里的示例代码干扰断言
static string StripComments(string body)
{
    var lines = body.Split('\n');
    var kept = lines.Where(l =>
    {
        var t = l.TrimStart();
        return !t.StartsWith("//", StringComparison.Ordinal)
               && !t.StartsWith("///", StringComparison.Ordinal)
               && !t.StartsWith("*", StringComparison.Ordinal);
    });
    return string.Join('\n', kept);
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
