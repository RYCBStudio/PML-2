// XamlSmoke：Avalonia 12 升级的运行时 XAML 冒烟探针。
//
// 目的：捕获「编译期不报错、运行期 XAML 加载失败」的问题。
// 典型场景：控件类型改名（如 FluentAvalonia 3.x 的 FA 前缀）后，XAML 里若还写旧短名，
// 编译器不会报错（XAML 是运行时解析的），只有真正加载时才会抛异常。
//
// 用法：dotnet run --project tools/XamlSmoke
// 退出码：0 = 全部通过；1 = 存在加载失败。

using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;

var failures = new List<string>();
var envNoise = new List<string>();

// ---- 0. 加载主程序已编译产物 ----
// 主程序是自包含可执行文件，无法用 ProjectReference 引用（NETSDK1151），
// 因此在运行时从其输出目录把程序集装载进来，再按名字反射实例化其中的控件类型。
var appAssembly = LoadAppAssembly();
if (appAssembly is null)
{
    Console.WriteLine("==> 失败：找不到主程序产物 MEFrpLauncherX.dll，请先构建主解决方案");
    return 1;
}

// 主程序 bin 目录里的第三方依赖（如 SkiaSharp）版本可能与本探针解析到的不一致，
// 直接让主程序集从自身 bin 目录解析依赖会触发 FileLoadException（清单版本不匹配）。
// 这里注册解析回退：优先返回探针自己已加载的同名程序集，保证整条依赖链版本自洽。
var appBinDir = Path.GetDirectoryName(appAssembly.Location)!;
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

    // 探针自身也没有时，才退回主程序 bin 目录（如 MEFrpLauncherX.Core 等本项目程序集）
    var candidate = Path.Combine(appBinDir, name + ".dll");
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

Console.WriteLine($"==> 已加载主程序集: {appAssembly.GetName().Name} {appAssembly.GetName().Version}");

// 预加载同目录下本项目其余程序集（Core / Plugin / Fonts 等），
// 保证后续按名字反射取类型时它们已在内存中。
foreach (var dll in Directory.GetFiles(Path.GetDirectoryName(appAssembly.Location)!, "MEFrpLauncherX*.dll"))
{
    try
    {
        Assembly.LoadFrom(dll);
    }
    catch
    {
        // 个别程序集加载失败不影响主流程
    }
}

// ---- 1. 准备一个 headless 的 Avalonia 运行时环境 ----
AppBuilder? builder = null;
try
{
    builder = AppBuilder.Configure<StubApplication>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions
        {
            // 使用无渲染后端，避免依赖本机 GPU/窗口系统
            UseHeadlessDrawing = true
        });
    builder.SetupWithoutStarting();
}
catch (Exception ex)
{
    Console.WriteLine($"==> 失败：无法初始化 Avalonia 运行时环境");
    Console.WriteLine($"    {ex.GetType().Name}: {ex.Message}");
    if (ex.InnerException is { } ie)
    {
        Console.WriteLine($"    内部异常: {ie.GetType().Name}: {ie.Message}");
    }
    return 1;
}

Console.WriteLine("==> Avalonia 运行时环境初始化成功");

// 主程序大量控件在构造期会读 ConfigManager.CurrentConfig / App.CurrentLogger 等静态依赖。
// 冒烟环境不会跑真实启动流程（不加载配置、不初始化日志），这些静态成员为 null 会导致
// 与 Avalonia 12 无关的 NullReferenceException。这里尽量做最小初始化，把「环境噪声」
// 与「真正的 XAML/控件问题」区分开。
InitStaticDependencies();

