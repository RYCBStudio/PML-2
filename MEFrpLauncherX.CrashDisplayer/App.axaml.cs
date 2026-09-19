using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using MEFrpLauncherX.CrashDisplayer.ViewModels;
using MainWindow = MEFrpLauncherX.CrashDisplayer.Views.MainWindow;

namespace MEFrpLauncherX.CrashDisplayer;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            // 命令行参数防御：主程序崩溃现场可能不完整，任何缺失/损坏的参数都必须能降级展示，
            // 崩溃报告器自身绝不能再崩溃。
            var args = desktop.Args ?? [];
            var exArg = args.Length > 0 ? args[0] : "";
            var logArg = args.Length > 1 ? args[1] : "";
            MainViewModel viewModel;
            try
            {
                viewModel = new MainViewModel(exArg, logArg);
            }
            catch
            {
                viewModel = new MainViewModel();
            }

            desktop.MainWindow = new MainWindow
            {
                Title = CrashStrings.CrashTitle,
                DataContext = viewModel
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}