using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace MEFrpLauncherX;

public class VisibilityConverter:IValueConverter
{
    public static VisibilityConverter Instance
    {
        get;
    } = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is 1;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}