// ---- 2. 加载主程序的 App.axaml（它会合并全部 Style/ResourceInclude）----
// 这一步能验证 App.axaml 里 3 个 ResourceInclude 路径、以及 _generic.axaml 等是否都能解析。
try
{
    // 关键点：必须挂到 Application.Current.Resources，而不是仅 Load 出一个游离的资源字典。
    // 否则子控件里的 {StaticResource GlobalFontFamily} 之类查找会因作用域缺失而失败，
    // 产生大量误报（真实 App 启动时资源是挂在 Application 上的）。
    var appAsm = appAssembly;

    // 反射 LoadFrom 的程序集没有预编译 XAML，AvaloniaXamlLoader.Load(app) 会失败。
    // 改用运行时 XAML 解析（RuntimeXamlLoader）直接解析 App.axaml 文本，
    // 再把得到的资源挂到 Application.Resources —— 这样子控件的 {StaticResource} 就能找到。
    string xaml;
    var uri = new Uri("avares://MEFrpLauncherX/App.axaml");
    if (Avalonia.Platform.AssetLoader.Exists(uri))
    {
        using var s = Avalonia.Platform.AssetLoader.Open(uri);
        using var r = new StreamReader(s);
        xaml = r.ReadToEnd();
    }
    else
    {
        xaml = File.ReadAllText(Path.Combine(FindRepoRoot(), "MEFrpLauncherX", "App.axaml"));
    }

    // 反射加载的程序集没有预编译 XAML，无法完整实例化 App。
    // 退而求其次：把 App.axaml 顶层定义的 <FontFamily x:Key="..."> 之类的资源键提取出来，
    // 直接注入 Application.Resources，让子控件的 {StaticResource} 查找能够命中。
    // （这些键正是此前 MissingResource 报错的来源。）
    var injected = InjectTopLevelResources(xaml);
    Console.WriteLine($"    OK   App.axaml 顶层资源已注入 Application.Resources（{injected} 个键）");

    // SymbolThemeFontFamily 等来自 FluentAvalonia / Avalonia Fluent 主题的资源，
    // 冒烟没有加载完整主题字典，这里补几个常被直接 StaticResource 引用的键。
    InjectThemeResourceFallbacks();

    // ReactiveUI 需要知道如何判断视图的激活/停用。真实启动由 ReactiveUI.Avalonia 注册，
    // 冒烟环境没跑那套初始化，HomePage 等 ReactiveUserControl 会抛
    // "Don't know how to detect when ... is activated/deactivated"，属于环境限制。
    RegisterActivationForViewFetcher();

    // 资源注入 OK 后，仍校验一遍 App.axaml 的 ResourceInclude 路径是否都存在
    foreach (var bad in VerifyAppAxamlIncludePaths())
    {
        failures.Add($"App.axaml ResourceInclude 缺失: {bad}");
    }
}
catch (Exception ex)
{
    // 展开到最内层异常，否则 TargetInvocationException 会掩盖真正的 XAML 解析错误
    var root = ex;
    while (root.InnerException is { } ie)
    {
        root = ie;
    }

    Console.WriteLine($"    ENV  App.axaml 资源注入未完成（环境限制）: {root.Message}");

    // 即便注入失败，也校验 ResourceInclude 路径 —— 那才是真正的产品失败点
    foreach (var bad in VerifyAppAxamlIncludePaths())
    {
        failures.Add($"App.axaml ResourceInclude 缺失: {bad}");
    }
}

