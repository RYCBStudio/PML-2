using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Languages;
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
        CompileTypeBox.SelectedIndex = ConfigManager.CurrentConfig.UpdateSettings.CompileType switch
        {
            "AOT" => 0,
            "Common" => 1,
            _ => Core.App.ReleaseFlag == "AOT" ? 0 : 1
        };

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

    private void CompileTypeChange(object? sender, SelectionChangedEventArgs e)
    {
        if (!_init)
        {
            return;
        }

        ConfigManager.UpdateConfig(cfg =>
        {
            cfg.UpdateSettings.CompileType = ((sender as ComboBox)?.SelectedItem as ComboBoxItem)?.Tag?.ToString()
                                             ?? Core.App.ReleaseFlag;
        });
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
                0 => Languages.Text_Update_StableChannelDesc,
                1 => Languages.Text_Update_PreviewChannelDesc,
                _ => Languages.Text_Update_UnknownChannel
            }
            : Languages.Text_Update_UnknownChannel;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
