using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace MEFrpLauncherX.Views;

public partial class PublicView : UserControl
{
    public static readonly StyledProperty<int> NodesCountProperty = AvaloniaProperty.Register<PublicView, int>(
        nameof(NodesCount), defaultBindingMode:BindingMode.TwoWay);

    public int NodesCount
    {
        get => GetValue(NodesCountProperty);
        set
        {
            SetValue(NodesCountProperty, value);
            Node.TargetNumber = value;
        }
    }

    public static readonly StyledProperty<int> UsersCountProperty = AvaloniaProperty.Register<PublicView, int>(
        nameof(UsersCount), defaultBindingMode:BindingMode.TwoWay);

    public int UsersCount
    {
        get => GetValue(UsersCountProperty);
        set
        {
            SetValue(UsersCountProperty, value);
            Users.TargetNumber = value;
        }
    }

    public static readonly StyledProperty<int> ProxiesCountProperty = AvaloniaProperty.Register<PublicView, int>(
        nameof(ProxiesCount), defaultBindingMode:BindingMode.TwoWay);

    public int ProxiesCount
    {
        get => GetValue(ProxiesCountProperty);
        set
        {
            SetValue(ProxiesCountProperty, value);
            Proxies.TargetNumber = value;
        }
    }

    public static readonly StyledProperty<long> TrafficProperty = AvaloniaProperty.Register<PublicView, long>(
        nameof(Traffic), defaultBindingMode:BindingMode.TwoWay);

    public long Traffic
    {
        get => GetValue(TrafficProperty);
        set
        {
            SetValue(TrafficProperty, value);
            Traffics.TargetNumber = ProcessFileSize(Traffic);
        }
    }


    public PublicView()
    {
        InitializeComponent();
        Node.TargetNumber = NodesCount;
        Users.TargetNumber = UsersCount;
        Proxies.TargetNumber = ProxiesCount;
        Traffics.TargetNumber = ProcessFileSize(Traffic);
    }

    /// <summary>
    /// 根据<paramref name="fileSize"/>的大小自动返回对应的文件大小值。
    /// <br/>
    /// 如：若<paramref name="fileSize"/>32743879328,则返回30.50GB；
    /// 返回值的数值范围为1~1000。
    /// </summary>
    /// <param name="fileSize">文件大小，单位为Bytes</param>
    /// <returns>处理后的文件大小值。</returns>
    private static int ProcessFileSize(long fileSize)
    {
        string[] sizeUnits = ["B", "KB", "MB", "GB", "TB"];
        double size = fileSize;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < sizeUnits.Length - 1)
        {
            size /= 1024;
        }

        return Convert.ToInt32(Math.Round(size));
    }
}

public class TrafficToTargetNumberConverter : IValueConverter
{
    public static TrafficToTargetNumberConverter Instance
    {
        get;
    } = new();
    
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is long traffic ? ProcessFileSize(traffic).ToString("######.## TB") : 0;
    }

    /// <summary>
    /// 根据<paramref name="fileSize"/>的大小自动返回对应的文件大小值。
    /// <br/>
    /// 如：若<paramref name="fileSize"/>32743879328,则返回30.50GB；
    /// 返回值的数值范围为1~1000。
    /// </summary>
    /// <param name="fileSize">文件大小，单位为Bytes</param>
    /// <returns>处理后的文件大小值。</returns>
    private static double ProcessFileSize(long fileSize)
    {
        string[] sizeUnits = ["B", "KB", "MB", "GB", "TB"];
        double size = fileSize;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < sizeUnits.Length - 1)
        {
            size /= 1024;
        }

        return size;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}