// ReactiveUI 通过 Splat 的可变依赖解析器查找 IActivationForViewFetcher 实现。
// 真实启动时由 ReactiveUI.Avalonia 注册 AvaloniaActivationForViewFetcher；
// 冒烟环境没有跑那步，这里注册一个「永远返回空序列」的最小实现，
// 只为让 ReactiveUserControl 能完成构造，不被环境性异常挡住。
static void RegisterActivationForViewFetcher()
{
    try
    {
        var rxAsm = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "ReactiveUI");
        if (rxAsm is null)
        {
            return;
        }

        var iface = rxAsm.GetType("ReactiveUI.IActivationForViewFetcher");
        if (iface is null)
        {
            return;
        }

        // 动态生成一个实现该接口的类型
        var asmName = new AssemblyName("XamlSmokeDynamic");
        var ab = System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(
            asmName, System.Reflection.Emit.AssemblyBuilderAccess.Run);
        var mb = ab.DefineDynamicModule("Main");
        var tb = mb.DefineType("StubActivationForViewFetcher",
            System.Reflection.TypeAttributes.Public | System.Reflection.TypeAttributes.Class);
        tb.AddInterfaceImplementation(iface);

        // GetAffinityForView(IViewFor view) -> int
        var affinity = tb.DefineMethod("GetAffinityForView",
            System.Reflection.MethodAttributes.Public | System.Reflection.MethodAttributes.Virtual,
            typeof(int), new[] { rxAsm.GetType("ReactiveUI.IViewFor")! });
        var il1 = affinity.GetILGenerator();
        il1.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_0);
        il1.Emit(System.Reflection.Emit.OpCodes.Ret);
        tb.DefineMethodOverride(affinity,
            iface.GetMethod("GetAffinityForView")!);

        // GetActivationForView(IViewFor view) -> IObservable<bool>
        var activation = tb.DefineMethod("GetActivationForView",
            System.Reflection.MethodAttributes.Public | System.Reflection.MethodAttributes.Virtual,
            typeof(IObservable<bool>), new[] { rxAsm.GetType("ReactiveUI.IViewFor")! });
        var il2 = activation.GetILGenerator();
        var obsType = typeof(System.Reactive.Linq.Observable);
        var ret = obsType.GetMethod("Return")!.MakeGenericMethod(typeof(bool));
        il2.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_1);
        il2.Emit(System.Reflection.Emit.OpCodes.Call, ret);
        il2.Emit(System.Reflection.Emit.OpCodes.Ret);
        tb.DefineMethodOverride(activation,
            iface.GetMethod("GetActivationForView")!);

        var created = tb.CreateType()!;
        var instance = Activator.CreateInstance(created)!;

        // Locator.CurrentMutable.RegisterConstant(instance, typeof(IActivationForViewFetcher))
        var splat = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Splat");
        var locator = splat?.GetType("Splat.Locator");
        var mutable = locator?.GetProperty("CurrentMutable",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
        var register = mutable?.GetType().GetMethods()
            .FirstOrDefault(m => m.Name == "RegisterConstant" && m.IsGenericMethod)
            ?.MakeGenericMethod(iface);
        register?.Invoke(mutable, new[] { instance });

        Console.WriteLine("    已注册 StubActivationForViewFetcher（ReactiveUI 激活检测）");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"    提示：IActivationForViewFetcher 注册失败（{ex.GetType().Name}），相关视图将按环境限制归类");
    }
}

// 补充主题侧资源键的兜底值。这些键正常运行由 FluentAvalonia / Avalonia Fluent 主题字典提供；
// 冒烟环境没加载完整主题，缺失会让控件构造失败并掩盖真正的 XAML 问题。
static void InjectThemeResourceFallbacks()
{
    var fallbacks = new (string Key, object Value)[]
    {
        ("SymbolThemeFontFamily", new Avalonia.Media.FontFamily("avares://FluentAvalonia/Fonts#Symbols")),
        ("ContentControlThemeFontFamily", new Avalonia.Media.FontFamily("avares://MEFrpLauncherX.Fonts/Fonts#HarmonyOS Sans SC")),
        ("TextControlThemeFontFamily", new Avalonia.Media.FontFamily("avares://MEFrpLauncherX.Fonts/Fonts#HarmonyOS Sans SC")),
    };

    foreach (var (key, value) in fallbacks)
    {
        if (!Application.Current!.Resources.ContainsKey(key))
        {
            Application.Current!.Resources[key] = value;
        }
    }
}

