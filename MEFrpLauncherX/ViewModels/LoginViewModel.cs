using System;
using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia.Collections;
using Avalonia.Data.Converters;
using MEFrpLauncherX.Core;
using ReactiveUI;

namespace MEFrpLauncherX.ViewModels;

public class LoginViewModel : ViewModelBase
{
    public double Progress
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public AvaloniaList<string> AuthModes
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public int AuthMode
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            ConfigManager.UpdateConfig(x => x.CaptchaMode = AuthMode switch
            {
                0 => "explicit",
                1 => "implicit",
                _ => "explicit"
            });
        }
    }

    public LoginViewModel()
    {
        if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
        {
            AuthModes = ["(推荐) 浏览器验证", "无感验证"];
        }
        else
        {
            AuthModes = ["浏览器验证", "(推荐) 无感验证"];
        }

        AuthMode = ConfigManager.CurrentConfig.CaptchaMode.ToLower() switch
        {
            "implicit" => 1,
            "explicit" => 0
        };
    }
}

public class ProgressToTextConverter : IValueConverter
{
    public static ProgressToTextConverter Instance
    {
        get;
    } = new();

    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        var progress = value as double?;
        return progress.HasValue ? progress > 0 ? $"登录中... {progress:F2}%" : "登录" : "登录";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}