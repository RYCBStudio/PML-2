using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using MEFrpLauncherX.Core;
using ReactiveUI;

namespace MEFrpLauncherX.ViewModels.Splash;

public class ClassIslandSplashViewModel : ViewModelBase
{
    public double Progress
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string? ProgressText
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ClassIslandSplashViewModel()
    {
        if (Design.IsDesignMode)
        {
            // 设计时模拟
            Task.Run(async () =>
            {
                for (int i = 0; i <= 100; i++)
                {
                    UpdateProgress(i, $"Loading... {i}%");
                    await Task.Delay(50);
                }
            });
        }
    }

    /// <summary>
    /// 更新进度（外部调用）
    /// </summary>
    public void UpdateProgress(double newProgress, string? statusText = null)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Progress = Math.Clamp(newProgress, 0, 100);
            ProgressText = !string.IsNullOrEmpty(statusText)
                ? statusText
                : $"Loading... {Progress:F0}%";
        });
    }
}