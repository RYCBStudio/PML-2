using System;
using System.Globalization;
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