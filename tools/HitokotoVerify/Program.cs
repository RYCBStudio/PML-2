using System.Reflection;

// =============================================================================
// HitokotoVerify —— PML 2 一言获取链路自检（26.5.1 C5 回归）
//
// 背景（C5）：
//   AboutPage 里「获取一言」的逻辑有两份完全相同的拷贝
//   （AttachedToVisualTree 与 RefreshHitokoto_Click），都写成：
//
//       if (Hitokoto == null)
//       {
//           if (Random.Shared.Next() % 2 == 0)   // ← 只有 50% 概率赋值
//           {
//               Hitokoto = new HitokotoResource { ... };
//           }
//       }
//       vm.Hitokoto = Hitokoto.hitokoto;   // ← 另一半概率 NRE
//
//   也就是说「刷新一言」有 50% 概率抛 NullReferenceException。
//
// 验证策略：
//   A. 行为验证：反射调用真实的 CreateLocalFallback()，反复调用断言
//      每次结果都非 null 且 hitokoto 非空 —— 这是「必定兜底」的核心不变量。
//   B. 源码断言：兜底不再被随机数条件包裹、备用源客户端有超时、
//      两个入口都走统一解析、不再用 ArgumentNullException 当控制流。
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

Console.WriteLine("==> HitokotoVerify：一言获取链路自检");
Console.WriteLine();

var repoRoot = FindRepoRoot();
var aboutPagePath = Path.Combine(repoRoot, "MEFrpLauncherX", "Views", "AboutPage.axaml.cs");
Check("能定位到 AboutPage.axaml.cs", File.Exists(aboutPagePath), aboutPagePath);

if (!File.Exists(aboutPagePath))
{
    Console.WriteLine($"==> 失败：找不到源文件");
    return 1;
}

var src = File.ReadAllText(aboutPagePath);

// =============================================================================
// A. 行为验证：CreateLocalFallback 必定返回可用内容
// =============================================================================
Console.WriteLine();
Console.WriteLine("== 1. 行为验证：本地兜底必定非 null 且文案非空 ==");

var appAssembly = LoadAppAssembly(repoRoot);
Check("能加载主程序集", appAssembly is not null, "请先构建主程序");

if (appAssembly is not null)
{
    var aboutPageType = appAssembly.GetType("MEFrpLauncherX.Views.AboutPage");
    Check("能定位到 AboutPage 类型", aboutPageType is not null);

    var fallbackMethod = aboutPageType?.GetMethod("CreateLocalFallback",
        BindingFlags.NonPublic | BindingFlags.Static);
    Check("能定位到 CreateLocalFallback（兜底构造方法）", fallbackMethod is not null);

    if (fallbackMethod is not null)
    {
        // 原实现只有 50% 概率赋值 —— 单次调用测不出来。
        // 这里跑 200 次：若仍存在「一半概率返回 null」的逻辑，几乎必然被抓到。
        const int iterations = 200;
        var nullCount = 0;
        var emptyTextCount = 0;
        Exception? firstException = null;

        for (var i = 0; i < iterations; i++)
        {
            try
            {
                var result = fallbackMethod.Invoke(null, null);
                if (result is null)
                {
                    nullCount++;
                    continue;
                }

                // 反射读 hitokoto 字段
                var text = result.GetType().GetProperty("hitokoto")?.GetValue(result) as string;
                if (string.IsNullOrWhiteSpace(text))
                {
                    emptyTextCount++;
                }
            }
            catch (Exception ex)
            {
                firstException ??= ex.InnerException ?? ex;
            }
        }

        Check($"{iterations} 次调用均未抛异常", firstException is null,
            firstException is null ? "" : $"{firstException.GetType().Name}: {firstException.Message}");
        Check($"{iterations} 次调用均返回非 null", nullCount == 0,
            $"有 {nullCount} 次返回 null（这正是 C5 的成因）");
        Check($"{iterations} 次调用的 hitokoto 文案均非空", emptyTextCount == 0,
            $"有 {emptyTextCount} 次文案为空");
    }
}

// =============================================================================
// B. 源码断言
// =============================================================================
Console.WriteLine();
Console.WriteLine("== 2. 源码断言：兜底不再被随机数条件包裹 ==");

// 原 bug 的形态：`if (Random...% 2 == 0)` 里只做赋值，条件外继续解引用。
// 修复后随机数只用于「在两种非 null 兜底之间选择」，不再决定「是否赋值」。
var fallbackBody = ExtractMethodBody(src, "private static HitokotoResource CreateLocalFallback");
Check("能定位到 CreateLocalFallback 方法体", fallbackBody is not null);

if (fallbackBody is not null)
{
    var codeOnly = StripComments(fallbackBody);

    // 方法必须有两个 return（笑话 / 固定文案），且都以 return 直接返回对象
    var returnCount = CountOccurrences(codeOnly, "return new HitokotoResource");
    Check("兜底有两条 return 分支（随机笑话 + 固定文案）", returnCount == 2, $"实际 {returnCount} 条");

    // 方法末尾必须是 return（不能隐式落空返回 null）
    Check("兜底方法能以 return 结束（无隐式 null 返回）",
        codeOnly.TrimEnd().TrimEnd('}').TrimEnd().EndsWith(";", StringComparison.Ordinal)
        || codeOnly.Contains("return new HitokotoResource", StringComparison.Ordinal));
}

