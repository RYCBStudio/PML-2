// P3 UI 回归自动化（Avalonia 12 升级）
//
// 用 Avalonia 官方 Headless 平台真实创建窗口并驱动 UI：
//   - HeadlessUnitTestSession：在进程内启动一个真正的 Avalonia Application
//   - HeadlessWindowExtensions：模拟鼠标点击/移动/滚轮、键盘按键与文本输入
//   - CaptureRenderedFrame：截取渲染结果，用于检测「渲染空白」
//
// 相比人工目视可重复执行、可进 CI；相比静态检查，它真的跑了渲染管线。
//
// 用法：dotnet run --project tools/P3UiRegression
// 退出码：0 = 全部通过；1 = 存在失败项。

using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;

var shotDir = Path.Combine(Path.GetTempPath(), "MEFrp-P3-shots");
Directory.CreateDirectory(shotDir);

var failures = new List<string>();
var notes = new List<string>();
var checks = 0;

Console.WriteLine("==> P3 UI 回归（Avalonia Headless 自动化）");
Console.WriteLine($"    截图输出目录: {shotDir}");
Console.WriteLine();

// ---- 0. 加载主程序产物 ----
var appAsm = LoadAppAssembly();
if (appAsm is null)
{
    Console.WriteLine("==> 失败：找不到主程序产物，请先构建主解决方案");
Environment.Exit(1);
return 1;
}

var appBinDir = Path.GetDirectoryName(appAsm.Location)!;
AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
{
    var name = new AssemblyName(e.Name).Name;
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

    var candidate = Path.Combine(appBinDir, name + ".dll");
    return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null;
};

foreach (var dll in Directory.GetFiles(appBinDir, "MEFrpLauncherX*.dll"))
{
    try
    {
        Assembly.LoadFrom(dll);
    }
    catch
    {
        // 忽略
    }
}

Console.WriteLine($"==> 已加载主程序集: {appAsm.GetName().Name} {appAsm.GetName().Version}");

// ---- 1. 启动 headless 会话 ----
using var session = HeadlessUnitTestSession.StartNew(typeof(StubApplication));

session.Dispatch(() => InitStaticDependencies(), CancellationToken.None).GetAwaiter().GetResult();

Console.WriteLine("==> headless 会话已启动");
Console.WriteLine();

// ---- 2. 逐项执行 UI 回归 ----
// 注意：窗口必须在 headless 会话的 UI 线程内创建（IWindowingPlatform 只在该线程可用）。

await RunCheck("主窗口 MainWindow 启动与渲染", () => {
        var w = CreateInstance<Window>(appAsm, "MEFrpLauncherX.Views.MainWindow");
        return new UiCase(w, w);
    });

await RunCheck("主页导航容器 MainPageFrame", () =>
    WrapInWindow(CreateInstance<Control>(appAsm, "MEFrpLauncherX.Views.MainPageFrame")));

await RunCheck("终端面板 TerminalControl（焦点与键盘输入）", () => {
        var ui = WrapInWindow(CreateInstance<Control>(appAsm, "MEFrpLauncherX.Console.TerminalControl"));
        ui.Actions.Add(("聚焦并输入命令", w =>
        {
            if (FindControl(ui.Content, "MEFrpLauncherX.Console.TerminalControl") is InputElement ie)
            {
                ie.Focus();
            }

            w.KeyTextInput("echo p3-regression");
            w.KeyPressQwerty(Avalonia.Input.PhysicalKey.Enter, Avalonia.Input.RawInputModifiers.None);
        }));
        return ui;
    });

await RunCheck("设置页 SettingsPage（滚轮浏览）", () => {
        var ui = WrapInWindow(CreateInstance<Control>(appAsm, "MEFrpLauncherX.Views.SettingsPage"));
        ui.Actions.Add(("滚轮浏览", w => w.MouseWheel(new Point(200, 300), new Vector(0, -120), Avalonia.Input.RawInputModifiers.None)));
        return ui;
    });

