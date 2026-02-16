using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.ViewModels;
using MEFrpLauncherX.Views.Appearance;
using MEFrpLauncherX.Views.ProxyMonitor;
using Microsoft.Win32;

namespace MEFrpLauncherX.Views;

public partial class SettingsPage : UserControl, INotifyPropertyChanged
{
    private bool isInit;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += (s, e) =>
        {
            MainPageFrameViewModel.Instance?.IsLoading = true;
            Skin.SelectedIndex = ConfigManager.CurrentConfig.Skin.ToUpper(0) switch
            {
                "Acyclic" or "AcrylicBlur" => 1,
                "Mica" => 0,
                "Blur" => 2,
                "Transparent" => 3,
                _ => 1
            };
            Theme.SelectedIndex = ConfigManager.CurrentConfig.Theme.ToLower().ToUpper(0) switch
            {
                "Light" => 1,
                _ => 0
            };
            HideInsteadOfClose.IsChecked = ConfigManager.CurrentConfig.HideInsteadOfClose;
            KickWithoutDisable.IsChecked = ConfigManager.CurrentConfig.KickWithoutDisable;
            ParallelDownload.IsChecked = ConfigManager.CurrentConfig.ParallelDownload;
            ParallelDownloadThreads.Value = ConfigManager.CurrentConfig.ParallelCount;
            AutoStart.IsChecked = ConfigManager.CurrentConfig.AutoStartup;
            AutoLaunch.IsChecked = ConfigManager.CurrentConfig.AutoLaunch;
            pmS.IsChecked = ConfigManager.CurrentConfig.PMSettings.Enabled;
            StretchBox.SelectedIndex =
                ConfigManager.CurrentConfig.BackgroundSettings.Stretch.ToUpper(0) switch
                {
                    "None" => 0,
                    "Stretch" => 1,
                    "Uniform" => 2,
                    "UniformToFill" => 3,
                    _ => 0
                };
            CaptchaModeCBox.SelectedIndex = ConfigManager.CurrentConfig.CaptchaMode.ToUpper(0) switch
            {
                "Implicit" or "NoSense" => 0,
                "Explicit" or "Browser" => 1,
                _ => 0
            };
            DownloadSource.SelectedIndex = ConfigManager.CurrentConfig.DownloadSource.ToUpper() switch
            {
                "TPCA" => 0,
                "OFFICIAL" => 1,
                _ => 0
            };
            DoNotShowResponseSettings.IsChecked = ConfigManager.CurrentConfig.DoNotShowSuccessMsg;
            isInit = true;
            if (ParallelDownloadThreads.Value >= 32)
            {
                TooMoreThreadWarning.IsOpen = true;
                TooMoreThreadWarningExpanderItem.IsVisible = true;
            }
            else
            {
                TooMoreThreadWarning.IsOpen = false;
                TooMoreThreadWarningExpanderItem.IsVisible = false;
            }

            MainPageFrameViewModel.Instance?.IsLoading = false;
        };
        if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
        {
            NoSenseValidation.Content = "无感验证";
            BrowserValidation.Content = "(推荐) 浏览器验证";
        }
        else
        {
            NoSenseValidation.Content = "(推荐) 无感验证";
            BrowserValidation.Content = "浏览器验证";
        }
    }

    private void SkinChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!isInit)
        {
            return;
        }

        ConfigManager.UpdateConfig(config =>
        {
            config.Skin = (string)((sender as ComboBox).SelectedItem as ComboBoxItem).Tag;
        });
        Core.App.MainWindow.TransparencyLevelHint =
        [
            ConfigManager.CurrentConfig.Skin.ToUpper(0) switch
            {
                "Mica" => WindowTransparencyLevel.Mica,
                "AcrylicBlur" or "Acrylic" => WindowTransparencyLevel.AcrylicBlur,
                "Blur" => WindowTransparencyLevel.Blur,
                "Transparent" => WindowTransparencyLevel.Transparent,
                _ => WindowTransparencyLevel.None
            }
        ];
        if (ConfigManager.CurrentConfig.Skin.ToUpper(0) == "None")
        {
            Core.App.MainWindow.Background =
                ConfigManager.CurrentConfig.Theme.Equals("Dark", StringComparison.OrdinalIgnoreCase)
                    ? Color.TryParse("#FF2D2D30", out var C) ? new SolidColorBrush(C) : Brushes.Black
                    : Color.TryParse("#FFF9F9F9", out var C1)
                        ? new SolidColorBrush(C1)
                        : Brushes.White;
        }

        Core.App.MainWindow.InvalidateVisual();
    }

    private void HideInsteadOfCloseChanged(object sender, RoutedEventArgs e)
    {
        if (!isInit)
        {
            return;
        }

        ConfigManager.UpdateConfig(config =>
        {
            config.HideInsteadOfClose = (bool)(sender as ToggleButton).IsChecked;
        });
    }

    private void KickWithoutDisableChanged(object sender, RoutedEventArgs e)
    {
        if (!isInit)
        {
            return;
        }

        ConfigManager.UpdateConfig(config =>
        {
            config.KickWithoutDisable = (bool)(sender as ToggleButton).IsChecked;
        });
    }

    private void ParallelDownloadChanged(object sender, RoutedEventArgs e)
    {
        if (!isInit)
        {
            return;
        }

        ConfigManager.UpdateConfig(config =>
        {
            config.ParallelDownload = (bool)(sender as ToggleButton).IsChecked;
        });
    }

    private void ParallelDownloadThreadsChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!isInit)
        {
            return;
        }

        ParallelDownloadThreads.Value = Math.Round(ParallelDownloadThreads.Value);
        if (ParallelDownloadThreads.Value >= 32)
        {
            TooMoreThreadWarning.IsOpen = true;
            TooMoreThreadWarningExpanderItem.IsVisible = true;
        }
        else
        {
            TooMoreThreadWarning.IsOpen = false;
            TooMoreThreadWarningExpanderItem.IsVisible = false;
        }

        ConfigManager.UpdateConfig(config =>
        {
            config.ParallelCount = (int)ParallelDownloadThreads.Value;
        });
    }

    private void AutoStartChanged(object? sender, RoutedEventArgs e)
    {
        if (!isInit)
        {
            return;
        }

        if (sender is not ToggleButton checkBox)
        {
            return;
        }

        var isAutoStartEnabled = (bool)checkBox.IsChecked;

        // 更新配置
        ConfigManager.UpdateConfig(config =>
        {
            config.AutoStartup = isAutoStartEnabled;
        });

        // 设置开机自启动
        try
        {
            if (OperatingSystem.IsWindows())
            {
                SetAutoStartWindows(isAutoStartEnabled);
            }
            else if (OperatingSystem.IsLinux())
            {
                SetAutoStartLinux(isAutoStartEnabled);
            }

            // 可以添加其他操作系统支持
            Growl.Success(isAutoStartEnabled ? "添加开机启动项成功" : "删除开机启动项成功");
        }
        catch (Exception ex)
        {
            // 记录错误并提供用户反馈
            Core.App.CurrentLogger.Log($"设置开机自启动失败: {ex.Message}");
            Core.App.CurrentLogger.Error(ex);
            ShowErrorMessage("无法更改开机自启动设置");

            // 回滚UI状态
            checkBox.IsChecked = !isAutoStartEnabled;
        }
    }

    private void SetAutoStartWindows(bool enable)
    {
        const string appName = "PML Ⅱ"; // 替换为你的应用名称
        var executablePath = Environment.ProcessPath;

        if (string.IsNullOrEmpty(executablePath))
        {
            throw new InvalidOperationException("无法获取可执行文件路径");
        }

        using var key = Registry.CurrentUser.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);

        if (key == null)
        {
            throw new InvalidOperationException("无法访问注册表");
        }

        if (enable)
        {
            key.SetValue(appName, $"\"{executablePath}\"");
        }
        else
        {
            key.DeleteValue(appName, false);
        }
    }

    private void SetAutoStartLinux(bool enable)
    {
        const string appName = "mefrplauncherx"; // 替换为你的应用ID
        var desktopFile =
            $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/.config/autostart/{appName}.desktop";
        var executablePath = Environment.ProcessPath;

        if (string.IsNullOrEmpty(executablePath))
        {
            throw new InvalidOperationException("无法获取可执行文件路径");
        }

        if (enable)
        {
            // 创建.desktop文件
            var desktopContent = $"""
                                  [Desktop Entry]
                                  Type=Application
                                  Name=PML 2
                                  Name[zh_CN]=PML 2
                                  Exec={executablePath}
                                  Icon=/usr/share/icons/meflx.png
                                  StartupNotify=false
                                  Terminal=false
                                  """;

            // 确保目录存在
            Directory.CreateDirectory(Path.GetDirectoryName(desktopFile));
            File.WriteAllText(desktopFile, desktopContent);
        }
        else
        {
            // 删除.desktop文件
            if (File.Exists(desktopFile))
            {
                File.Delete(desktopFile);
            }
        }
    }

    // 可选：添加错误显示方法
    private async void ShowErrorMessage(string message)
    {
        // 使用Avalonia的MessageBox或自定义对话框
        await MessageBox.ShowAsync(message, "错误", MessageBoxIcon.Error);
    }

    private void AutoLaunchChanged(object? sender, RoutedEventArgs e)
    {
        if (!isInit)
        {
            return;
        }

        ConfigManager.UpdateConfig(config =>
        {
            config.AutoLaunch = (bool)(sender as ToggleButton).IsChecked;
        });
    }

    private void OpenALPSettingsWindow(object? sender, RoutedEventArgs e)
    {
        new ALPSettings().ShowDialog(Core.App.MainWindow);
    }

    private void ExpireDaysSlider_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (!isInit)
        {
            return;
        }

        ConfigManager.UpdateConfig(config =>
            config.ExpireDays = (int)e.NewValue);
    }

    private void ThemeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!isInit)
        {
            return;
        }

        var theme = (string)((sender as ComboBox).SelectedItem as ComboBoxItem).Tag;
        ConfigManager.UpdateConfig(config =>
        {
            config.Theme = theme;
        });
        Application.Current?.RequestedThemeVariant = theme.ToLower() switch
        {
            "dark" => ThemeVariant.Dark,
            "light" => ThemeVariant.Light,
            _ => ThemeVariant.Default
        };
        if (ConfigManager.CurrentConfig.Skin.ToUpper(0) == "None")
        {
            Core.App.MainWindow.Background =
                Application.Current?.ActualThemeVariant.Equals(ThemeVariant.Dark) == true
                    ? Color.TryParse("#FF2D2D30", out var C) ? new SolidColorBrush(C) : Brushes.Black
                    : Color.TryParse("#FFF9F9F9", out var C1)
                        ? new SolidColorBrush(C1)
                        : Brushes.White;
        }

        Core.App.MainWindow.InvalidateVisual();
    }

    private void SetProxyMonitorBar(object? sender, RoutedEventArgs e)
    {
        new ProxyFloatSettings().ShowDialog(Core.App.MainWindow);
    }

    private void PMChanged(object? sender, RoutedEventArgs e)
    {
        if (!isInit)
        {
            return;
        }

        ConfigManager.UpdateConfig(config => config.PMSettings.Enabled = (sender as ToggleButton).IsChecked.Value);
    }

    private async void ChooseBackground(object? sender, RoutedEventArgs e)
    {
        FooterButtonSettingsItem.SelectBackgroundImpl();
    }

    private void StretchBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!isInit)
        {
            return;
        }

        ConfigManager.UpdateConfig(config =>
        {
            config.BackgroundSettings.Stretch = (string)((sender as ComboBox)?.SelectedItem as ComboBoxItem)?.Tag ?? "";
        });
        if (File.Exists(ConfigManager.CurrentConfig.BackgroundSettings.BackgroundImage))
        {
            Core.App.MainWindow.Background =
                new ImageBrush(new Bitmap(ConfigManager.CurrentConfig.BackgroundSettings.BackgroundImage))
                {
                    Stretch = ConfigManager.CurrentConfig.BackgroundSettings.Stretch switch
                    {
                        "None" => Stretch.None,
                        "Stretch" => Stretch.Fill,
                        "Uniform" => Stretch.Uniform,
                        "UniformToFill" => Stretch.UniformToFill,
                        _ => Stretch.None
                    },
                };
        }

        Core.App.MainWindow.InvalidateVisual();
    }

    private void ClearBackground(object? sender, RoutedEventArgs e)
    {
        Core.App.MainWindow.Background = null;
        ConfigManager.UpdateConfig(config => config.BackgroundSettings.BackgroundImage = "");
        Core.App.MainWindow.InvalidateVisual();
    }

    private void SetAppearanceAdvanced(object? sender, RoutedEventArgs e)
    {
        new AppearanceSettings { DataContext = new AppearanceSettingsViewModel() }.Show(Core.App.MainWindow);
    }

    private void CaptchaModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        ConfigManager.UpdateConfig(config =>
        {
            config.CaptchaMode = (string)((sender as ComboBox)?.SelectedItem as ComboBoxItem)?.Tag ?? "";
        });
    }

    private void DownloadSourceChanged(object? sender, SelectionChangedEventArgs e)
    {
        ConfigManager.UpdateConfig(config =>
            config.DownloadSource = (string)((sender as ComboBox)?.SelectedItem as ComboBoxItem)?.Tag ?? "");
    }

    private void DoNotShowResponseSettings_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        ConfigManager.UpdateConfig(config => config.DoNotShowSuccessMsg = (sender as ToggleSwitch).IsChecked.Value);
    }
}

public class ValidationModeConverter : IValueConverter
{
    public static readonly ValidationModeConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int)
        {
            return null;
        }

        return (int)value switch
        {
            0 => "在软件内验证, 无需其他操作, 对于x64系列处理器友好, 对于Arm架构处理器可能会耗费大量时间。",
            1 => "通过浏览器打开验证网页, 并手动复制验证结果, 对于Arm处理器友好。",
            _ => "未知方式"
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}