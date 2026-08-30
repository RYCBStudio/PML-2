using System;
using System.Globalization;
using Avalonia.Data.Converters;
using MEFrpLauncherX.Core.Languages;

namespace MEFrpLauncherX;

/// <summary>插件执行日志状态 → 本地化文本（26.3.1 S4）</summary>
public class PluginLogStatusConverter : IValueConverter
{
    public static PluginLogStatusConverter Instance
    {
        get;
    } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "success" => Languages.Text_PluginList_LogStatusSuccess,
            "failed" => Languages.Text_PluginList_LogStatusFailed,
            "skipped" => Languages.Text_PluginList_LogStatusSkipped,
            _ => Languages.Text_PluginList_LogStatusInfo
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
