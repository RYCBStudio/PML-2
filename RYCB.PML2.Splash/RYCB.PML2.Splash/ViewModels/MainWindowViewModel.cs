using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RYCB.PML2.Splash.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string? _statusText;

    [ObservableProperty]
    private bool _showProgress;

    /// <summary>主程序管道进度更新（26.3.1 M1，UI 线程调用）</summary>
    public void UpdateProgress(double newProgress, string? message)
    {
        ShowProgress = true;
        Progress = Math.Clamp(newProgress, 0, 100);
        if (!string.IsNullOrEmpty(message))
        {
            StatusText = message;
        }
    }

    /// <summary>主程序报错：显示错误文案（随后由主程序/超时关闭）</summary>
    public void ShowError(string? message)
    {
        ShowProgress = true;
        StatusText = string.IsNullOrEmpty(message) ? "启动失败" : message;
    }
}
