// ===================== 类型定义 =====================

using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;

// P3 回归用的 headless 宿主。
//
// HeadlessUnitTestSession 会查找带 [AvaloniaTestApplication] 的类型，
// 调用其静态 BuildAvaloniaApp() 拿到 AppBuilder。关键配置是
// UseHeadlessDrawing = false —— 只有关掉 stub 绘制才会走真实 Skia 渲染，
// CaptureRenderedFrame() 才能拿到非空帧（否则恒为 null）。
public static class P3TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<StubApplication>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                // 真实渲染：这样截图才有内容，能验证 IconPacks 是否绘制
                UseHeadlessDrawing = false,
                ShouldRenderOnUIThread = true,
            });
}


sealed class UiCase
{
    public UiCase(Control content, Window window)
    {
        Content = content;
        Window = window;
    }

    public Control Content { get; }

    public Window Window { get; }

    // 需执行的交互动作（UI 线程、窗口显示后执行）
    public List<(string Label, Action<Window> Action)> Actions { get; } = new();

    // 是否要求渲染结果非空白（用于 IconPacks「渲染缺失」检测）
    public bool RequireNonBlank { get; set; }

    // 是否校验窗口装饰（无边框窗口回归）
    public bool CheckDecorations { get; set; }
}

// 单个用例在 UI 线程上的执行结果（需跨 dispatch 边界传回主线程）。
sealed record UiCaseOutcome(double? BlankRatio, bool Found, string Decorations = null);

// 冒烟用的最小 Application 宿主：不跑真实启动流程（不联网、不建托盘、不弹窗），
// 但必须加载与真实启动一致的主题样式/资源，否则控件拿不到模板
// （表现为 OnApplyTemplate 找不到 PART_xxx、渲染空白等环境性误报）。
internal sealed class StubApplication : Application
{
    public override void Initialize()
    {
        // 关键：必须加载与真实启动一致的主题，否则控件拿不到模板/资源，
        // 会产生大量与升级无关的环境性误报（PART_xxx 找不到、渲染空白、MissingResource）。
        // 这里按 App.axaml 的声明「按同样顺序」重建。
        Styles.Add(new FluentAvalonia.Styling.FluentAvaloniaTheme { PreferUserAccentColor = true });

        AddStyleInclude("avares://MEFrpLauncherX/Styles/_generic.axaml");
        AddStyleInclude("avares://MEFrpLauncherX/Styles/StackPanelIntroAnimation.axaml");
        AddStyleInclude("avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml");
        AddStyleInclude("avares://FluentAvalonia.MarkdownRender/Index.axaml");
        AddStyleInclude("avares://IconPacks.Avalonia.Lucide/Lucide.axaml");
        AddStyleInclude("avares://IconPacks.Avalonia.FileIcons/FileIcons.axaml");
        AddStyleInclude("avares://IconPacks.Avalonia.Material/Material.axaml");
        AddStyleInclude("avares://IconPacks.Avalonia.SimpleIcons/SimpleIcons.axaml");

        // App.axaml 里的字体等资源键（子控件的 {StaticResource GlobalFontFamily} 依赖它们）
        Resources["ControlContentThemeFontSize"] = 13.0;
        Resources["ContentControlThemeFontFamily"] = new Avalonia.Media.FontFamily("avares://MEFrpLauncherX.Fonts/Fonts#HarmonyOS Sans SC");
        Resources["GlobalFontFamily"] = new Avalonia.Media.FontFamily("avares://MEFrpLauncherX.Fonts/Fonts#HarmonyOS Sans SC");
        Resources["IconFont"] = new Avalonia.Media.FontFamily("avares://MEFrpLauncherX.Fonts/Fonts#iconfont");
        Resources["Jbm"] = new Avalonia.Media.FontFamily("avares://MEFrpLauncherX.Fonts/Fonts#Jetbrains Mono");

        // App.axaml 的 3 个 ResourceInclude（它们的路径正是此前 AVLN2000 的失败点）
        AddResourceInclude("avares://MEFrpLauncherX/Styles/TabStripThemes.axaml");
        AddResourceInclude("avares://MEFrpLauncherX/Styles/FAFix.axaml");
        AddResourceInclude("avares://MEFrpLauncherX/Styles/NavigationViewItemPresenterStyles.axaml");
    }

    private void AddStyleInclude(string uri)
    {
        try
        {
            Styles.Add(new StyleInclude(new Uri(uri)) { Source = new Uri(uri) });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    提示：StyleInclude 失败 {uri}（{ex.GetType().Name}）");
        }
    }

    private void AddResourceInclude(string uri)
    {
        try
        {
            Resources.MergedDictionaries.Add(new ResourceInclude(new Uri(uri)) { Source = new Uri(uri) });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    提示：ResourceInclude 失败 {uri}（{ex.GetType().Name}）");
        }
    }
}
