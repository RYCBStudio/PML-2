using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.ViewModels;

namespace MEFrpLauncherX.Views;

public partial class UpdatePage : UserControl
{
    private readonly bool _init;

    public UpdatePage()
    {
        InitializeComponent();
        DataContext = new UpdatePageViewModel();
        var autoUpdate = ConfigManager.CurrentConfig.UpdateSettings.AutoCheck;
        UpdateMethodBox.SelectedIndex = ConfigManager.CurrentConfig.UpdateSettings.Method switch
        {
            "ds" => 0,
            "dd" => 1,
            "md" => 2,
            _ => 0
        };
        UpdateChannelBox.SelectedIndex = ConfigManager.CurrentConfig.UpdateSettings.Channel.ToLower().ToUpper(1) switch
        {
            "Stable" => 0,
            "Preview" => 1,
            _ => 0
        };
        KeepProfileSwitch.IsChecked = ConfigManager.CurrentConfig.UpdateSettings.KeepProfile;

        _init = true;
        MainPageFrameViewModel.UpdatePage = this;
    }

    private void UpdateMethodChange(object? sender, SelectionChangedEventArgs e)
    {
        if (!_init)
        {
            return;
        }

        ConfigManager.UpdateConfig(cfg =>
        {
            var method = ((sender as ComboBox).SelectedItem as ComboBoxItem)?.Tag?.ToString();
            cfg.UpdateSettings.Method = method;
            cfg.UpdateSettings.AutoCheck = method.StartsWith("d");
        });
    }

    private void UpdateChannelChange(object? sender, SelectionChangedEventArgs e)
    {
        if (!_init)
        {
            return;
        }

        ConfigManager.UpdateConfig(cfg =>
        {
            cfg.UpdateSettings.Channel = ((sender as ComboBox).SelectedItem as ComboBoxItem)?.Tag?.ToString();
        });
    }

    private void KeepProfileChanged(object? sender, RoutedEventArgs e)
    {
        if (!_init)
        {
            return;
        }

        ConfigManager.UpdateConfig(cfg =>
            cfg.UpdateSettings.KeepProfile = (sender as ToggleSwitch)?.IsChecked ?? false);
    }
}

public class UpdateChannelBoxItemToDescConverter : IValueConverter
{
    public static UpdateChannelBoxItemToDescConverter Instance
    {
        get;
    } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is int index
            ? index switch
            {
                0 => "接收应用稳定版的更新,包含较新且稳定的特性和改进。",
                1 => "提前预览下一个版本中应用的功能,包含较新的特性和改进,可能存在少量缺陷。",
                _ => "未知渠道"
            }
            : "未知渠道";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}