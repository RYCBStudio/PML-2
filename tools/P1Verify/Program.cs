// P1 验收脚本（Avalonia 12 升级）
//
// 用途：一键检查「全部项目」的编译状态，覆盖主 sln **以及**不在主 sln 内的
//       MEFrpLauncherX.CrashDisplayer（独立 .sln）与 RYCB.PML2.Splash（独立 .slnx）
//       —— 这两个项目最容易被漏掉。
//
// 用法：dotnet run --project tools/P1Verify
// 退出码：0 = 全部项目 0 错误

using System.Diagnostics;
using System.Text.RegularExpressions;

var repoRoot = FindRepoRoot();
if (repoRoot is null)
{
    Console.WriteLine("未找到仓库根目录（应包含 MEFrpLauncherX.sln）");
    return 2;
}

Console.WriteLine($"仓库根：{repoRoot}");
Console.WriteLine();

// 前两项是独立 sln，必须显式列出，否则会被漏掉。
var targets = new (string Label, string Path)[]
{
    ("MEFrpLauncherX.sln（主 sln）", "MEFrpLauncherX.sln"),
    ("CrashDisplayer（独立 .sln）", @"MEFrpLauncherX.CrashDisplayer\MEFrpLauncherX.CrashDisplayer.csproj"),
    ("Splash（独立 .slnx）", @"RYCB.PML2.Splash\RYCB.PML2.Splash\RYCB.PML2.Splash.csproj"),
};

var totalErrors = 0;
var summary = new List<(string Label, int Errors, int Warnings)>();

foreach (var (label, rel) in targets)
{
    var full = Path.Combine(repoRoot, rel);
    if (!File.Exists(full))
    {
        Console.WriteLine($"[跳过] {label} —— 未找到 {rel}");
        continue;
    }

    Console.WriteLine($"=== {label} ===");
    var (errors, warnings, lines) = Build(repoRoot, full);

    // 去重后打印，避免同一错误因并行编译重复多行
    foreach (var line in lines.Distinct().Take(30))
        Console.WriteLine($"    {line}");
    if (lines.Distinct().Count() > 30)
        Console.WriteLine($"    ...（另有 {lines.Distinct().Count() - 30} 条）");

    Console.WriteLine($"    => 错误 {errors}，警告 {warnings}");
    Console.WriteLine();

    totalErrors += errors;
    summary.Add((label, errors, warnings));
}

Console.WriteLine("===== 汇总 =====");
foreach (var (label, e, w) in summary)
    Console.WriteLine($"    {(e == 0 ? "OK  " : "FAIL")}  {label,-34} 错误 {e,3}  警告 {w,4}");

Console.WriteLine();
Console.WriteLine(totalErrors == 0
    ? "==> P1 通过：全部项目 0 错误"
    : $"==> P1 未通过：合计 {totalErrors} 个错误（见上，按计划 P2 处理）");
return totalErrors == 0 ? 0 : 1;

static (int Errors, int Warnings, List<string> ErrorLines) Build(string workDir, string target)
{
    var psi = new ProcessStartInfo("dotnet", $"build \"{target}\" -v q --nologo")
    {
        WorkingDirectory = workDir,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        StandardOutputEncoding = System.Text.Encoding.UTF8,
        StandardErrorEncoding = System.Text.Encoding.UTF8,
    };

    using var p = Process.Start(psi)!;
    var stdout = p.StandardOutput.ReadToEnd();
    var stderr = p.StandardError.ReadToEnd();
    p.WaitForExit();

    var all = stdout + stderr;
    var errorLines = new List<string>();
    foreach (var raw in all.Split('\n'))
    {
        var m = Regex.Match(raw, @": error (CS|AVLN|MSB|NU)\d+");
        if (!m.Success) continue;
        errorLines.Add(Regex.Replace(raw.Trim(), @"\s*\[[^\]]*\]$", "")
            .Replace(workDir + @"\", ""));
    }

    var wm = Regex.Match(all, @"(\d+) 个警告");
    var em = Regex.Match(all, @"(\d+) 个错误");
    return (em.Success ? int.Parse(em.Groups[1].Value) : errorLines.Count,
            wm.Success ? int.Parse(wm.Groups[1].Value) : 0,
            errorLines);
}

static string? FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "MEFrpLauncherX.sln")))
            return dir.FullName;
        dir = dir.Parent;
    }
    return null;
}
