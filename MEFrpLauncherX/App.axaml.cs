using System.Linq;
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
using MEFrpLauncherX.Views;

namespace MEFrpLauncherX;

public class App : Application
{
    public const string Codename = "Fluorine";
    public static SplashScreen splash;

    public static FluentAvaloniaTheme? FATheme;

    public static IClassicDesktopStyleApplicationLifetime Desktop
    {
        get;
        private set;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        Core.App.Initialize();
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

            Current.Resources["GlobalFontFamily"] = new FontFamily("Exo");
            Current.Resources["ContentControlThemeFontFamily"] = new FontFamily("Exo");

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