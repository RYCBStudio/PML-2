using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Core.Services;
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
        DownloadSourceBox.SelectedIndex =
            GitHubUpdateSources.Normalize(ConfigManager.CurrentConfig.UpdateSettings.DownloadSource) switch
            {
                GitHubUpdateSources.GitHub => 1,
                GitHubUpdateSources.GitHubGhProxy => 2,
                GitHubUpdateSources.GitHubMoeyy => 3,
                _ => 0
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

    private void DownloadSourceChange(object? sender, SelectionChangedEventArgs e)
    {
        if (!_init)
        {
            return;
        }

        ConfigManager.UpdateConfig(cfg =>
        {
            // 归一化后再落盘，避免存储非法取值（UI 下拉项恒为合法值）
            cfg.UpdateSettings.DownloadSource = GitHubUpdateSources.Normalize(
                ((sender as ComboBox)?.SelectedItem as ComboBoxItem)?.Tag?.ToString());
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

/// <summary>
///     把更新页「下载源」下拉框的选中序号转换为对应说明文案（26.4）。
/// </summary>
public class UpdateDownloadSourceBoxItemToDescConverter : IValueConverter
{
    public static UpdateDownloadSourceBoxItemToDescConverter Instance
    {
        get;
    } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is int index
            ? index switch
            {
                0 => Languages.Text_Update_DownloadSourceDesc_TPCA,
                1 => Languages.Text_Update_DownloadSourceDesc_GitHub,
                2 => Languages.Text_Update_DownloadSourceDesc_GitHubGhProxy,
                3 => Languages.Text_Update_DownloadSourceDesc_GitHubMoeyy,
                _ => Languages.Text_Update_DownloadSourceDesc_Unknown
            }
            : Languages.Text_Update_DownloadSourceDesc_Unknown;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
