using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using static MEFrpLauncherX.Core.MEFIntergrated.InfoClasses;
using Color = Avalonia.Media.Color;
using Colors = Avalonia.Media.Colors;

namespace MEFrpLauncherX.Core;

public class ConverterBase : IValueConverter
{
    public static ConverterBase Instance
    {
        get;
    } = new();

    public virtual object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value;
    }

    public virtual object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value;
    }
}

public class ProgressToVisibilityConverter : IValueConverter
{
    public static ProgressToVisibilityConverter Instance
    {
        get;
    } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return double.TryParse(value?.ToString(), out var progress) && progress is > 0 and < 100;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class StatusNumToTextConverter : IValueConverter
{
    public static StatusNumToTextConverter Instance => new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int status)
        {
            return null;
        }

        return status switch
        {
            -1 => "网络服务错误/不可用",
            0 => "系统正常运行",
            1 => "系统服务降级",
            2 => "系统服务故障",
            _ => "网络服务错误"
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class StatusNumToVisibilityConverter : IValueConverter
{
    public static StatusNumToVisibilityConverter Instance => new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int status)
        {
            return false;
        }

        return status != -2;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class StatusNumToSeveritryConverter : IValueConverter
{
    public static StatusNumToSeveritryConverter Instance => new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int status)
        {
            return false;
        }

        return status switch
        {
            0 => InfoBarSeverity.Success,
            1 => InfoBarSeverity.Warning,
            _ => InfoBarSeverity.Error,
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class StatusNumToSuccessBackgroundConverter : IValueConverter
{
    public static StatusNumToSuccessBackgroundConverter Instance => new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int status)
        {
            return false;
        }

        return status == 0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class StatusNumToWarningBackgroundConverter : IValueConverter
{
    public static StatusNumToWarningBackgroundConverter Instance => new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int status)
        {
            return false;
        }

        return status == 1;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class StatusNumToErrorBackgroundConverter : IValueConverter
{
    public static StatusNumToErrorBackgroundConverter Instance => new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int status)
        {
            return false;
        }

        return status == 2;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class StatusNumToFatalBackgroundConverter : IValueConverter
{
    public static StatusNumToFatalBackgroundConverter Instance => new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int status)
        {
            return false;
        }

        return status == -1;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class ThemeToBackgroundConverter : IValueConverter
{
    public static ThemeToBackgroundConverter Instance => new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? Color.FromArgb(63, 0, 0, 0) : Color.FromArgb(63, 255, 255, 255);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class ObjectEqualityConverter : IValueConverter
{
    public static ObjectEqualityConverter Instance
    {
        get;
    } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Equals(value, parameter);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (bool)value ? parameter : null;
    }
}

public class SelectedToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var trueBrush = Application.Current.FindResource("CardBackgroundFillColorDefaultBrush") as Brush;

        var falseBrush = Application.Current.FindResource("CardBackgroundFillColorSecondaryBrush") as Brush;

        return value is bool and true ? trueBrush : falseBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class Int32ToProgressBarSuccessConvertor : IValueConverter
{
    public static Int32ToProgressBarSuccessConvertor Instance
    {
        get;
    } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return int.TryParse(value?.ToString(), out var val) && val < 60;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class Int32ToProgressBarWarnConvertor : IValueConverter
{
    public static Int32ToProgressBarWarnConvertor Instance
    {
        get;
    } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return int.TryParse(value?.ToString(), out var val) && val >= 60;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class Int32ToProgressBarDangerConvertor : IValueConverter
{
    public static Int32ToProgressBarDangerConvertor Instance
    {
        get;
    } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return int.TryParse(value?.ToString(), out var val) && val >= 85;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

// 视图模板选择器
public class ViewModeTemplateSelector : IDataTemplate
{
    public DataTemplate? GridTemplate
    {
        get;
        set;
    }

    public DataTemplate? ListTemplate
    {
        get;
        set;
    }

    public Control Build(object param)
    {
        if (param is Control { Parent: ItemsControl })
        {
            //var viewModel = itemsControl.DataContext as ProxyViewModel;
            //return viewModel?.CurrentViewMode == ViewMode.List ? ListTemplate : GridTemplate;
        }

        return (GridTemplate ?? throw new InvalidOperationException()).Build(param);
    }

    public bool Match(object data)
    {
        return data is Control;
    }
}

public class NodeNotFoundToBoolConverter : IValueConverter
{
    public static NodeNotFoundToBoolConverter Instance
    {
        get;
    } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string nodeNotFound)
        {
            return nodeNotFound.Contains("不存在");
        }

        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class NodeNotFoundToBoolConverterReverse : IValueConverter
{
    public static NodeNotFoundToBoolConverterReverse Instance
    {
        get;
    } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string nodeNotFound)
        {
            return !nodeNotFound.Contains("不存在");
        }

        return true;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

// 枚举到布尔值转换器
public class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.Equals(parameter);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.Equals(true) == true ? parameter : null;
    }
}

public class BoolToBadgeHeaderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (bool)value ? "负载过高" : null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class DisabledToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (bool)value ? new SolidColorBrush(Color.FromRgb(242, 189, 86)) : Brushes.ForestGreen;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class DisabledToSpanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (bool)value ? 1 : 2;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // 确保处理的是布尔值，而不是转换后的文本
        if (value is bool isOnline)
        {
            return isOnline ? Brushes.ForestGreen : Brushes.Red;
        }

        return Brushes.Gray; // 默认值
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BannedToBrushConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // 确保处理的是布尔值，而不是转换后的文本
        if (value is bool isBanned)
        {
            return isBanned ? Brushes.Red : null;
        }

        return null; // 默认值
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class ProxyTypeToBoolConverter : IValueConverter
{
    public static ProxyTypeToBoolConverter Instance
    {
        get;
    } = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string proxyType && (proxyType.ToLower().Equals("http") || proxyType.ToLower().Equals("https"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class ProxyTypeToBoolReverseConverter : IValueConverter
{
    public static ProxyTypeToBoolReverseConverter Instance
    {
        get;
    } = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is string proxyType &&
               !(proxyType.ToLower().Equals("http") || proxyType.ToLower().Equals("https"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class LowerCaseToUpperCaseConverter : ConverterBase
{
    public override object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString()?.ToUpper();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class BoolToStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isOnline)
        {
            return isOnline ? "在线" : "离线";
        }

        return "未知";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// 选中状态转边框颜色
public class SelectedToBorderBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (bool)value ? new SolidColorBrush(Colors.DodgerBlue) : new SolidColorBrush(Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class MultiBindingConverter : IMultiValueConverter
{
    public static MultiBindingConverter Instance
    {
        get;
    } = new();

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        return values;
    }
}


public class BytesToReadableConverterEx : IValueConverter
{
    public static BytesToReadableConverterEx Instance => new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is long bytesLong)
        {
            return FormatBytes(bytesLong);
        }
        else if (value is int bytesInt)
        {
            return FormatBytes(bytesInt);
        }
        else if (value is double bytesDouble)
        {
            return FormatBytes((long)bytesDouble);
        }

        return "0 B";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private string FormatBytes(long bytes)
    {
        string[] suffixes = ["B/s", "KB/s", "MB/s", "GB/s", "TB/s"];
        var counter = 0;
        decimal number = bytes;

        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }

        return $"{number:n1} {suffixes[counter]}";
    }
}

public class BytesToReadableConverter : IValueConverter
{
    public static BytesToReadableConverter Instance
    {
        get;
    } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is long bytes)
        {
            string[] sizes = ["B", "KB", "MB", "GB", "TB"];
            double len = bytes;
            var order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        return "0 B";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoundToReadableConverter : IValueConverter
{
    public static BoundToReadableConverter Instance
    {
        get;
    } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int bytes)
        {
            string[] sizes = ["Bps", "Kbps", "Mbps", "Gbps", "Tbps"];
            double len = bytes;
            var order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        return value?.ToString() ?? "0 Bps";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class UnixTimeToDateTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int unixTime)
        {
            var dateTime = DateTimeOffset.FromUnixTimeSeconds(unixTime).LocalDateTime;
            return dateTime.ToString("yyyy-MM-dd HH:mm:ss");
        }

        return "N/A";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// 在线状态颜色转换器
public class OnlineStatusColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isOnline)
        {
            return isOnline
                ? new SolidColorBrush(Color.FromRgb(0, 200, 83))
                : // 绿色
                new SolidColorBrush(Color.FromRgb(255, 82, 82)); // 红色
        }

        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// Unix时间戳转换为可读时间（精确到分钟）
public class UnixTimeToReadableConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int unixTime)
        {
            var dateTime = DateTimeOffset.FromUnixTimeSeconds(unixTime).LocalDateTime;
            var timeDiff = DateTime.Now - dateTime;
            var isOnline = parameter is bool and true;

            if (isOnline)
            {
                return $"已在线 {GetTimeAgoString(timeDiff)}";
            }

            return $"{GetTimeAgoString(timeDiff)}前离线";
        }

        return string.Empty;
    }

    private string GetTimeAgoString(TimeSpan timeSpan)
    {
        if (timeSpan.TotalDays >= 1)
        {
            var days = (int)timeSpan.TotalDays;
            var hours = (int)(timeSpan.TotalHours % 24);
            return $"{days}天{hours}小时";
        }

        if (timeSpan.TotalHours >= 1)
        {
            var hours = (int)timeSpan.TotalHours;
            var minutes = (int)(timeSpan.TotalMinutes % 60);
            return $"{hours}小时{minutes}分";
        }

        if (timeSpan.TotalMinutes >= 1)
        {
            return $"{(int)timeSpan.TotalMinutes}分";
        }

        return $"{(int)timeSpan.TotalSeconds}秒";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class OnlineStatusTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is NodeStatus status)
        {
            var duration = TimeSpan.FromSeconds(status.uptime);
            var timeText = FormatTimeSpan(duration);

            return status.isOnline ? $"已在线 {timeText}" : $"{timeText}前离线";
        }

        return string.Empty;
    }

    private string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalDays >= 1)
        {
            var days = (int)ts.TotalDays;
            var hours = ts.Hours;
            return $"{days}天{hours}小时";
        }

        if (ts.TotalHours >= 1)
        {
            var hours = (int)ts.TotalHours;
            var minutes = ts.Minutes;
            return $"{hours}小时{minutes}分";
        }

        if (ts.TotalMinutes >= 1)
        {
            return $"{(int)ts.TotalMinutes}分";
        }

        return $"{(int)ts.TotalSeconds}秒";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// 负载百分比颜色转换器
public class LoadPercentColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int loadPercent)
        {
            if (loadPercent < 80)
            {
                return Application.Current.FindResource("NormalProgressBar");
            }

            if (loadPercent < 100)
            {
                return Application.Current.FindResource("WarningProgressBar");
            }

            return Application.Current.FindResource("DangerProgressBar");
        }

        return Application.Current.FindResource("NormalProgressBar");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToYesNoConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? "是" : "否";
        }

        return "否";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class StatusToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int status)
        {
            switch (status)
            {
                case 0: return "正常";
                case 1: return "封禁";
                case 2: return "流量超限";
                default: return $"未知状态 ({status})";
            }
        }

        return "N/A";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}