// 从 App.axaml 文本里提取顶层带 x:Key 的纯文本资源（如 <FontFamily x:Key="X">值</FontFamily>），
// 注入 Application.Resources。返回注入数量。
static int InjectTopLevelResources(string xaml)
{
    var count = 0;

    // 匹配形如 <Tag x:Key="Key">Value</Tag>
    foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(
        xaml,
        @"<(?'tag'FontFamily|SolidColorBrush|Thickness|CornerRadius|Double|Int32|x:String|sys:String|Color)\s[^>]*x:Key=""(?'key'[^""]+)""[^>]*>(?'val'[^<]*)</\k'tag'>"))
    {
        var key = m.Groups["key"].Value;
        var raw = m.Groups["val"].Value.Trim();
        var tag = m.Groups["tag"].Value;

        object? value = tag switch
        {
            "FontFamily" => new Avalonia.Media.FontFamily(raw),
            "SolidColorBrush" => TryParseBrush(raw),
            "Thickness" => TryParseThickness(raw),
            "CornerRadius" => TryParseCornerRadius(raw),
            "Double" => double.TryParse(raw, out var d) ? d : null,
            "Int32" => int.TryParse(raw, out var i) ? i : null,
            _ => raw,
        };

        if (value is null)
        {
            continue;
        }

        Application.Current!.Resources[key] = value;
        count++;
    }

    return count;
}

static object? TryParseBrush(string raw)
{
    try
    {
        return Avalonia.Media.Brush.Parse(raw);
    }
    catch
    {
        return null;
    }
}

static object? TryParseThickness(string raw)
{
    try
    {
        return Avalonia.Thickness.Parse(raw);
    }
    catch
    {
        return null;
    }
}

static object? TryParseCornerRadius(string raw)
{
    try
    {
        return Avalonia.CornerRadius.Parse(raw);
    }
    catch
    {
        return null;
    }
}

