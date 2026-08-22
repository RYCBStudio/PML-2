using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using FluentAvalonia.Styling;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Analysis;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Core.Styling;
using MEFrpLauncherX.Core.ViewModels;
using MEFrpLauncherX.Plugin.Services;
using MEFrpLauncherX.Services;
using MEFrpLauncherX.Styling;
using MEFrpLauncherX.Views;

namespace MEFrpLauncherX;

public class App : Application
{
    public const string Codename = "Aluminum";
#pragma warning disable CA2211
    public static ISplashService? SplashService;
#pragma warning restore CA2211

    public static FluentAvaloniaTheme? FATheme
    {
        get;
        private set;
    }

    public static IClassicDesktopStyleApplicationLifetime Desktop
    {
        get;
        private set;
    }

    internal static AppJsonSerializerContext AppJsonSerializerContext;

    public override void Initialize()
    {
        Core.App.Initialize().ConfigureAwait(true);
        // 必须在加载 App.axaml 前设置 Culture，
        // 否则 Styles 中的 {x:Static languages:...} 会以默认(zh-CN)资源被提前固化
        Languages.Culture = ConfigManager.CurrentConfig.Language switch
        {
            "zh-CN" => new CultureInfo("zh-CN"),
            "en-US" => new CultureInfo("en-US"),
            "zh-Hant" => new CultureInfo("zh-Hant"),
            _ => CultureInfo.CurrentCulture
        };
        AvaloniaXamlLoader.Load(this);
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
#if DEBUG
        //Languages.Culture = new CultureInfo("en-US");
        Debug.WriteLine("CurrentCulture: " + CultureInfo.CurrentCulture);
#endif
        // 按配置应用动画程度 (0=关闭 1=精简 2=标准)
        AnimationStyles.Apply(ConfigManager.CurrentConfig.AnimationLevel);
        if (!Design.IsDesignMode)
        {
            AppAnalytics.Setup(
                "https://840a0a2c7a17031d7639b82c602312fc@o4511009461305344.ingest.de.sentry.io/4511009467924560",
                Core.App.Version);

            if (ConfigManager.CurrentConfig.IsTelemetryEnabled)
            {
                AppAnalytics.EnableAnalytics();
            }
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            SplashService?.UpdateProgress(10, Languages.Text_App_LoadingTheme);
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
            SplashService?.UpdateProgress(30, Languages.Text_App_CreatingMainWindow);
            Current?.RequestedThemeVariant = currentTheme;
            var mainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel()
            };
            desktop.MainWindow = mainWindow;
            desktop.Exit += async (sender, args) =>
            {
                try
                {
                    // 26.3 M6b：退出前销毁流量悬浮窗，避免残留置顶窗
                    Views.ProxyMonitor.ProxyFloat.Instance?.Close();

                    // 触发插件事件：应用退出
                    await PluginService.Instance.TriggerAsync("app.exit", new Dictionary<string, object>
                    {
                        ["version"] = Core.App.Version,
                        ["os"] = Environment.OSVersion.Platform.ToString()
                    });
                }
                catch (Exception ex)
                {
                    Core.App.CurrentLogger?.Error(ex, "触发 app.exit 插件事件失败");
                }

                Core.App.CurrentLogger?.Dispose();
            };
            Desktop = desktop;
            Core.App.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}