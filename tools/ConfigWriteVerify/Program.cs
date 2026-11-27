using System.Text.Json;
using MEFrpLauncherX.Core;

// =============================================================================
// ConfigWriteVerify —— PML 2 配置文件写入的原子性 / 互斥性自检（26.5.1 C7 回归）
//
// 背景（C7）：
//   SaveConfig 原实现 File.WriteAllText(ConfigPath, json) 直接覆盖 —— 写到一半
//   被杀/断电会留下截断 JSON，下次启动被判为「配置损坏」并静默重置为默认配置。
//   SaveConfigAsync 序列化在 lock 内、await 写文件在 lock 外 —— 并发保存会交错
//   写同一文件，产出半截 JSON。
//
// 验证：
//   A. 源码断言
//   B. 行为验证（临时数据根，不触碰真实配置）
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

Console.WriteLine("==> ConfigWriteVerify：配置写入原子性 / 互斥性自检");
Console.WriteLine();

var repoRoot = FindRepoRoot();

// =============================================================================
// A. 源码断言
// =============================================================================
Console.WriteLine("== 1. 源码断言：不再有裸覆盖写，改用临时文件 + 原子替换 ==");

var cmPath = Path.Combine(repoRoot, "MEFrpLauncherX.Core", "ConfigManager.cs");
Check("能定位到 ConfigManager.cs", File.Exists(cmPath), cmPath);

if (File.Exists(cmPath))
{
    var src = File.ReadAllText(cmPath);

    // 1.1 不再有任何「直接写正式配置」的裸调用
    Check("不再出现裸 File.WriteAllText(ConfigPath, ...)（覆盖写会留下截断文件）",
        !src.Contains("File.WriteAllText(ConfigPath", StringComparison.Ordinal));
    Check("不再出现裸 File.WriteAllTextAsync(ConfigPath, ...)",
        !src.Contains("File.WriteAllTextAsync(ConfigPath", StringComparison.Ordinal));

    // 1.2 同步保存：序列化后委托原子写，自身不做文件 IO
    var saveBody = ExtractMethodBody(src, "public static void SaveConfig()");
    Check("能定位到 SaveConfig 方法体", saveBody is not null);
    if (saveBody is not null)
    {
        var body = StripComments(saveBody);
        Check("SaveConfig 委托给 WriteConfigAtomically（原子写）",
            body.Contains("WriteConfigAtomically", StringComparison.Ordinal));
        Check("SaveConfig 自身不再直接写文件（IO 已移出）",
            !body.Contains("File.WriteAllText", StringComparison.Ordinal)
            && !body.Contains("File.WriteAllTextAsync", StringComparison.Ordinal));
    }

    // 1.3 异步保存：序列化后取同一把写信号量再写盘
    var saveAsyncBody = ExtractMethodBody(src, "public static async Task SaveConfigAsync()");
    Check("能定位到 SaveConfigAsync 方法体", saveAsyncBody is not null);
    if (saveAsyncBody is not null)
    {
        var body = StripComments(saveAsyncBody);
        Check("SaveConfigAsync 在写盘前获取 _writeSemaphore（与同步路径共用写锁）",
            body.Contains("_writeSemaphore.WaitAsync", StringComparison.Ordinal));
        Check("SaveConfigAsync 委托给 WriteConfigAtomicallyAsync",
            body.Contains("WriteConfigAtomicallyAsync", StringComparison.Ordinal));
    }

    // 1.4 同步原子写内部取同一把写锁
    var atomicBody = ExtractMethodBody(src, "private static void WriteConfigAtomically(string content)");
    Check("能定位到 WriteConfigAtomically 方法体", atomicBody is not null);
    if (atomicBody is not null)
    {
        var body = StripComments(atomicBody);
        Check("WriteConfigAtomically 获取 _writeSemaphore（与异步路径共用写锁）",
            body.Contains("_writeSemaphore.Wait()", StringComparison.Ordinal));
        Check("WriteConfigAtomically 用 ReplaceWithTemp 做原子替换",
            body.Contains("ReplaceWithTemp", StringComparison.Ordinal));
    }

    // 1.5 原子替换用 File.Move(overwrite)，而非「先删再移」（后者有双缺失窗口）
    var replaceBody = ExtractMethodBody(src, "private static void ReplaceWithTemp(string temp)");
    Check("能定位到 ReplaceWithTemp 方法体", replaceBody is not null);
    if (replaceBody is not null)
    {
        var body = StripComments(replaceBody);
        Check("ReplaceWithTemp 用 File.Move(..., overwrite: true)（底层原子替换）",
            body.Contains("File.Move(temp, ConfigPath, overwrite: true)", StringComparison.Ordinal));
        Check("ReplaceWithTemp 不再「先删除再移动」（避免两文件都不存在的窗口）",
            !body.Contains("File.Delete(ConfigPath)", StringComparison.Ordinal));
    }

    // 1.6 启动时清理上次异常退出遗留的 .tmp
    var initBody = ExtractMethodBody(src, "public static void Initialize()");
    Check("Initialize 调用 CleanupStaleTempFile（清理上次遗留 .tmp）",
        initBody is not null
        && StripComments(initBody).Contains("CleanupStaleTempFile", StringComparison.Ordinal));
    Check("存在 CleanupStaleTempFile 实现",
        ExtractMethodBody(src, "private static void CleanupStaleTempFile()") is not null);
}

