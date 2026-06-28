namespace MEFrpLauncherX.Services;


public interface ISplashService
{
    void Show();

    void UpdateProgress(double progress, string progressText);
    
    void Close();
}