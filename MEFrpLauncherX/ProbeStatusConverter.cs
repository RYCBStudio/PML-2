using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace MEFrpLauncherX;

public class ProbeStatusConverter : IValueConverter
{
    public static ProbeStatusConverter Instance
    {
        get;
    } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (value is long i
            ? i switch
            {
                > 0 and <= 60 => "Good",
                <= 120 => "Intermediate",
                _ => "Bad"
            }
            : "Bad").Equals(parameter?.ToString() ?? "");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}