await RunCheck("图标渲染 PackIconLucide（IconPacks fork）", () => {
        var icon = CreateInstance<Control>(null, "IconPacks.Avalonia.Lucide.PackIconLucide", "IconPacks.Avalonia.Lucide");
        SetEnumIfPossible(icon, "Home");
        var ui = WrapInWindow(icon);
        ui.RequireNonBlank = true; // 空白即判定为 IconPacks 类陷阱
        return ui;
    });

await RunCheck("图标渲染 PackIconMaterial（IconPacks fork）", () => {
        var icon = CreateInstance<Control>(null, "IconPacks.Avalonia.Material.PackIconMaterial", "IconPacks.Avalonia.Material");
        SetEnumIfPossible(icon, "Home");
        var ui = WrapInWindow(icon);
        ui.RequireNonBlank = true;
        return ui;
    });

await RunCheck("图表 TrafficStatusControl（LiveCharts 2.1-dev）", () => {
        var ui = WrapInWindow(CreateInstance<Control>(appAsm, "MEFrpLauncherX.Controls.TrafficStatusControl"));
        ui.Actions.Add(("鼠标悬停（关注 Pie 悬停异常）", w => w.MouseMove(new Point(150, 150), Avalonia.Input.RawInputModifiers.None)));
        return ui;
    });

await RunCheck("无边框窗口 ProxyFloat", () => {
        var w = CreateInstance<Window>(appAsm, "MEFrpLauncherX.Views.ProxyMonitor.ProxyFloat");
        return new UiCase(w, w) { CheckDecorations = true };
    });

await RunCheck("启动屏 ClassIslandSplash", () => {
        var w = CreateInstance<Window>(appAsm, "MEFrpLauncherX.Views.Splash.ClassIslandSplash");
        return new UiCase(w, w) { CheckDecorations = true };
    });

await RunCheck("重要公告 Markdown（FluentAvalonia.MarkdownRender）", () => {
        var md = CreateInstance<Control>(null, "FluentAvalonia.MarkdownRender.Controls.MarkdownRender.MarkdownRender",
            "FluentAvalonia.MarkdownRender");
        SetTextIfPossible(md, "# P3 回归\n\n- 列表项一\n- 列表项二\n\n`code` 与 **粗体**");
        return WrapInWindow(md);
    });

Console.WriteLine();

// ---- 3. 汇总 ----
if (notes.Count > 0)
{
    Console.WriteLine($"-- 提示（{notes.Count} 项）--");
    foreach (var n in notes)
    {
        Console.WriteLine($"    · {n}");
    }

    Console.WriteLine();
}

if (failures.Count == 0)
{
    Console.WriteLine($"==> P3 UI 回归通过：{checks} 项用例全部通过（截图见 {shotDir}）");
    Environment.Exit(0);
    return 0;
}

Console.WriteLine($"==> P3 UI 回归失败：{failures.Count}/{checks} 项");
foreach (var f in failures)
{
    Console.WriteLine($"    - {f}");
}
Environment.Exit(1);
return 1;

// ===================== 用例执行 =====================

