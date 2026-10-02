// P4 验收：TrimmerRoots.xml 逐项校验（Avalonia 12 升级）
//
// 为什么需要这个脚本：
//   ILC / linker 对 <assembly fullname> 不匹配的条目**静默忽略**——
//   既没有构建错误，也没有警告，条目只是不生效。Avalonia 大版本升级后
//   程序集名与类型全名都可能变化，于是产生「看起来配了裁剪规则、实际完全没配」
//   的隐蔽故障，只在 AOT 产物上才暴露。
//
// 校验方式：
//   1) 程序集名：在 bin 产物 + NuGet 缓存中查找同名 dll；
//   2) 类型全名：用 System.Reflection.Metadata 读取该 dll 的真实 TypeDefinition 列表比对；
//      "Foo.*" 视为前缀通配，"*" 视为全量。
//
// 用法：dotnet run --project tools/P4Verify
// 判定：全部命中 => 退出码 0；存在失效条目 => 退出码 1 并逐条列出。

using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;

var repoRoot = FindRepoRoot();
var rootsFile = Path.Combine(repoRoot, "MEFrpLauncherX", "TrimmerRoots.xml");
var binDir = Path.Combine(repoRoot, "MEFrpLauncherX", "bin", "Debug", "net10.0");

Console.WriteLine("==> P4 验收：TrimmerRoots.xml 逐项校验");
Console.WriteLine($"    规则文件: {rootsFile}");
Console.WriteLine($"    产物目录: {binDir}");
Console.WriteLine();

if (!File.Exists(rootsFile))
{
    Console.WriteLine("==> 失败：找不到 TrimmerRoots.xml");
    Environment.Exit(1);
    return 1;
}

// ---- 1. 建立可用程序集索引（bin 产物优先，其后用 NuGet 缓存兜底）----
var available = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

if (Directory.Exists(binDir))
{
    foreach (var f in Directory.GetFiles(binDir, "*.dll"))
    {
        available[Path.GetFileNameWithoutExtension(f)] = f;
    }
}

var binCount = available.Count;
var nugetRoot = ResolveNuGetRoot();
if (nugetRoot is not null && Directory.Exists(nugetRoot))
{
    foreach (var pkgDir in Directory.GetDirectories(nugetRoot))
    {
        foreach (var dll in Directory.GetFiles(pkgDir, "*.dll", SearchOption.AllDirectories))
        {
            available.TryAdd(Path.GetFileNameWithoutExtension(dll), dll);
        }
    }
}

Console.WriteLine($"    程序集索引: 产物 {binCount} 个，含 NuGet 缓存共 {available.Count} 个");
Console.WriteLine();

// ---- 2. 逐条校验 ----
var doc = XDocument.Load(rootsFile);
var typeCache = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

var assemblyTotal = 0;
var typeTotal = 0;
var failures = new List<string>();

foreach (var asm in doc.Descendants("assembly"))
{
    assemblyTotal++;
    var asmName = asm.Attribute("fullname")?.Value ?? "(no fullname)";

    if (!available.TryGetValue(asmName, out var dllPath))
    {
        failures.Add($"程序集不存在: {asmName}");
        Console.WriteLine($"    FAIL 程序集不存在  {asmName}");
        continue;
    }

    if (!typeCache.TryGetValue(asmName, out var typeNames))
    {
        try
        {
            typeNames = ReadTypeNames(dllPath);
        }
        catch (Exception ex)
        {
            failures.Add($"读取失败: {asmName}（{ex.GetType().Name}: {ex.Message}）");
            Console.WriteLine($"    FAIL 读取失败      {asmName}: {ex.Message}");
            continue;
        }

        typeCache[asmName] = typeNames;
    }

    foreach (var t in asm.Elements("type"))
    {
        typeTotal++;
        var typeName = t.Attribute("fullname")?.Value ?? "(no fullname)";
        if (IsTypeMatched(typeNames, typeName))
        {
            continue;
        }

        failures.Add($"类型不存在: {asmName} :: {typeName}");
        Console.WriteLine($"    FAIL 类型不存在    {asmName} :: {typeName}");
    }
}

