using System.IO.Compression;
using System.Text.Json;
using MEFrpLauncherX.Core.Services;

// 临时验证：UpdatePackageService.Apply 的解压 → 落位 → 版本槽位 → 清理 全流程
// 用法：dotnet run --project .tmp-verify/updtest

var root = Path.Combine(Path.GetTempPath(), "pml2-updtest-" + Guid.NewGuid().ToString("N")[..8]);
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

string MakeZip(string version, string fileName)
{
    var dir = Path.Combine(root, "zip-src-" + version);
    Directory.CreateDirectory(Path.Combine(dir, "Tools"));
    File.WriteAllText(Path.Combine(dir, fileName), "MZ-" + version);
    File.WriteAllText(Path.Combine(dir, "Tools", "splash.exe"), "splash-" + version);

    var zip = Path.Combine(root, $"pml2-{version}-windows-x64-aot.zip");
    if (File.Exists(zip))
    {
        File.Delete(zip);
    }

    ZipFile.CreateFromDirectory(dir, zip);
    return zip;
}

// ---------- 准备安装根：已存在 v1.0.0 ----------
Directory.CreateDirectory(root);
Directory.CreateDirectory(Path.Combine(root, "data"));
var v100 = Path.Combine(root, "v1.0.0");
Directory.CreateDirectory(v100);
File.WriteAllText(Path.Combine(v100, "MEFrpLauncherX.exe"), "old");
File.WriteAllText(Path.Combine(root, "launcher.json"),
    """{ "schema": 1, "current": "v1.0.0", "previous": "", "aot": true }""");

Console.WriteLine("installRoot = " + root);
Console.WriteLine("IsVersionedLayout = " + MEFrpLauncherX.Core.AppPaths.IsVersionedLayout);
Check(MEFrpLauncherX.Core.AppPaths.IsVersionedLayout, "临时安装根被识别为版本化布局");

// ---------- 第 1 次升级：保留回滚点 ----------
var zip120 = MakeZip("1.2.0", "MEFrpLauncherX.exe");
var r1 = UpdatePackageService.Apply(zip120, "1.2.0", removeOldVersions: false);
Check(r1.Success, "第 1 次 Apply 成功（" + r1.Error + "）");
Check(Directory.Exists(Path.Combine(root, "v1.2.0")), "解压出 v1.2.0 目录");
Check(File.Exists(Path.Combine(root, "v1.2.0", "MEFrpLauncherX.exe")), "v1.2.0 下存在主程序（未多套一层目录）");
Check(File.Exists(Path.Combine(root, "v1.2.0", "Tools", "splash.exe")), "v1.2.0 下 Tools/splash 到位");
Check(Directory.Exists(v100), "保留回滚点 v1.0.0");

// 主程序内容必须是新包的（确认不是复用了旧目录）
Check(File.ReadAllText(Path.Combine(root, "v1.2.0", "MEFrpLauncherX.exe")) == "MZ-1.2.0", "新目录内容是本次更新包");

var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "launcher.json"))).RootElement;
Check(manifest.GetProperty("current").GetString() == "v1.2.0", "launcher.json current = v1.2.0");
Check(manifest.GetProperty("previous").GetString() == "v1.0.0", "launcher.json previous = v1.0.0");

// ---------- 重复操作同一版本：整目录替换而非混合 ----------
File.WriteAllText(Path.Combine(root, "v1.2.0", "stale.txt"), "stale");
var r2 = UpdatePackageService.Apply(zip120, "v1.2.0", removeOldVersions: false);
Check(r2.Success, "同版本重装成功（带前导 v 的版本号）");
Check(!File.Exists(Path.Combine(root, "v1.2.0", "stale.txt")), "同版本重装会清掉旧目录里的残留文件");

// ---------- 第 2 次升级：不保留回滚点 ----------
var zip130 = MakeZip("1.3.0", "MEFrpLauncherX.exe");
var r3 = UpdatePackageService.Apply(zip130, "1.3.0", removeOldVersions: true);
Check(r3.Success, "第 2 次 Apply 成功（" + r3.Error + "）");
Check(Directory.Exists(Path.Combine(root, "v1.3.0")), "解压出 v1.3.0 目录");
Check(!Directory.Exists(v100), "不保留回滚点时旧版本目录被清理");
Check(!Directory.Exists(Path.Combine(root, "v1.2.0")), "不保留回滚点时上一个版本目录被清理");

manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "launcher.json"))).RootElement;
Check(manifest.GetProperty("current").GetString() == "v1.3.0", "launcher.json current = v1.3.0");
Check(manifest.GetProperty("previous").GetString() == "", "不保留回滚点时 previous 为空");

// ---------- 坏包：缺少主程序 ----------
var badDir = Path.Combine(root, "bad-src");
Directory.CreateDirectory(badDir);
File.WriteAllText(Path.Combine(badDir, "readme.txt"), "no exe");
var badZip = Path.Combine(root, "bad.zip");
ZipFile.CreateFromDirectory(badDir, badZip);
var r4 = UpdatePackageService.Apply(badZip, "1.4.0", removeOldVersions: false);
Check(!r4.Success, "缺少主程序的包被拒绝");
Check(!Directory.Exists(Path.Combine(root, "v1.4.0")), "拒绝后不留下版本目录");
Check(!Directory.Exists(Path.Combine(root, ".update-v1.4.0.staging")), "拒绝后不留下临时解压目录");
manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "launcher.json"))).RootElement;
Check(manifest.GetProperty("current").GetString() == "v1.3.0", "失败时版本槽位保持不变");

// ---------- 拒绝“覆盖当前运行版本” ----------
var currentFolder = MEFrpLauncherX.Core.AppPaths.CurrentVersionFolder;
Console.WriteLine("CurrentVersionFolder = " + (currentFolder == "" ? "(空)" : currentFolder));

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