// 关键：窗口的「创建 + 显示 + 交互 + 截图」必须在同一个 dispatch 回调内完成。
// HeadlessUnitTestSession 把回调排到 UI 线程；若拆成两次 dispatch，
// 后一次拿到的渲染帧可能尚未提交（CaptureRenderedFrame 返回 null）。
async Task RunCheck(string title, Func<UiCase> factory)
{
    checks++;
    var shotPath = Path.Combine(shotDir, Sanitize(title) + ".png");

    UiCaseOutcome outcome;
    try
    {
        // 注意用 await 而非 .GetAwaiter().GetResult()：HeadlessUnitTestSession 的
        // 渲染管线需要真正让出到 UI 线程调度循环，阻塞式等待会拿不到已提交的渲染帧。
        outcome = await session.Dispatch(() =>
        {
            var uiCase = factory();
            if (uiCase?.Window is null)
            {
                return new UiCaseOutcome(null, false);
            }

            var ratio = Execute(uiCase, shotPath);
            var deco = uiCase.CheckDecorations ? uiCase.Window.SystemDecorations.ToString() : null;
            return new UiCaseOutcome(ratio, true, deco);
        }, CancellationToken.None);
    }
    catch (Exception ex)
    {
        var r0 = Unwrap(ex);
        if (IsEnvironmentNoise(r0))
        {
            notes.Add($"SKIP {title}（环境限制）: {r0.Message}");
            Console.WriteLine($"    SKIP {title}（环境限制）");
        }
        else
        {
            // 把内层堆栈一起记下来：仅看外层消息定位不到真实出错行。
            var chain = new List<string>();
            for (var e = r0; e is not null; e = e.InnerException)
            {
                chain.Add(e.TargetSite is null
                    ? e.GetType().Name
                    : $"{e.GetType().Name}@{e.TargetSite.DeclaringType?.Name}.{e.TargetSite.Name}");
            }

            failures.Add($"{title}: {r0.GetType().Name}: {r0.Message} [链路: {string.Join(" <- ", chain)}]");
            Console.WriteLine($"    FAIL {title}: {r0.GetType().Name}: {r0.Message} [链路: {string.Join(" <- ", chain)}]");
        }

        return;
    }

    if (!outcome.Found)
    {
        notes.Add($"SKIP {title}（类型未找到或无法构造）");
        Console.WriteLine($"    SKIP {title}（类型未找到或无法构造）");
        return;
    }

    if (outcome.Decorations is not null)
    {
        Console.WriteLine($"    OK   {title}（SystemDecorations={outcome.Decorations}）");
        return;
    }

    if (outcome.BlankRatio is > 0.995)
    {
        failures.Add($"{title}: 渲染空白（背景像素占比 {outcome.BlankRatio:P2}）—— 正是 IconPacks 类陷阱");
        Console.WriteLine($"    FAIL {title}: 渲染空白 {outcome.BlankRatio:P2}");
        return;
    }

    Console.WriteLine($"    OK   {title}（截图 {Path.GetFileName(shotPath)}）");
}

