using System;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using FluentAvalonia.Styling;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Analysis;
using MEFrpLauncherX.Core.Styling;
using MEFrpLauncherX.Core.ViewModels;
using MEFrpLauncherX.Plugin.Services;
using MEFrpLauncherX.Services;
using MEFrpLauncherX.Views;

namespace MEFrpLauncherX;

public class App : Application
{
    public const string Codename = "Sodium";
#pragma warning disable CA2211
    public static ISplashService? SplashService;
#pragma warning restore CA2211

    public static FluentAvaloniaTheme? FATheme;

    public static IClassicDesktopStyleApplicationLifetime Desktop
    {
        get;
        private set;
    }

    internal static AppJsonSerializerContext AppJsonSerializerContext;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        _ = Core.App.Initialize();
        AppJsonSerializerContext = new AppJsonSerializerContext(new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
        });

        // 初始化插件系统
        PluginService.Instance.LoadPlugins();
    }

    public override void RegisterServices() => base.RegisterServices();

    public override void OnFrameworkInitializationCompleted()
    {
        AppAnalytics.Setup(
            "https://840a0a2c7a17031d7639b82c602312fc@o4511009461305344.ingest.de.sentry.io/4511009467924560",
            Core.App.Version);
        if (ConfigManager.CurrentConfig.IsTelemetryEnabled)
        {
            AppAnalytics.EnableAnalytics();
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            SplashService?.UpdateProgress(10, "正在加载主题...");
            FATheme = Current.Styles.OfType<FluentAvaloniaTheme>().First();
            var currentTheme = ConfigManager.CurrentConfig.Theme.ToLower() switch
            {
                "dark" => ThemeVariant.Dark,
                "light" => ThemeVariant.Light,
                _ => ThemeVariant.Default
            };
            LiveCharts.Configure(config => config.UseDefaults());
            var ac = ConfigManager.CurrentConfig.AccentColor;
            if (!ac.IsNullOrEmpty())
            {
                Current.Styles.OfType<FluentAvaloniaTheme>().First().CustomAccentColor =
                    Color.TryParse(ConfigManager.CurrentConfig.AccentColor, out var color) ? color : null;
            }


            string selectedTheme;
            try
            {
                selectedTheme =
                    File.ReadAllText(Path.Combine(Core.App.StartupPath, "Config", "Themes",
                            "selected"))
                        .Trim();
            }
            catch (FileNotFoundException)
            {
                Core.App.CurrentLogger.Log("未找到主题配置文件，跳过主题加载");
                goto CONTINUE;
            }
            catch (Exception ex)
            {
                Core.App.CurrentLogger.Error(ex, "加载主题配置文件时发生错误");
                goto CONTINUE;
            }

            if (selectedTheme.IsNullOrEmpty())
            {
                goto CONTINUE;
            }

            var themePath = Path.Combine(Core.App.StartupPath, "Config", "Themes", selectedTheme);
            var themeManifest =
                ThemeProcessor.LoadTheme(Path.Combine(themePath, "index.json"));
            if (themeManifest is { FontFamily: not null })
            {
                var fontFamily = themeManifest.FontFamily;
                var ff = ThemeProcessor.IsFontFilePath(fontFamily)
                    ? new FontFamily(new Uri(Path.Combine(themePath, fontFamily)),
                        Path.GetFileNameWithoutExtension(fontFamily))
                    : new FontFamily(fontFamily);
                Resources["GlobalFontFamily"] = ff;
                Resources["ContentControlThemeFontFamily"] = ff;
            }

            CONTINUE:
            SplashService?.UpdateProgress(30, "正在创建主窗口");
            Current?.RequestedThemeVariant = currentTheme;
            var mainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel()
            };
            desktop.MainWindow = mainWindow;
            desktop.Exit += (sender, args) =>
            {
                Core.App.CurrentLogger?.Dispose();
            };
            Desktop = desktop;
            Core.App.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}