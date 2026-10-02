using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using RYCB.PML2.Splash.ViewModels;
using RYCB.PML2.Splash.Views;

namespace RYCB.PML2.Splash;

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
            // Avalonia 12 起不再默认注册 DataAnnotationsValidationPlugin
            // （默认仅 IndeiValidationPlugin / ExceptionValidationPlugin），
            // 故原先的「移除 Avalonia 校验插件」逻辑已成死代码，直接删除。
            desktop.MainWindow = new MainWindow(desktop.Args)
            {
                DataContext = new MainWindowViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}