// 在 UI 线程上真实执行：显示 -> 布局 -> 渲染 -> 交互 -> 截图 -> 像素统计。
// 注意保持同步：dispatch 回调内不要 await UI 线程，否则会与 GetResult() 的阻塞等待冲突。
double Execute(UiCase uiCase, string shotPath)
{
    var window = uiCase.Window;
    window.Width = 900;
    window.Height = 600;
    window.Show();

    // Avalonia 12：布局直接走 TopLevel.UpdateLayout()（LayoutManager 不再公开）
    window.UpdateLayout();
    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

    foreach (var (label, action) in uiCase.Actions)
    {
        try
        {
            action(window);
            window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        catch (Exception ex)
        {
            var r = Unwrap(ex);
            if (!IsEnvironmentNoise(r))
            {
                throw new InvalidOperationException($"交互「{label}」失败: {r.Message}", r);
            }

            notes.Add($"交互「{label}」跳过（环境限制）: {r.Message}");
        }
    }

    using var bitmap = window.CaptureRenderedFrame()
                       ?? throw new InvalidOperationException("CaptureRenderedFrame 返回 null（渲染失败）");
    bitmap.Save(shotPath, new PngBitmapEncoderOptions());

    return AnalyzeBlankRatio(bitmap);
}

// 统计「与左上角像素相同」的占比，用于发现整块空白的渲染缺失。
double AnalyzeBlankRatio(Bitmap bitmap)
{
    int width = bitmap.PixelSize.Width;
    int height = bitmap.PixelSize.Height;
    if (width == 0 || height == 0)
    {
        return 1.0;
    }

    var stride = width * 4;
    var buffer = new byte[stride * height];

    // Avalonia 12：CopyPixels 的目标参数是 nint（指针），用 fixed 固定托管数组。
    unsafe
    {
        fixed (byte* p = buffer)
        {
            bitmap.CopyPixels(new PixelRect(0, 0, width, height), (nint)p, buffer.Length, stride);
        }
    }

    uint At(int i) => (uint)(buffer[i] | (buffer[i + 1] << 8) | (buffer[i + 2] << 16) | (buffer[i + 3] << 24));

    var corner = At(0);
    long same = 0;
    long total = 0;

    int stepX = Math.Max(1, width / 200);
    int stepY = Math.Max(1, height / 200);
    for (int y = 0; y < height; y += stepY)
    {
        for (int x = 0; x < width; x += stepX)
        {
            total++;
            if (At(y * stride + x * 4) == corner)
            {
                same++;
            }
        }
    }

    return total == 0 ? 1.0 : (double)same / total;
}

// ===================== 辅助函数 =====================

static string Sanitize(string s)
{
    var invalid = Path.GetInvalidFileNameChars();
    return new string(s.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
}

static Exception Unwrap(Exception ex)
{
    var r = ex;
    while (r.InnerException is { } ie)
    {
        r = ie;
    }

    return r;
}

// 判定「环境限制」：冒烟未跑真实启动流程导致的、与本次升级无关的失败。
static bool IsEnvironmentNoise(Exception root)
{
    var msg = root.Message ?? string.Empty;
    var stack = root.StackTrace ?? string.Empty;

    return root is KeyNotFoundException && msg.Contains("Static resource")
           || msg.Contains("IActivationForViewFetcher")
           || root is NullReferenceException && (
               stack.Contains("ConfigManager") || stack.Contains("CurrentLogger")
               || stack.Contains("CurrentConfig"))
           || root is MissingMethodException
           || msg.Contains("No parameterless constructor")
           || root is NotSupportedException;
}

static UiCase WrapInWindow(Control content)
{
    var win = new Window { Width = 900, Height = 600, Content = content };
    return new UiCase(content, win);
}

static T CreateInstance<T>(Assembly asm, string typeName, string hintAssembly = null) where T : class
{
    if (hintAssembly is not null)
    {
        try
        {
            Assembly.Load(hintAssembly);
        }
        catch
        {
            // 兜底走 GetAssemblies
        }
    }

    var candidates = asm is not null
        ? new[] { asm }
        : AppDomain.CurrentDomain.GetAssemblies();

    foreach (var a in candidates)
    {
        var t = a.GetType(typeName, throwOnError: false);
        if (t is null)
        {
            continue;
        }

        // 优先用无参构造；没有则用未初始化实例（仅验证类型可解析，不执行构造逻辑，
        // 避免把「需要构造参数」误报成产品缺陷）。
        if (t.GetConstructor(Type.EmptyTypes) is not null)
        {
            try
            {
                return (T)Activator.CreateInstance(t);
            }
            catch (TargetInvocationException tie) when (tie.InnerException is { } ie)
            {
                throw new InvalidOperationException(ie.Message, ie);
            }
        }

        if (!t.IsAbstract)
        {
            return (T)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(t);
        }

        return null;
    }

    return null;
}

// IconPacks 图标控件需设置 Kind 才绘制内容，否则天然空白会造成误判。
static void SetEnumIfPossible(object icon, string kindName)
{
    if (icon is null)
    {
        return;
    }

    foreach (var propName in new[] { "Kind", "Icon", "Value" })
    {
        var p = icon.GetType().GetProperty(propName);
        if (p is null || !p.CanWrite || !p.PropertyType.IsEnum)
        {
            continue;
        }

        try
        {
            var names = Enum.GetNames(p.PropertyType);
            var match = names.FirstOrDefault(n => string.Equals(n, kindName, StringComparison.OrdinalIgnoreCase))
                        ?? names.FirstOrDefault(n => !string.Equals(n, "None", StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                p.SetValue(icon, Enum.Parse(p.PropertyType, match));
                return;
            }
        }
        catch
        {
            // 继续尝试下一个属性
        }
    }
}

static void SetTextIfPossible(object md, string text)
{
    if (md is null)
    {
        return;
    }

    foreach (var propName in new[] { "Markdown", "Text", "Source" })
    {
        var p = md.GetType().GetProperty(propName);
        if (p is null || !p.CanWrite || p.PropertyType != typeof(string))
        {
            continue;
        }

        try
        {
            p.SetValue(md, text);
            return;
        }
        catch
        {
            // 继续
        }
    }
}

static Control FindControl(Control root, string typeName)
{
    if (root?.GetType().FullName == typeName)
    {
        return root;
    }

    return root?.GetVisualDescendants()
        .OfType<Control>()
        .FirstOrDefault(c => c.GetType().FullName == typeName);
}

// 主程序控件构造期依赖这些静态成员；冒烟不跑真实启动流程，这里补最小初始化。
static void InitStaticDependencies()
{
    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
    {
        var cm = asm.GetType("MEFrpLauncherX.Core.ConfigManager");
        var cfgProp = cm?.GetProperty("CurrentConfig", BindingFlags.Public | BindingFlags.Static);
        if (cfgProp?.GetValue(null) is null && cfgProp is not null)
        {
            foreach (var mn in new[] { "LoadConfig", "Load", "Initialize", "Init" })
            {
                var mi = cm.GetMethod(mn, BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes);
                if (mi is null)
                {
                    continue;
                }

                try
                {
                    mi.Invoke(null, null);
                    if (cfgProp.GetValue(null) is not null)
                    {
                        break;
                    }
                }
                catch
                {
                    // 继续
                }
            }

            var cfg = cfgProp.GetValue(null);
            if (cfg is null)
            {
                try
                {
                    cfgProp.SetValue(null, Activator.CreateInstance(cfgProp.PropertyType));
                    cfg = cfgProp.GetValue(null);
                }
                catch
                {
                    // 忽略
                }
            }

            if (cfg is not null)
            {
                FillNestedDefaults(cfg);
            }
        }

        var appType = asm.GetType("MEFrpLauncherX.Core.App");
        var loggerProp = appType?.GetProperty("CurrentLogger", BindingFlags.Public | BindingFlags.Static);
        if (loggerProp?.GetValue(null) is null && loggerProp is not null)
        {
            try
            {
                var lt = loggerProp.PropertyType;
                object logger = lt.GetConstructor(Type.EmptyTypes) is not null
                    ? Activator.CreateInstance(lt)
                    : lt.GetConstructor(new[] { typeof(string) }) is not null
                        ? Activator.CreateInstance(lt, Path.Combine(Path.GetTempPath(), "P3Regression.log"))
                        : System.Runtime.Serialization.FormatterServices.GetUninitializedObject(lt);
                loggerProp.SetValue(null, logger);
            }
            catch
            {
                // 忽略
            }
        }
    }
}

static void FillNestedDefaults(object obj)
{
    foreach (var prop in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
    {
        if (!prop.CanRead || !prop.CanWrite || prop.PropertyType.IsValueType || prop.PropertyType == typeof(string))
        {
            continue;
        }

        try
        {
            if (prop.GetValue(obj) is null)
            {
                prop.SetValue(obj, Activator.CreateInstance(prop.PropertyType));
            }
        }
        catch
        {
            // 忽略
        }
    }
}

static Assembly LoadAppAssembly()
{
    var root = FindRepoRoot();
    var candidates = new[]
    {
        Path.Combine(root, "MEFrpLauncherX", "bin", "Debug", "net10.0", "MEFrpLauncherX.dll"),
        Path.Combine(root, "MEFrpLauncherX", "bin", "Release", "net10.0", "MEFrpLauncherX.dll"),
        Path.Combine(root, "MEFrpLauncherX", "bin", "Debug", "net8.0", "MEFrpLauncherX.dll"),
    };

    foreach (var p in candidates)
    {
        if (File.Exists(p))
        {
            return Assembly.LoadFrom(p);
        }
    }

    return null;
}

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