// ---- 3. 关键条目语义校验（计划点名必须生效的）----
// 仅「名字存在」还不够，这里确认它们确实落在预期程序集上。
var critical = new (string Assembly, string Type, string Note)[]
{
    ("MEFrpLauncherX.Fonts", "*", "字体资源全量保留"),
    ("Avalonia.Base", "Avalonia.Platform.IAssetLoader", "资源加载器（XAML 反射用）"),
    ("Avalonia.Markup", "Avalonia.Markup.*", "绑定/选择器解析"),
    ("Avalonia.Markup.Xaml", "*", "XAML 加载器本体"),
    ("Avalonia.Markup.Xaml", "Avalonia.Markup.Xaml.Converters.BitmapTypeConverter",
        "位图类型转换器（Avalonia 12 新命名空间）"),
    ("MEFrpLauncherX.Core", "*", "Core 全量（含 Converters）"),
    ("MEFrpLauncherX.Plugin", "*", "插件元数据（XAML 反射绑定）"),
    ("Iciclecreek.Avalonia.Terminal", "*", "端子控件"),
};

Console.WriteLine();
Console.WriteLine("-- 关键条目语义校验 --");
foreach (var (asm, type, note) in critical)
{
    var ok = IsMatchedInAssembly(available, typeCache, asm, type);
    Console.WriteLine($"    {(ok ? "OK  " : "FAIL")}  {asm} :: {type}   （{note}）");
    if (!ok)
    {
        failures.Add($"关键条目失效: {asm} :: {type}（{note}）");
    }
}

// ---- 4. 汇总 ----
Console.WriteLine();
Console.WriteLine($"    程序集条目 {assemblyTotal} 条，类型条目 {typeTotal} 条");

if (failures.Count == 0)
{
    Console.WriteLine("==> P4 验收通过：TrimmerRoots.xml 全部条目均对应 Avalonia 12 的真实程序集/类型");
    Environment.Exit(0);
    return 0;
}

Console.WriteLine($"==> P4 验收失败：{failures.Count} 条失效");
foreach (var f in failures)
{
    Console.WriteLine($"    - {f}");
}

Console.WriteLine();
Console.WriteLine("提示：失效条目会被 linker 静默忽略（不报错、不警告），请按 Avalonia 12 的真实");
Console.WriteLine("      程序集名与类型全名修正，或直接删除该条目。");

Environment.Exit(1);
return 1;

// ===================== 辅助 =====================

static bool IsMatchedInAssembly(
    Dictionary<string, string> available,
    Dictionary<string, HashSet<string>> cache,
    string asmName,
    string pattern)
{
    if (!available.TryGetValue(asmName, out var path))
    {
        return false;
    }

    if (!cache.TryGetValue(asmName, out var names))
    {
        names = ReadTypeNames(path);
        cache[asmName] = names;
    }

    return IsTypeMatched(names, pattern);
}

static bool IsTypeMatched(HashSet<string> typeNames, string pattern) =>
    pattern == "*"
    || (pattern.EndsWith(".*")
        ? typeNames.Any(n => n.StartsWith(pattern[..^2], StringComparison.Ordinal))
        : typeNames.Contains(pattern));

static HashSet<string> ReadTypeNames(string dllPath)
{
    var set = new HashSet<string>(StringComparer.Ordinal);
    using var fs = File.OpenRead(dllPath);
    using var pe = new PEReader(fs);
    if (!pe.HasMetadata)
    {
        return set;
    }

    var md = pe.GetMetadataReader();
    foreach (var h in md.TypeDefinitions)
    {
        var td = md.GetTypeDefinition(h);
        var ns = md.GetString(td.Namespace);
        var name = md.GetString(td.Name);
        set.Add(string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}");

        foreach (var nh in td.GetNestedTypes())
        {
            var nd = md.GetTypeDefinition(nh);
            set.Add(string.IsNullOrEmpty(ns)
                ? $"{name}+{md.GetString(nd.Name)}"
                : $"{ns}.{name}+{md.GetString(nd.Name)}");
        }
    }

    return set;
}

static string? ResolveNuGetRoot()
{
    // 依次尝试常见位置，避免把机器相关的绝对路径写死在脚本里
    foreach (var candidate in new[]
             {
                 Environment.GetEnvironmentVariable("NUGET_PACKAGES"),
                 Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget",
                     "packages"),
                 @"G:\.Nuget\packages"
             })
    {
        if (!string.IsNullOrEmpty(candidate) && Directory.Exists(candidate))
        {
            return candidate;
        }
    }

    return null;
}

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        if (Directory.Exists(Path.Combine(dir.FullName, "MEFrpLauncherX"))
            && File.Exists(Path.Combine(dir.FullName, "MEFrpLauncherX.sln")))
        {
            return dir.FullName;
        }

        dir = dir.Parent;
    }

    return AppContext.BaseDirectory;
}
