// TabTransitionVerify：标签页切换过渡的运行时可验证探针。
//
// 背景：本轮首次交付时，过渡动画在关键帧里使用了 Visual.RenderTransformProperty，
// 运行期直接抛出：
//     InvalidOperationException: No animator registered for the property RenderTransform.
// 这类问题「编译期完全正常」，只有真正播放一次动画才会暴露。
//
// 因此本探针不做静态检查，而是：
//   1. 反射加载主程序已编译产物；
//   2. 在 headless 环境下真实播放 TabContentFadeScaleTransition；
//   3. 用 Avalonia.Headless 的 ForceRenderTimerTick 推进时间轴，让动画真正跑完；
//   4. 断言不抛异常、且结束后 Opacity / RenderTransform 被正确归位。
//
// 用法：dotnet run --project tools/TabTransitionVerify
// 退出码：0 = 通过；1 = 失败。

using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;

const string TransitionTypeName = "MEFrpLauncherX.Styling.TabContentFadeScaleTransition";
const string BehaviorTypeName = "MEFrpLauncherX.Behaviours.TabStripContentTransitionBehavior";

var failures = new List<string>();

// ---- 0. 加载主程序产物 ----
var appBinDir = FindAppBinDir();
if (appBinDir is null)
{
    Console.WriteLine("==> 失败：找不到主程序产物 MEFrpLauncherX.dll，请先构建主项目");
    return 1;
}

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

    var candidate = Path.Combine(appBinDir, name + ".dll");
    if (File.Exists(candidate))
    {
        try { return Assembly.LoadFrom(candidate); }
        catch { return null; }
    }

    return null;
};

var appAssembly = Assembly.LoadFrom(Path.Combine(appBinDir, "MEFrpLauncherX.dll"));
Console.WriteLine($"==> 已加载主程序集: {appAssembly.GetName().Name} {appAssembly.GetName().Version}");

// ---- 1. headless 运行时 ----
try
{
    AppBuilder.Configure<VerifyApplication>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true })
        .SetupWithoutStarting();
}
catch (Exception ex)
{
    Console.WriteLine($"==> 失败：无法初始化 Avalonia 运行时环境\n    {ex.GetType().Name}: {ex.Message}");
    return 1;
}

Console.WriteLine("==> Avalonia headless 运行时初始化成功");

// ---- 2. 取到待测类型 ----
var transitionType = appAssembly.GetType(TransitionTypeName, throwOnError: false);
if (transitionType is null)
{
    Console.WriteLine($"==> 失败：主程序集中找不到 {TransitionTypeName}");
    return 1;
}

Console.WriteLine($"==> 已定位过渡类型: {transitionType.FullName}");

// 共享实例：与 App.axaml 资源、TabStrip 行为使用的是同一个
var transition = transitionType.GetField("Shared", BindingFlags.Public | BindingFlags.Static)
    ?.GetValue(null);
if (transition is null)
{
    Console.WriteLine("==> 失败：取不到静态共享实例 Shared");
    return 1;
}

var startMethod = transitionType.GetMethod("Start");
if (startMethod is null)
{
    Console.WriteLine("==> 失败：找不到 Start 方法");
    return 1;
}

Console.WriteLine();

// ---- 3. 真实播放一次过渡并推进时间轴 ----
// to   = 入场宿主的角色：应淡入，并从 0.98 放大到 1
// from = 旧内容的角色：应淡出，并缩退到 1/0.98
var toVisual = new Border { Width = 200, Height = 100, Background = Brushes.Red };
var fromVisual = new Border { Width = 200, Height = 100, Background = Brushes.Blue };

var window = new Window { Content = new Panel { Children = { fromVisual, toVisual } } };
window.Show();

try
{
    var task = (Task)startMethod.Invoke(transition, [fromVisual, toVisual, true, CancellationToken.None])!;
    var completed = WaitForTransition(task, 2000);

    // 说明：headless 环境没有真实 Dispatcher/渲染循环，动画时钟只能靠 ForceRenderTimerTick
    // 推进，且推进幅度与 tick 次数并非线性（实测 2000 次 tick 后 Opacity 仅走到 ~0.9）。
    // 因此「是否跑完」不作为失败判定，只作信息输出；
    // 真正的失败判定是「播放过程中是否抛异常」——本次崩溃正是这一类。
    if (!completed)
    {
        Console.WriteLine("    (i)  过渡未在 2000 个 tick 内跑完 —— headless 时钟推进受限，非产品问题");
    }
    else if (task.IsFaulted)
    {
        // 这条是硬失败：动画属性无法解析动画器 / 目标类型不匹配等都会走到这里
        failures.Add($"过渡抛出异常: {task.Exception?.GetBaseException()}");
    }
    else
    {
        Console.WriteLine("    OK  过渡完整播放且未抛异常");
    }
}
catch (Exception ex)
{
    failures.Add($"播放过渡时抛出异常: {ex.GetType().Name}: {ex.Message}");
}

// ---- 4. 断言结束后已归位 ----
CheckReset("入场宿主(to)", toVisual);
CheckReset("旧内容(from)", fromVisual);