// =============================================================================
// B. 行为验证：用临时数据根，不触碰真实配置
// =============================================================================
Console.WriteLine();
Console.WriteLine("== 2. 行为验证：原子写 / 并发读 ==");

var sandbox = Path.Combine(Path.GetTempPath(), "pml2-configverify-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(sandbox);

// 必须在首次触碰 AppPaths / ConfigManager 之前设置，让静态路径解析到沙箱。
Environment.SetEnvironmentVariable("PML2_INSTALL_ROOT", sandbox);
Environment.SetEnvironmentVariable("PML2_DATA_ROOT", sandbox);

try
{
    // 初始化 App（建立 AppJsonSerializerContext 与日志），但不让它自动跑 ConfigManager。
    await App.Initialize(externalUse: true);
    ConfigManager.Initialize();

    var settingsPath = ConfigManager.ConfigPath;
    var tmpPath = settingsPath + ".tmp";

    Check("配置文件已生成", File.Exists(settingsPath), settingsPath);

    // 2.1 保存后不留 .tmp 残留
    ConfigManager.SaveConfig();
    Check("保存后不残留 .tmp 文件（原子替换已消费临时文件）", !File.Exists(tmpPath), tmpPath);

    // 2.2 上次异常退出遗留的 .tmp 会被清理，且不覆盖有效配置
    ConfigManager.Initialize(); // 确保正式文件是完整合法 JSON
    var validBefore = File.ReadAllText(settingsPath);
    File.WriteAllText(tmpPath, "{ \"SchemaVersion\": 4, \"truncated\":"); // 模拟崩溃时的半截临时文件
    Check("（前置）已放置一个截断的 .tmp", File.Exists(tmpPath));

    ConfigManager.Initialize(); // 再次初始化：应清理 .tmp、读取正式文件

    Check("启动时清理了上次遗留的 .tmp", !File.Exists(tmpPath), tmpPath);
    var afterCleanup = File.ReadAllText(settingsPath);
    Check("有效配置未被截断的 .tmp 覆盖（正式文件保持完整）",
        afterCleanup == validBefore, "配置内容发生了变化");
    Check("（复核）正式文件仍是合法 JSON", IsValidJson(afterCleanup));

    // 2.3 并发「写 + 读」：读方永远看到完整的合法 JSON（原子替换的核心不变量）
    //     旧实现直接覆盖写正式文件，读方会读到「截断/半截」内容 → JSON 解析失败。
    var payload = new string('x', 400_000);
    ConfigManager.UpdateConfig(c => c.SplashCustomImagePath = payload);

    var invalidReads = 0;
    var totalReads = 0;
    using var cts = new CancellationTokenSource();

    var reader = Task.Run(() =>
    {
        while (!cts.IsCancellationRequested)
        {
            try
            {
                // 允许并发写 + 删除：原子替换在有人持读句柄时也能完成
                using var fs = new FileStream(settingsPath, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                using var sr = new StreamReader(fs);
                var text = sr.ReadToEnd();
                Interlocked.Increment(ref totalReads);
                if (!IsValidJson(text))
                {
                    Interlocked.Increment(ref invalidReads);
                }
            }
            catch (FileNotFoundException)
            {
                // 原子替换不应让文件消失；出现即视为非原子证据
                Interlocked.Increment(ref invalidReads);
            }
            catch (IOException)
            {
                // 与替换动作的共享冲突，不代表内容错误
            }
            catch (UnauthorizedAccessException)
            {
                // 读取瞬间撞上原子替换的句柄切换，不代表内容错误
            }
        }
    });

    for (var i = 0; i < 150; i++)
    {
        if (i % 2 == 0)
        {
            ConfigManager.SaveConfig();
        }
        else
        {
            await ConfigManager.SaveConfigAsync();
        }
    }

    cts.Cancel();
    await reader;

    Console.WriteLine($"     （并发读 {totalReads} 次，非法 {invalidReads} 次）");
    Check("并发写期间读方有实际读到内容", totalReads > 0, $"总读取 {totalReads} 次");
    Check("并发写期间读方从未读到非法/截断 JSON（原子替换保证）",
        invalidReads == 0, $"非法读取 {invalidReads} 次");

    var final = File.ReadAllText(settingsPath);
    Check("全部写完后正式文件是完整合法 JSON", IsValidJson(final));

    // 停掉并发后再做一次普通保存，确保临时文件被消费（清理重试未成功的极端残留）
    ConfigManager.SaveConfig();
    Check("全部写完后不残留 .tmp 文件", !File.Exists(tmpPath), tmpPath);
}
catch (Exception ex)
{
    Check("行为验证未抛出异常", false, ex.GetType().Name + ": " + ex.Message);
}
finally
{
    try
    {
        // 释放日志句柄后再删沙箱
        App.CurrentLogger?.Dispose();
    }
    catch
    {
        // 忽略
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
static bool IsValidJson(string text)
{
    if (string.IsNullOrWhiteSpace(text))
    {
        return false;
    }

    try
    {
        using var _ = JsonDocument.Parse(text);
        return true;
    }
    catch
    {
        return false;
    }
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

// 剔除注释，避免注释里的示例代码干扰断言
static string StripComments(string body)
{
    var lines = body.Split('\n');
    var kept = lines.Where(l =>
    {
        var t = l.TrimStart();
        return !t.StartsWith("//", StringComparison.Ordinal)
               && !t.StartsWith("/*", StringComparison.Ordinal)
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
