using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Controls;
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

        // 26.5.0：旧版本目录策略 + 多余版本目录提醒
        KeepOldVersionBox.SelectedIndex = ConfigManager.CurrentConfig.UpdateSettings.KeepOldVersion switch
        {
            UpdateSettings.UpdateOldVersionValues.Keep => 1,
            UpdateSettings.UpdateOldVersionValues.Discard => 2,
            _ => 0
        };
        NotifyRedundantVersionSwitch.IsChecked = ConfigManager.CurrentConfig.UpdateSettings.NotifyRedundantVersion;

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

    /// <summary>
    ///     旧版本目录策略变更（26.5.0）：每次询问 / 保留 / 删除。
    /// </summary>
    private void KeepOldVersionChange(object? sender, SelectionChangedEventArgs e)
    {
        if (!_init)
        {
            return;
        }

        var value = ((sender as ComboBox)?.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        if (value is null)
        {
            return;
        }

        (DataContext as UpdatePageViewModel)?.SetKeepOldVersion(value);
    }

    /// <summary>是否在启动时提醒清理多余版本目录（26.5.0）。</summary>
    private void NotifyRedundantVersionChanged(object? sender, RoutedEventArgs e)
    {
        if (!_init)
        {
            return;
        }

        var value = (sender as ToggleSwitch)?.IsChecked ?? false;
        ConfigManager.UpdateConfig(cfg => cfg.UpdateSettings.NotifyRedundantVersion = value);
    }

    /// <summary>
    ///     立即清理可删除的旧版本目录（26.5.0）。
    ///     <para>删除前二次确认，并明示「data\ 中的数据不受影响、但将失去一键回滚」。</para>
    /// </summary>
    private async void CleanupVersionsNow(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not UpdatePageViewModel vm)
        {
            return;
        }

        var inspection = VersionCleanupService.Inspect();
        if (inspection.Removable.Count == 0)
        {
            Growl.Info(Languages.Text_Update_VersionCleanup_NothingToDo);
            return;
        }

        var confirm = new FAContentDialog
        {
            Title = Languages.Text_Update_VersionCleanup_ConfirmTitle,
            Content = string.Format(Languages.Text_Update_VersionCleanup_ConfirmMessage,
                inspection.Removable.Count,
                ToolboxService.FormatFileSize(inspection.RemovableSizeBytes)),
            PrimaryButtonText = Languages.Text_Update_CleanupVersionsNow,
            CloseButtonText = Languages.Text_Global_Cancel,
            DefaultButton = FAContentDialogButton.Close
        };

        if (await confirm.ShowAsync() != FAContentDialogResult.Primary)
        {
            return;
        }

        var (removed, failed, freed) = vm.CleanupOldVersions();

        if (removed > 0)
        {
            Growl.Success(string.Format(Languages.Text_Update_VersionCleanup_Done, removed,
                ToolboxService.FormatFileSize(freed)));
        }

        if (failed > 0)
        {
            Growl.Warning(Languages.Text_Update_VersionCleanup_Failed);
        }

        if (removed == 0 && failed == 0)
        {
            Growl.Info(Languages.Text_Update_VersionCleanup_NothingToDo);
        }
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

/// <summary>
///     把「旧版本目录」下拉框的选中序号转换为对应说明文案（26.5.0）。
///     <para>非版本化布局（开发态 / ≤26.4 单层安装）下该设置无意义，单独给出提示。</para>
/// </summary>
public class UpdateKeepOldVersionBoxItemToDescConverter : IValueConverter
{
    public static UpdateKeepOldVersionBoxItemToDescConverter Instance
    {
        get;
    } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // 旧布局没有 vXXX\ 目录，保留/删除都无从谈起 —— 如实说明而不是给出无效选项
        if (!AppPaths.IsVersionedLayout)
        {
            return Languages.Text_Update_KeepOldVersionDesc_Unavailable;
        }

        return value is int index
            ? index switch
            {
                0 => Languages.Text_Update_KeepOldVersionDesc_Ask,
                1 => Languages.Text_Update_KeepOldVersionDesc_Keep,
                2 => Languages.Text_Update_KeepOldVersionDesc_Discard,
                _ => Languages.Text_Update_KeepOldVersionDesc_Ask
            }
            : Languages.Text_Update_KeepOldVersionDesc_Ask;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