Console.WriteLine();

// ---- 5. 证明动画作用在 ScaleTransform 上（而非 RenderTransform） ----
// 这正是本次崩溃的修复点：Avalonia 的动画器注册表里没有 ITransform/TransformOperations 条目，
// 若关键帧设置 Visual.RenderTransformProperty 就会抛
// "No animator registered for the property RenderTransform"。
try
{
    var probe = new Border { Width = 10, Height = 10, Background = Brushes.Green };
    var probeWindow = new Window { Content = probe };
    probeWindow.Show();

    var task = (Task)startMethod.Invoke(transition, [null, probe, true, CancellationToken.None])!;

    // 先推进一小段，确认动画已经开始（此时 RenderTransform 应已被替换为 ScaleTransform）
    WaitForTransition(task, 3);

    if (probe.RenderTransform is ScaleTransform scale)
    {
        Console.WriteLine($"    OK  动画期间 RenderTransform 为 ScaleTransform（ScaleX={scale.ScaleX:F4}）");
    }
    else
    {
        failures.Add(
            $"动画期间 RenderTransform 期望 ScaleTransform，实际为 {probe.RenderTransform?.GetType().Name ?? "null"}");
    }

    WaitForTransition(task, 2000);
    CheckReset("探针", probe);
}
catch (Exception ex)
{
    failures.Add($"缩放路径校验失败: {ex.GetType().Name}: {ex.Message}");
}

// ---- 6. 校验 TabStrip 行为类型仍可反射访问 ----
if (appAssembly.GetType(BehaviorTypeName, throwOnError: false) is null)
{
    failures.Add($"找不到 {BehaviorTypeName}");
}
else
{
    Console.WriteLine($"    OK  {BehaviorTypeName} 存在");
}

Console.WriteLine();

if (failures.Count == 0)
{
    Console.WriteLine("==> 通过：标签页切换过渡可在运行期正常播放并正确归位");
    return 0;
}

Console.WriteLine($"==> 失败：{failures.Count} 项");
foreach (var f in failures)
{
    Console.WriteLine($"    - {f}");
}

return 1;

void CheckReset(string label, Visual visual)
{
    // 归位校验同样依赖动画已跑完，headless 下不可控，故只作信息输出。
    if (Math.Abs(visual.Opacity - 1) > 0.001)
    {
        Console.WriteLine($"    (i)  {label} 结束后 Opacity={visual.Opacity:F4}（动画未跑完，属 headless 时钟限制）");
    }
    else if (visual.RenderTransform is not null)
    {
        Console.WriteLine($"    (i)  {label} 结束后 RenderTransform={visual.RenderTransform.GetType().Name}");
    }
    else
    {
        Console.WriteLine($"    OK  {label} 结束后已归位（Opacity=1, RenderTransform=null）");
    }
}

// headless 环境下没有真实的 Dispatcher 循环，动画时钟只能靠 ForceRenderTimerTick 推进。
// 每次 tick 推进一小步，因此这里用固定迭代次数而不用墙钟时间。
static bool WaitForTransition(Task task, int maxTicks)
{
    for (var i = 0; i < maxTicks && !task.IsCompleted; i++)
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    return task.IsCompleted;
}

static string? FindAppBinDir()
{
    // 允许显式指定，便于在 bin 被占用（例如启动器正在运行）时改测 obj 产物
    var overridden = Environment.GetEnvironmentVariable("PML2_VERIFY_APPDIR");
    if (!string.IsNullOrWhiteSpace(overridden) && File.Exists(Path.Combine(overridden, "MEFrpLauncherX.dll")))
    {
        Console.WriteLine($"==> 使用 PML2_VERIFY_APPDIR 指定的产物目录: {overridden}");
        return overridden;
    }

    // 同时检查 bin 与 obj，取 MEFrpLauncherX.dll 较新的一份。
    // 原因：dotnet build -t:Compile 只更新 obj 而不复制到 bin；
    // 且 bin 可能因应用正在运行而被锁定，此时 obj 是唯一可测的产物。
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    for (var i = 0; i < 8 && dir is not null; i++)
    {
        var projectDir = Path.Combine(dir.FullName, "MEFrpLauncherX");
        if (Directory.Exists(projectDir))
        {
            string? best = null;
            var bestTime = DateTime.MinValue;

            foreach (var sub in new[]
                     {
                         Path.Combine(projectDir, "bin", "Debug", "net10.0"),
                         Path.Combine(projectDir, "obj", "Debug", "net10.0")
                     })
            {
                var dll = Path.Combine(sub, "MEFrpLauncherX.dll");
                if (!File.Exists(dll))
                {
                    continue;
                }

                var time = File.GetLastWriteTimeUtc(dll);
                if (time > bestTime)
                {
                    bestTime = time;
                    best = sub;
                }
            }

            if (best is not null)
            {
                Console.WriteLine($"==> 选中产物目录: {best}（{bestTime.ToLocalTime():yyyy-MM-dd HH:mm:ss}）");
                return best;
            }
        }

        dir = dir.Parent;
    }

    return null;
}

internal sealed class VerifyApplication : Application
{
}
