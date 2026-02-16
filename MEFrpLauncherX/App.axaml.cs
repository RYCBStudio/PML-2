using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using FluentAvalonia.Styling;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Views;

namespace MEFrpLauncherX;

public class App : Application
{
    public static string Version = "2.3.0-preview2";

    public static string MEFrpVersion
    {
        get;
        set;
    } = "0.67.0_20260214_7d549bc1";

    public static string Codename = "Fluorine";
    public static SplashScreen splash;
    public static LogUtil LogService;

    public static FluentAvaloniaTheme? FATheme;

    public static IClassicDesktopStyleApplicationLifetime Desktop
    {
        get;
        private set;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        // splash = new SplashScreen();
        // splash.Show();
        Core.App.Initialize();
    }

    public override void RegisterServices()
    {
        base.RegisterServices();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            FATheme = Current?.Styles[4] as FluentAvaloniaTheme;
            var currentTheme = ConfigManager.CurrentConfig.Theme.ToLower() switch
            {
                "dark" => ThemeVariant.Dark,
                "light" => ThemeVariant.Light,
                _ => ThemeVariant.Default
            };
            var ac = ConfigManager.CurrentConfig.AccentColor;
            if (!ac.IsNullOrEmpty())
            {
                Current.Styles.OfType<FluentAvaloniaTheme>().First().CustomAccentColor =
                    Color.TryParse(ConfigManager.CurrentConfig.AccentColor, out var color) ? color : null;
            }

            Current?.RequestedThemeVariant = currentTheme;
            var mainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
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