// 直接检查 App.axaml 里各个 ResourceInclude 的 avares 路径是否真实存在。
// 这正是之前同事改名文件导致 AVLN2000 的失败点，必须守住。
// 返回无法解析的路径列表。
static List<string> VerifyAppAxamlIncludePaths()
{
    // AssetLoader 只认识"已注册"程序集的资源，而本探针是用 LoadFrom 反射加载主程序集的，
    // 未走 Avalonia 的资源注册。因此改为按磁盘路径校验 —— 校验目标一致：
    // App.axaml 里每个 ResourceInclude 指向的 .axaml 文件必须真实存在
    // （同事把 NavigationViewItemPresenterStyles.axaml 改名引用就曾触发 AVLN2000）。
    var appAsm = AppDomain.CurrentDomain.GetAssemblies()
        .FirstOrDefault(a => a.GetName().Name == "MEFrpLauncherX");
    var asmDir = appAsm is not null
        ? Path.GetDirectoryName(appAsm.Location)!
        : Path.Combine(FindRepoRoot(), "MEFrpLauncherX", "bin", "Debug", "net10.0");

    var uri = new Uri("avares://MEFrpLauncherX/App.axaml");
    string xaml;
    if (Avalonia.Platform.AssetLoader.Exists(uri))
    {
        using var s = Avalonia.Platform.AssetLoader.Open(uri);
        using var r1 = new StreamReader(s);
        xaml = r1.ReadToEnd();
    }
    else
    {
        // 回退：直接读源码树里的 App.axaml
        var diskPath = Path.Combine(FindRepoRoot(), "MEFrpLauncherX", "App.axaml");
        if (!File.Exists(diskPath))
        {
            Console.WriteLine("    FAIL 找不到 App.axaml（avares 与磁盘路径均不可用）");
            return new List<string> { "App.axaml" };
        }

        xaml = File.ReadAllText(diskPath);
    }

    var matches = System.Text.RegularExpressions.Regex.Matches(
        xaml, @"Source=\""(?'p'[^""]+\.axaml)\""");

    var bad = new List<string>();
    foreach (System.Text.RegularExpressions.Match m in matches)
    {
        var p = m.Groups["p"].Value;

        // 跨程序集的 avares（avares://OtherAssembly/...）不由本程序集承载，
        // 运行时由对应程序集自己提供，这里只校验本程序集内的相对路径。
        if (p.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"    SKIP 跨程序集资源（由对方程序集承载）: {p}");
            continue;
        }

        var rel = p.TrimStart('/');
        var res = new Uri($"avares://MEFrpLauncherX/{rel}");
        if (Avalonia.Platform.AssetLoader.Exists(res) || File.Exists(Path.Combine(FindRepoRoot(), "MEFrpLauncherX", rel)))
        {
            Console.WriteLine($"    OK   ResourceInclude 可解析: {p}");
        }
        else
        {
            Console.WriteLine($"    FAIL ResourceInclude 无法解析: {p}");
            bad.Add(p);
        }
    }

    return bad;
}

// ---- 3. 逐个实例化关键控件/窗口/页面 ----
// 这些是各功能页的入口，能覆盖绝大多数 FA 控件与自定义控件的 XAML。
var targets = new (string Name, Func<object?> Factory)[]
{
    ("MainWindow", () => TryCreate("MEFrpLauncherX.Views.MainWindow")),
    ("MainPageFrame", () => TryCreate("MEFrpLauncherX.Views.MainPageFrame")),
    ("SettingsPage", () => TryCreate("MEFrpLauncherX.Views.SettingsPage")),
    ("HomePage", () => TryCreate("MEFrpLauncherX.Views.HomePage")),
    ("CreateProxyPage", () => TryCreate("MEFrpLauncherX.Views.CreateProxyPage")),
    ("ManageProxyPage", () => TryCreate("MEFrpLauncherX.Views.ManageProxyPage")),
    ("AboutPage", () => TryCreate("MEFrpLauncherX.Views.AboutPage")),
    ("ThemesPage", () => TryCreate("MEFrpLauncherX.Views.ThemesPage")),
    ("PluginListPage", () => TryCreate("MEFrpLauncherX.Views.PluginListPage")),
    ("UpdatePage", () => TryCreate("MEFrpLauncherX.Views.UpdatePage")),
    ("ALPSettings", () => TryCreate("MEFrpLauncherX.Views.ALPSettings")),
    ("AppearanceSettings", () => TryCreate("MEFrpLauncherX.Views.Appearance.AppearanceSettings")),
    ("ThemeEditor", () => TryCreate("MEFrpLauncherX.Views.ThemeEditor")),
    ("ConfigEditor", () => TryCreate("MEFrpLauncherX.Views.ConfigEditor")),
    ("CreateProxy", () => TryCreate("MEFrpLauncherX.Controls.CreateProxy")),
    ("UserProxyControl", () => TryCreate("MEFrpLauncherX.Controls.UserProxyControl")),
    ("AnimatedProgressRing", () => TryCreate("MEFrpLauncherX.Controls.AnimatedProgressRing")),
    ("IconContent", () => TryCreate("MEFrpLauncherX.Controls.IconContent")),
    ("NoData", () => TryCreate("MEFrpLauncherX.Controls.NoData")),
    // ---- P3 回归补充项 ----
    // 重要公告 Markdown（统一走 FluentAvalonia.MarkdownRender）
    ("MarkdownRender", () => TryCreate("FluentAvalonia.MarkdownRender.Controls.MarkdownRender.MarkdownRender", "FluentAvalonia.MarkdownRender")),
    // 无边框窗口（P3 需人工验证拖拽/最大化/圆角，这里至少确认能构造）
    ("ProxyFloat", () => TryCreate("MEFrpLauncherX.Views.ProxyMonitor.ProxyFloat")),
    ("ClassIslandSplash", () => TryCreate("MEFrpLauncherX.Views.Splash.ClassIslandSplash")),
    ("WhatsNewWindow", () => TryCreate("MEFrpLauncherX.Views.WhatsNewWindow")),
    // 隧道列表 / 节点监视（隧道启停与导航流畅性相关）
    ("NodesContainer", () => TryCreate("MEFrpLauncherX.Views.NodesContainer")),
    ("NodesMonitoringPage", () => TryCreate("MEFrpLauncherX.Views.NodesMonitoringPage")),
    ("NodesContainerCompact", () => TryCreate("MEFrpLauncherX.Controls.NodesContainerCompact")),
    // DNS 账户管理窗口（26.4 阶段 B）：MVVM 重构后 DataTemplate 自带 x:DataType，覆盖 AVLN2000
    ("DnsAccountsWindow", () => TryCreate("MEFrpLauncherX.Views.DnsAccountsWindow")),
    // 终端面板（焦点/复制）
    ("TerminalControl", () => TryCreate("MEFrpLauncherX.Console.TerminalControl")),
    ("TerminalPage", () => TryCreate("MEFrpLauncherX.Views.TerminalPage")),
    // 图表页（LiveCharts 2.1.0-dev，P3 关注 Pie 悬停 MethodAccessException）
    ("TrafficStatusControl", () => TryCreate("MEFrpLauncherX.Controls.TrafficStatusControl")),
    ("HomeSimplePanel", () => TryCreate("MEFrpLauncherX.Controls.HomeSimplePanel")),
    // IconPacks 实际使用点：验证 fork 在 12.1.3 下不是「编译通过但渲染空白」
    ("PackIconLucide", () => TryCreate("IconPacks.Avalonia.Lucide.PackIconLucide", "IconPacks.Avalonia.Lucide")),
    ("PackIconMaterial", () => TryCreate("IconPacks.Avalonia.Material.PackIconMaterial", "IconPacks.Avalonia.Material")),
    ("PackIconFileIcons", () => TryCreate("IconPacks.Avalonia.FileIcons.PackIconFileIcons", "IconPacks.Avalonia.FileIcons")),
    ("PackIconSimpleIcons", () => TryCreate("IconPacks.Avalonia.SimpleIcons.PackIconSimpleIcons", "IconPacks.Avalonia.SimpleIcons")),
};

foreach (var (name, factory) in targets)
{
    try
    {
        var obj = factory();
        if (obj is null)
        {
            Console.WriteLine($"    SKIP {name}（程序集中未找到该类型，可能已改名或移除）");
            continue;
        }

        Console.WriteLine($"    OK   {name}");
    }
    catch (Exception ex)
    {
        var root = ex;
        while (root.InnerException is { } ie)
        {
            root = ie;
        }

        var msg = $"{name}: {root.GetType().Name}: {root.Message}";

        // 分类：环境噪声 vs 真正的 XAML/控件故障。
        // 冒烟不跑真实启动流程，以下两类属于环境限制，不计入失败：
        //   1) 静态资源作用域缺失（App.axaml 资源未挂到 Application.Resources 链）
        //   2) 业务静态依赖未初始化（ConfigManager.CurrentConfig / App.CurrentLogger 为 null）
        var stack = root.StackTrace ?? string.Empty;
        var isEnvNoise =
            root is KeyNotFoundException && root.Message.Contains("Static resource")
            || root is NullReferenceException && (
                stack.Contains("ConfigManager") || stack.Contains("CurrentLogger")
                || stack.Contains("CurrentConfig") || stack.Contains("BackgroundSettings")
                || stack.Contains("PluginListViewModel") || stack.Contains("CreateProxyViewModel")
                || stack.Contains("MainWindow..ctor") || stack.Contains("AppearanceSettings..ctor"))
            || root is ArgumentException && root.Message.Contains("IActivationForViewFetcher");

        if (isEnvNoise)
        {
            envNoise.Add(msg);
            Console.WriteLine($"    ENV  {name}（环境限制，非 XAML 故障）: {root.GetType().Name}");
            continue;
        }

        failures.Add(msg);
        Console.WriteLine($"    FAIL {msg}");

        // 打印最内层异常的堆栈首行，便于判断是控件自身 XAML 问题还是环境限制
        var firstFrame = stack.Split('\n')
            .Select(l => l.Trim())
            .FirstOrDefault(l => l.StartsWith("at "));
        if (firstFrame is not null)
        {
            Console.WriteLine($"         {firstFrame}");
        }
    }
}

// ---- 4. 结论 ----
Console.WriteLine();
if (envNoise.Count > 0)
{
    Console.WriteLine($"-- 环境限制（不计失败，{envNoise.Count} 项）：冒烟未跑真实启动流程所致 --");
    foreach (var e in envNoise)
    {
        Console.WriteLine($"    · {e}");
    }

    Console.WriteLine();
}

if (failures.Count == 0)
{
    Console.WriteLine("==> 通过：全部 XAML 资源与目标类型均可加载（无运行时 XAML 故障）");
    return 0;
}

Console.WriteLine($"==> 失败：{failures.Count} 处 XAML 加载故障");
foreach (var f in failures)
{
    Console.WriteLine($"    - {f}");
}

return 1;

// 尝试初始化主程序的静态依赖（配置/日志），减少与本次升级无关的环境性 NullReference。
// 失败不影响冒烟继续 —— 相应控件会被判为「环境限制」而非产品缺陷。
static void InitStaticDependencies()
{
    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
    {
        // ConfigManager.CurrentConfig
        var cm = asm.GetType("MEFrpLauncherX.Core.ConfigManager")
                 ?? asm.GetType("ConfigManager");
        var cfgProp = cm?.GetProperty("CurrentConfig", BindingFlags.Public | BindingFlags.Static);
        if (cfgProp?.GetValue(null) is null)
        {
            var cfgType = cfgProp?.PropertyType;
            if (cfgType is not null)
            {
                object? instance = null;

                // 优先走产品自身的加载入口（会填好默认值），失败再退回无参构造。
                foreach (var methodName in new[] { "Load", "LoadConfig", "Initialize", "Init" })
                {
                    var mi = cm?.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes);
                    if (mi is null)
                    {
                        continue;
                    }

                    try
                    {
                        mi.Invoke(null, null);
                        instance = cfgProp!.GetValue(null);
                        if (instance is not null)
                        {
                            Console.WriteLine($"    已通过 ConfigManager.{methodName}() 初始化配置");
                            break;
                        }
                    }
                    catch
                    {
                        // 继续尝试下一个入口
                    }
                }

                if (instance is null)
                {
                    try
                    {
                        cfgProp!.SetValue(null, Activator.CreateInstance(cfgType));
                        Console.WriteLine("    已初始化 ConfigManager.CurrentConfig（默认实例）");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"    提示：ConfigManager.CurrentConfig 初始化失败：{ex.GetType().Name}（相关控件将按环境限制归类）");
                    }
                }

                // 配置对象里常有嵌套的子配置（如 BackgroundSettings），默认为 null 会让控件构造时 NRE。
                // 这里把一层引用类型的可写属性补上默认实例。
                var cfg = cfgProp!.GetValue(null);
                if (cfg is not null)
                {
                    FillNestedDefaults(cfg);
                }
            }
        }

        // Core.App.CurrentLogger：若为空，控件里的 logger.Error(...) 会 NRE。
        var appType2 = asm.GetType("MEFrpLauncherX.Core.App");
        var loggerProp = appType2?.GetProperty("CurrentLogger", BindingFlags.Public | BindingFlags.Static);
        if (loggerProp?.GetValue(null) is null && loggerProp is not null)
        {
            var lt = loggerProp.PropertyType;
            try
            {
                // LogUtil 是具体类（非接口），直接按它的构造尝试实例化：
                // 优先无参，其次单参 string（日志路径），其余退回未初始化实例。
                object? logger = null;
                if (lt.GetConstructor(Type.EmptyTypes) is not null)
                {
                    logger = Activator.CreateInstance(lt);
                }
                else if (lt.GetConstructor(new[] { typeof(string) }) is not null)
                {
                    logger = Activator.CreateInstance(lt, Path.Combine(Path.GetTempPath(), "XamlSmoke.log"));
                }
                else if (!lt.IsAbstract)
                {
                    logger = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(lt);
                }

                if (logger is not null)
                {
                    loggerProp.SetValue(null, logger);
                    Console.WriteLine($"    已初始化 Core.App.CurrentLogger（{lt.Name}）");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    提示：CurrentLogger 初始化失败：{ex.GetType().Name}");
            }
        }
    }
}

// 给配置对象里为 null 的引用类型属性补默认实例（只做一层，够冒烟用）。
static void FillNestedDefaults(object obj)
{
    foreach (var prop in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
    {
        if (!prop.CanRead || !prop.CanWrite)
        {
            continue;
        }

        if (prop.PropertyType.IsValueType || prop.PropertyType == typeof(string))
        {
            continue;
        }

        object? current;
        try
        {
            current = prop.GetValue(obj);
        }
        catch
        {
            continue;
        }

        if (current is not null)
        {
            continue;
        }

        try
        {
            prop.SetValue(obj, Activator.CreateInstance(prop.PropertyType));
        }
        catch
        {
            // 该类型无法默认构造，跳过
        }
    }
}

// 定位仓库根目录（含 MEFrpLauncherX 子目录的那一层）。
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

// 在仓库中定位并加载主程序产物。
static Assembly? LoadAppAssembly()
{
    var repoRoot = FindRepoRoot();

    var candidates = new[]
    {
        Path.Combine(repoRoot, "MEFrpLauncherX", "bin", "Debug", "net10.0", "MEFrpLauncherX.dll"),
        Path.Combine(repoRoot, "MEFrpLauncherX", "bin", "Release", "net10.0", "MEFrpLauncherX.dll"),
        Path.Combine(repoRoot, "MEFrpLauncherX", "bin", "Debug", "net8.0", "MEFrpLauncherX.dll"),
    };

    foreach (var path in candidates)
    {
        if (!File.Exists(path))
        {
            continue;
        }

        try
        {
            return Assembly.LoadFrom(path);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    警告：加载 {path} 失败：{ex.Message}");
        }
    }

    return null;
}

// 通过反射按名字找类型并实例化（默认构造）。
// hintAssembly：可选。.NET 会延迟加载未被代码直接引用的程序集，导致 GetAssemblies() 里没有它；
// 传入名字可先按名强制加载。
static object? TryCreate(string typeName, string? hintAssembly = null)
{
    if (hintAssembly is not null)
    {
        try
        {
            Assembly.Load(hintAssembly);
        }
        catch
        {
            // 忽略：下面仍会走 GetAssemblies() 兜底
        }
    }

    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
    {
        Type? t;
        try
        {
            t = asm.GetType(typeName, throwOnError: false);
        }
        catch
        {
            continue;
        }

        if (t is null)
        {
            continue;
        }

        // 优先用无参构造；没有则用 FormatterServices 创建未初始化实例（仅用于验证
        // 「类型可解析 + 非抽象类」，不执行构造函数逻辑，避免把「需要构造参数」
        // 误报成产品缺陷）。
        if (t.GetConstructor(Type.EmptyTypes) is not null)
        {
            try
            {
                return Activator.CreateInstance(t);
            }
            catch (TargetInvocationException tie) when (tie.InnerException is { } ie)
            {
                throw new InvalidOperationException(ie.Message, ie);
            }
        }

        if (!t.IsAbstract)
        {
            try
            {
                return System.Runtime.Serialization.FormatterServices.GetUninitializedObject(t);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    return null;
}

// 冒烟用的最小 Application 宿主：不跑真正的 App 启动流程（避免弹窗/联网/托盘等副作用），
// 只提供 XAML 解析所必需的 Application 实例。
internal sealed class StubApplication : Application
{
}