Console.WriteLine();
Console.WriteLine("== 3. 源码断言：两个入口都走统一解析，不再自行判 null ==");

var resolveBody = ExtractMethodBody(src, "private static async Task<HitokotoResource> ResolveHitokotoAsync");
Check("能定位到 ResolveHitokotoAsync", resolveBody is not null);

if (resolveBody is not null)
{
    Check("ResolveHitokotoAsync 会兜底到本地文案",
        StripComments(resolveBody).Contains("CreateLocalFallback", StringComparison.Ordinal));

    // 两个调用入口都应改为调用 ResolveHitokotoAsync
    var resolveCalls = CountOccurrences(StripComments(src), "ResolveHitokotoAsync()");
    // 1 次是方法自身的签名不算，2 次是两处调用点
    Check("两个入口都调用 ResolveHitokotoAsync（逻辑已去重）",
        resolveCalls >= 2, $"出现 {resolveCalls} 次");
}

// 旧代码把 ArgumentNullException 当作「换备用源」的控制流
Check("不再用 ArgumentNullException 当控制流",
    !StripComments(src).Contains("catch (ArgumentNullException)", StringComparison.Ordinal));

// 旧代码在两处都解引用 Hitokoto.hitokoto —— 修复后不该再有裸解引用
var assignments = CountOccurrences(StripComments(src), "= Hitokoto.hitokoto");
Check("不再有裸解引用 Hitokoto.hitokoto", assignments == 0, $"仍有 {assignments} 处");

Console.WriteLine();
Console.WriteLine("== 4. 源码断言：备用源客户端也有超时 ==");

var backupBody = ExtractMethodBody(src,
    "private static async Task<HitokotoResource?> ExecuteHitokotoBackupRequest");
Check("能定位到 ExecuteHitokotoBackupRequest", backupBody is not null);

if (backupBody is not null)
{
    var codeOnly = StripComments(backupBody);
    Check("备用源客户端设置了 Timeout",
        codeOnly.Contains("Timeout", StringComparison.Ordinal));
    Check("备用源不再用裸 new RestClient(url)",
        !codeOnly.Contains("new RestClient(\"", StringComparison.Ordinal));
}

// 主源与备用源都应经过统一的响应解析（覆盖 HTTP 失败 / 空体 / 非法 JSON）
var parseBody = ExtractMethodBody(src, "private static HitokotoResource? ParseHitokotoResponse");
Check("存在统一的响应解析方法 ParseHitokotoResponse", parseBody is not null);

if (parseBody is not null)
{
    var codeOnly = StripComments(parseBody);
    Check("解析方法检查 IsSuccessful", codeOnly.Contains("IsSuccessful", StringComparison.Ordinal));
    Check("解析方法检查空响应体", codeOnly.Contains("IsNullOrWhiteSpace", StringComparison.Ordinal));
    Check("解析方法捕获 JsonException", codeOnly.Contains("JsonException", StringComparison.Ordinal));
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
// 加载主程序产物（自包含 exe 无法 ProjectReference，只能运行时反射加载）
// -----------------------------------------------------------------------------
static Assembly? LoadAppAssembly(string repoRoot)
{
    var overridden = Environment.GetEnvironmentVariable("PML2_VERIFY_APPDIR");
    var candidates = new[]
    {
        !string.IsNullOrWhiteSpace(overridden) ? Path.Combine(overridden, "MEFrpLauncherX.dll") : null,
        Path.Combine(repoRoot, "MEFrpLauncherX", "bin", "Release", "net10.0", "MEFrpLauncherX.dll"),
        Path.Combine(repoRoot, "MEFrpLauncherX", "bin", "Debug", "net10.0", "MEFrpLauncherX.dll"),
        Path.Combine(repoRoot, "MEFrpLauncherX", "bin", "Release", "net10.0", "win-x64", "MEFrpLauncherX.dll"),
        Path.Combine(repoRoot, "MEFrpLauncherX", "bin", "Debug", "net10.0", "win-x64", "MEFrpLauncherX.dll"),
    };

    foreach (var path in candidates)
    {
        if (path is null || !File.Exists(path))
        {
            continue;
        }

        var dir = Path.GetDirectoryName(path)!;

        // 主程序 bin 里的第三方依赖与探针自身解析到的版本可能不一致，
        // 注册解析回退，优先复用已加载的同名程序集。
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

            var candidate = Path.Combine(dir, name + ".dll");
            if (File.Exists(candidate))
            {
                try
                {
                    return Assembly.LoadFrom(candidate);
                }
                catch
                {
                    return null;
                }
            }

            return null;
        };

        try
        {
            var asm = Assembly.LoadFrom(path);
            // 明确告知用的是哪份产物 —— 回退到陈旧 bin 时最容易误判成「修复没生效」。
            Console.WriteLine($"    加载产物: {path}");
            Console.WriteLine($"    构建时间: {File.GetLastWriteTime(path):yyyy-MM-dd HH:mm:ss}");
            return asm;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    警告：加载 {path} 失败：{ex.Message}");
        }
    }

    return null;
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
