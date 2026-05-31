using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MEFrpLauncherX.Services;
using MEFrpLauncherX.ViewModels.Splash;

namespace MEFrpLauncherX.Views.Splash;

public partial class ClassIslandSplash : Window, ISplashService
{
    private ClassIslandSplashViewModel ViewModel
    {
        get;
    }

    public ClassIslandSplash()
    {
        InitializeComponent();
        if (!Design.IsDesignMode)
        {
            ViewModel = new ClassIslandSplashViewModel();
            this.DataContext = ViewModel;
        }
    }

    public void UpdateProgress(double progress, string progressText)
    {
        ViewModel.UpdateProgress(progress, progressText);
    }
    
    void ISplashService.Close()
    {
        this.Close();
    }
    
    void ISplashService.Show()
    {
        this.Show();
    }
}