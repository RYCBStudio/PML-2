using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace MEFrpLauncherX.Controls;

public class BoolToBorderThicknessConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (bool)value ? 2 : -1;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}