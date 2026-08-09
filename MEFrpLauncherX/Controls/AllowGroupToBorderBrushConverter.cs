using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Data.Converters;

namespace MEFrpLauncherX.Controls;

public class AllowGroupToBorderBrushConverter : IValueConverter
{
    public static AllowGroupToBorderBrushConverter Instance
    {
        get;
    } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is IEnumerable<string> e && !e.Contains("default")
            ? Application.Current.TryGetResource("SystemFillColorCautionBrush", Application.Current.ActualThemeVariant,
                out var o)
                ? o
                : null
            : null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}