using Avalonia.Controls;
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
            DataContext = ViewModel;
        }
    }

    public void UpdateProgress(double progress, string progressText)
    {
        ViewModel.UpdateProgress(progress, progressText);
    }
    
    void ISplashService.Close()
    {
        Close();
    }
    
    void ISplashService.Show()
    {
        Show();
    }
}