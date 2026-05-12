using System;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
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
using MEFrpLauncherX.Core.Styling;
using MEFrpLauncherX.Views;

namespace MEFrpLauncherX;

public class App : Application
{
    public const string Codename = "Neon";
    public static SplashScreen splash;

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
        Core.App.Initialize();
        AppJsonSerializerContext = new AppJsonSerializerContext(new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
        });
    }

    public override void RegisterServices() => base.RegisterServices();

    public override void OnFrameworkInitializationCompleted()
    {
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
            if (themeManifest != null)
            {
                var ff = new FontFamily(new Uri(Path.Combine(themePath, themeManifest.FontFamily.ToString())),
                    Path.GetFileNameWithoutExtension(themeManifest.FontFamily.ToString()));
                Resources["GlobalFontFamily"] = ff;
                Resources["ContentControlThemeFontFamily"] = ff;
            }
            CONTINUE:
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