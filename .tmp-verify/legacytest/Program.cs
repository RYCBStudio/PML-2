using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Services;

// 临时验证：LegacyLayoutCleaner 的识别 / 收拢 / 删除 / 迁移保护
// 用法：dotnet run --project .tmp-verify/legacytest            （主场景，复刻用户反馈的安装根）
//       dotnet run --project .tmp-verify/legacytest -- protect （数据迁移保护场景）
// 两个场景必须分进程跑：AppPaths 的 InstallRoot 是进程内 Lazy 缓存。

var protectMode = args is ["protect"];
var root = Path.Combine(Path.GetTempPath(),
    (protectMode ? "pml2-legacytest-protect-" : "pml2-legacytest-") + Guid.NewGuid().ToString("N")[..8]);

Environment.SetEnvironmentVariable("PML2_INSTALL_ROOT", root);
Environment.SetEnvironmentVariable("PML2_DATA_ROOT", Path.Combine(root, "data"));

var failures = 0;
void Check(bool condition, string message)
{
    Console.WriteLine((condition ? "[PASS] " : "[FAIL] ") + message);
    if (!condition)
    {
        failures++;
    }
}

void File_(string relPath, string content = "x")
{
    var full = Path.Combine(root, relPath);
    Directory.CreateDirectory(Path.GetDirectoryName(full)!);
    File.WriteAllText(full, content);
}

void Dir_(string relPath) => Directory.CreateDirectory(Path.Combine(root, relPath));

Console.WriteLine("installRoot = " + root);
Console.WriteLine("mode = " + (protectMode ? "protect" : "main"));

if (protectMode)
{
    // ---------- 数据迁移保护：旧 Config 尚未迁移时不能搬 ----------
    Dir_("data");
    File_("PML 2.exe", "launcher");
    Dir_("v1.0.0");
    File_("v1.0.0/MEFrpLauncherX.exe", "app");
    Dir_("Config/frp");
    File_("Config/frp/old.ini", "old");
    Dir_("Resources");
    File_("Resources/splash.png");

    var protectedItems = LegacyLayoutCleaner.Inspect().Select(i => i.Name).ToList();
    Console.WriteLine("第一轮识别: " + string.Join(", ", protectedItems));
    Check(!protectedItems.Contains("Config"), "未迁移的旧 Config 被保护，不会被搬走");
    Check(protectedItems.Contains("Resources"), "同一轮里其它残留照常收拢");

    // data 里已有内容 → 视为迁移完成 → 旧 Config 可收拢
    File_("data/Config/frp/new.ini", "new");
    var migratedItems = LegacyLayoutCleaner.Inspect().Select(i => i.Name).ToList();
    Console.WriteLine("第二轮识别: " + string.Join(", ", migratedItems));
    Check(migratedItems.Contains("Config"), "data 已有内容时旧 Config 视为残留，可收拢");
}
else
{
    // ---------- 主场景：复刻用户反馈的安装根 ----------
    Dir_("data/Config/frp");
    Dir_("data/Cache");
    Dir_("data/Logs/Crash");
    Dir_("data/Run");
    File_("data/Config/Settings.json", "{}");
    File_("PML 2.exe", "launcher");
    File_("launcher.json", """{ "schema": 1, "current": "v26.5.1-preview1" }""");
    File_("launcher.log", "log");
    Dir_("v26.5.1-preview1/Tools");
    File_("v26.5.1-preview1/MEFrpLauncherX.exe", "app");
    File_("v26.5.1-preview1/Tools/splash.exe", "splash");

    // 旧版扁平残留
    Dir_("Resources");
    File_("Resources/splash.png");
    Dir_("runtimes/win-x64/native");
    File_("runtimes/win-x64/native/WinToastWrapper.dll");
    Dir_("Tools");
    File_("Tools/splash.exe");
    // 迁移向导归档
    Dir_("Cache.old-20261025_082153");
    File_("Cache.old-20261025_082153/x.bin");
    Dir_("Config.old-20261025_082153/frp");
    File_("Config.old-20261025_082153/frp/p1.ini");
    Dir_("Logs.old-20261025_082153/Crash");
    File_("Logs.old-20261025_082153/Crash/c.log");
    // 必须保留的
    File_("unins000.exe", "unins");
    File_("unins000.dat", "dat");
    File_("desktop.ini", "[.ShellClassInfo]");

    Check(AppPaths.IsVersionedLayout, "临时安装根被识别为版本化布局");

    // 1. 识别
    var names = LegacyLayoutCleaner.Inspect()
        .Select(i => i.Name)
        .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
        .ToList();
    Console.WriteLine("残留: " + string.Join(", ", names));

    Check(names.SequenceEqual(new[]
    {
        "Cache.old-20261025_082153", "Config.old-20261025_082153", "Logs.old-20261025_082153",
        "Resources", "runtimes", "Tools"
    }, StringComparer.OrdinalIgnoreCase), "只识别出旧版残留，白名单项一个都不在内");

    Check(!names.Contains("data") && !names.Contains("legacy") && !names.Contains("v26.5.1-preview1"),
        "data / legacy / vXXX 未被当成残留");
    Check(!names.Contains("PML 2.exe") && !names.Contains("launcher.json") && !names.Contains("launcher.log"),
        "启动器 / launcher.json / launcher.log 保留");
    Check(!names.Contains("unins000.exe") && !names.Contains("unins000.dat") && !names.Contains("desktop.ini"),
        "卸载器与系统隐藏文件保留");

    // 2. 收拢
    var result = LegacyLayoutCleaner.Sweep();
    Check(result.Moved.Count == 6, $"收拢 6 项（实际 {result.Moved.Count}）");
    Check(result.Failed.Count == 0, "无失败项");
    Check(Directory.Exists(Path.Combine(root, "legacy", "Resources")), "Resources 已进入 legacy");
    Check(Directory.Exists(Path.Combine(root, "legacy", "runtimes", "win-x64", "native")), "runtimes 目录树完整搬移");
    Check(File.Exists(Path.Combine(root, "legacy", "Config.old-20261025_082153", "frp", "p1.ini")),
        "*.old-* 归档已进入 legacy");
    Check(!Directory.Exists(Path.Combine(root, "Resources")), "安装根不再有 Resources");
    Check(Directory.Exists(Path.Combine(root, "v26.5.1-preview1")), "版本目录原地未动");
    Check(File.Exists(Path.Combine(root, "PML 2.exe")), "启动器原地未动");
    Check(File.Exists(Path.Combine(root, "data", "Config", "Settings.json")), "data 原地未动");

    var again = LegacyLayoutCleaner.Sweep();
    Check(again.Moved.Count == 0, "再次收拢为空操作（幂等）");

    // 3. legacy 统计
    var legacyItems = LegacyLayoutCleaner.InspectLegacy();
    Check(legacyItems.Count == 6, $"legacy 中 6 项（实际 {legacyItems.Count}）");
    Check(legacyItems.Sum(i => i.SizeBytes) > 0, "legacy 体积统计 > 0");

    // 4. 删除
    var (failed, freed) = LegacyLayoutCleaner.DeleteLegacy();
    Check(failed == 0, "删除 legacy 无失败项");
    Check(freed > 0, $"回收体积 > 0（{freed} 字节）");
    Check(!Directory.Exists(Path.Combine(root, "legacy")), "legacy 目录被一并移除");
}

Console.WriteLine(failures == 0 ? "ALL PASS" : $"{failures} FAILED");

try
{
    Directory.Delete(root, true);
}
catch
{
    // 清理失败不影响结论
}

return failures == 0 ? 0 : 1;
