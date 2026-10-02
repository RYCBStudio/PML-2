// IconPacks + Avalonia 12 兼容性探针（计划 P1.6 交付物 / P3 回归工具）
//
// 目的：验证 IconPacks.Avalonia 的 4 个图标包在 Avalonia 12 下
// 「实例化 -> 加载 ControlTheme -> 解析图标 Path.Data」全链路可用。
//
// 背景：NuGet 上的 IconPacks.Avalonia.* 2.0.0 是针对 Avalonia 11.0.13 编译的，
// 在 Avalonia 12 下会出现「还原通过 + 编译通过 + XAML 加载通过，
// 但套用 ControlTheme 时抛 MissingMethodException、图标静默空白」的问题：
//
//     Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension.ProvideValue(IServiceProvider)
//         Avalonia 11 返回 Avalonia.Data.IBinding
//         Avalonia 12 返回 Avalonia.Data.BindingBase
//
// 因此本探针**必须**真实实例化控件并套用模板，不能只做类型加载。
//
// 运行：dotnet run   （返回码 0 = 通过，1 = 失败）

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using IconPacks.Avalonia.FileIcons;
using IconPacks.Avalonia.Lucide;
using IconPacks.Avalonia.Material;
using IconPacks.Avalonia.SimpleIcons;

// Path 在 Avalonia.Controls.Shapes 与 System.IO（ImplicitUsings）之间二义，显式取别名
using ShapePath = Avalonia.Controls.Shapes.Path;

namespace IconPacksProbe;

internal static class Program
{
    private static int _failures;

    /// <summary>主程序 App.axaml 中实际引用的 4 个主题资源路径（必须保持可加载）。</summary>
    private static readonly string[] ThemeUris =
    {
        "avares://IconPacks.Avalonia.Lucide/Lucide.axaml",
        "avares://IconPacks.Avalonia.FileIcons/FileIcons.axaml",
        "avares://IconPacks.Avalonia.Material/Material.axaml",
        "avares://IconPacks.Avalonia.SimpleIcons/SimpleIcons.axaml",
    };

    public static int Main()
    {
        AppBuilder.Configure<ProbeApp>()
            .UsePlatformDetect()
            .SetupWithoutStarting();

        Console.WriteLine($"Avalonia  : {typeof(Application).Assembly.GetName().Version}");
        Console.WriteLine($"IconPacks : {typeof(PackIconLucide).Assembly.GetName().Version}");
        Console.WriteLine();

        Probe("枚举解析（CreateProxyGuideViewModel 用法：Enum.TryParse<PackIconXxxKind>）", () =>
        {
            var enumType = typeof(PackIconMaterial).Assembly
                .GetType("IconPacks.Avalonia.Material.PackIconMaterialKind")!;
            var names = Enum.GetNames(enumType);
            Console.WriteLine($"    PackIconMaterialKind 项数 = {names.Length}");
            if (names.Length == 0) _failures++;
            if (!Enum.TryParse(enumType, "Home", true, out var v)) _failures++;
            Console.WriteLine($"    TryParse(\"Home\") -> {v}");
        });

        foreach (var t in new[] { typeof(PackIconLucide), typeof(PackIconMaterial), typeof(PackIconFileIcons), typeof(PackIconSimpleIcons) })
        {
            Probe($"实例化 new {t.Name}()", () =>
                Console.WriteLine($"    {Activator.CreateInstance(t)!.GetType().FullName}"));
        }

        foreach (var uri in ThemeUris)
        {
            Probe($"XAML 主题加载 {uri}", () =>
            {
                var loaded = AvaloniaXamlLoader.Load(new Uri(uri));
                Console.WriteLine($"    {loaded?.GetType().FullName ?? "<null>"}");
                if (loaded is not IStyle) _failures++;
            });
        }

        ProbeControlTheme();

        // 记录 Avalonia 12 的实际签名，便于日后比对上游是否已修复。
        Probe("DynamicResourceExtension.ProvideValue 返回类型（Avalonia 12 应为 BindingBase）", () =>
        {
            var t = typeof(Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension);
            var m = t.GetMethod("ProvideValue")!;
            Console.WriteLine($"    {m.ReturnType.FullName}  @ {t.Assembly.GetName().Name} {t.Assembly.GetName().Version}");
            if (m.ReturnType.FullName != "Avalonia.Data.BindingBase") _failures++;
        });

        Console.WriteLine();
        Console.WriteLine(_failures == 0
            ? "==> 通过：IconPacks 在 Avalonia 12.1.3 下全链路可用"
            : $"==> 失败项：{_failures}");
        return _failures == 0 ? 0 : 1;
    }

    /// <summary>真正的风险点：套用 ControlTheme 并解析图标几何数据。</summary>
    private static void ProbeControlTheme()
    {
        Probe("ControlTheme 套用 + 图标 Path.Data 解析", () =>
        {
            foreach (var uri in ThemeUris)
                Application.Current!.Styles.Add((IStyle)AvaloniaXamlLoader.Load(new Uri(uri)));

            var window = new Window { Width = 200, Height = 200 };
            var panel = new StackPanel();
            window.Content = panel;

            var controls = new (string Name, Control Icon)[]
            {
                ($"Lucide.{FirstKind<PackIconLucideKind>()}", new PackIconLucide { Kind = FirstKind<PackIconLucideKind>() }),
                ($"Material.{PackIconMaterialKind.Home}", new PackIconMaterial { Kind = PackIconMaterialKind.Home }),
                ($"FileIcons.{FirstKind<PackIconFileIconsKind>()}", new PackIconFileIcons { Kind = FirstKind<PackIconFileIconsKind>() }),
                ($"SimpleIcons.{FirstKind<PackIconSimpleIconsKind>()}", new PackIconSimpleIcons { Kind = FirstKind<PackIconSimpleIconsKind>() }),
            };

            foreach (var (_, icon) in controls)
            {
                icon.Width = 24;
                icon.Height = 24;
                panel.Children.Add(icon);
            }

            window.Show();
            window.Measure(new Size(200, 200));
            window.Arrange(new Rect(0, 0, 200, 200));

            foreach (var (name, icon) in controls)
            {
                var path = icon.GetVisualDescendants().OfType<ShapePath>().FirstOrDefault();
                Console.WriteLine($"    {name}: Path.Data={(path?.Data is null ? "<null>" : "ok")}");
                if (path?.Data is null) _failures++;
            }

            window.Close();
        });
    }

    /// <summary>取第一个非 None 的枚举值（None 无对应图标路径，属正常）。</summary>
    private static T FirstKind<T>() where T : struct, Enum
        => Enum.GetValues<T>().First(v => !string.Equals(v.ToString(), "None", StringComparison.Ordinal));

    private static void Probe(string title, Action action)
    {
        try
        {
            action();
            Console.WriteLine($"[ OK ] {title}");
        }
        catch (Exception ex)
        {
            _failures++;
            Console.WriteLine($"[FAIL] {title}");
            Console.WriteLine($"       {ex.GetType().Name}: {ex.Message}");
        }
    }
}

internal sealed class ProbeApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
}
