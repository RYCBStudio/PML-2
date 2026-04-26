using System;
using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace MEFrpLauncherX;

public static class WindowPositionHelper
{
    private const int Tolerance = 10;

    /// <summary>
    ///     根据指定的位置指示符组合，计算窗口在屏幕上的坐标。
    /// </summary>
    /// <param name="window">需要定位的窗口。</param>
    /// <param name="positionIndicators">位置指示符字符串，例如 "rt" (right-top)。</param>
    /// <returns>一个包含 X 和 Y 坐标的 PixelPoint。如果无法计算，则返回窗口当前位置。</returns>
    public static PixelPoint GetPosition(Window window, string positionIndicators)
    {
        if (window == null)
        {
            throw new ArgumentNullException(nameof(window));
        }

        if (string.IsNullOrEmpty(positionIndicators))
        {
            return window.Position;
        }

        var screen = window.Screens.Primary;
        if (screen == null)
        {
            return window.Position;
        }

        var workingArea = screen.WorkingArea;
        var windowWidth = double.IsNaN(window.Width) ? window.DesiredSize.Width : window.Width;
        var windowHeight = double.IsNaN(window.Height) ? window.DesiredSize.Height : window.Height;

        if (windowWidth <= 0 || windowHeight <= 0)
        {
            return window.Position;
        }

        // 从当前位置开始
        double x = window.Position.X;
        double y = window.Position.Y;

        // 遍历所有指示符
        foreach (var c in positionIndicators.ToLower())
        {
            switch (c)
            {
                case 'r':
                    x = workingArea.Right - windowWidth - 10;
                    break;
                case 't':
                    y = workingArea.TopLeft.Y;
                    break;
                case 'l':
                    x = workingArea.TopLeft.X;
                    break;
                case 'b':
                    y = workingArea.Bottom - windowHeight - 50;
                    break;
            }
        }

        return new PixelPoint((int)x, (int)y);
    }

    public static string GetPositionReverse(Window window)
    {
        // ... (上面实现的 Reverse 方法)
        if (window == null)
        {
            throw new ArgumentNullException(nameof(window));
        }

        var screen = window.Screens.Primary;
        if (screen == null)
        {
            return string.Empty;
        }

        var workingArea = screen.WorkingArea;
        var windowWidth = double.IsNaN(window.Width) ? window.DesiredSize.Width : window.Width;
        var windowHeight = double.IsNaN(window.Height) ? window.DesiredSize.Height : window.Height;

        if (windowWidth <= 0 || windowHeight <= 0)
        {
            return string.Empty;
        }

        var position = window.Position;
        var result = new StringBuilder();

        if (Math.Abs(position.X - workingArea.TopLeft.X) <= Tolerance)
        {
            result.Append('l');
        }
        else if (Math.Abs(position.X - (workingArea.Right - windowWidth)) <= Tolerance)
        {
            result.Append('r');
        }

        if (Math.Abs(position.Y - workingArea.TopLeft.Y) <= Tolerance)
        {
            result.Append('t');
        }
        else if (Math.Abs(position.Y - (workingArea.Bottom - windowHeight)) <= Tolerance)
        {
            result.Append('b');
        }

        return result.ToString();
    }
}

public class LoadPercentToForegroundConverter : IValueConverter
{
    public static LoadPercentToForegroundConverter Instance
    {
        get;
    } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (int)value switch
        {
            >= 85 => App.Current.TryGetResource("SystemFillColorCriticalBrush", App.Current.ActualThemeVariant,
                out var o)
                ? o
                : null,
            < 85 and >= 60 => App.Current.TryGetResource("SystemFillColorCautionBrush", App.Current.ActualThemeVariant,
                out var o1)
                ? o1
                : null,
            < 60 and >= 40 => App.Current.TryGetResource("SystemFillColorAttentionBrush",
                App.Current.ActualThemeVariant, out var o3)
                ? o3
                : null,
            < 40 => App.Current.TryGetResource("SystemFillColorSuccessBrush", App.Current.ActualThemeVariant,
                out var o2)
                ? o2
                : null
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}