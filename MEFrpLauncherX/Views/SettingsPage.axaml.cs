using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using FluentAvalonia.UI.Controls;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.ViewModels;
using MEFrpLauncherX.Styling;
using MEFrpLauncherX.ViewModels;
using MEFrpLauncherX.Views.Appearance;
using MEFrpLauncherX.Views.ProxyMonitor;
using Microsoft.Win32;

// ReSharper disable UnusedParameter.Local
#pragma warning disable CS8601 // 引用类型赋值可能为 null。
#pragma warning disable CS8629 // 可为 null 的值类型可为 null。

namespace MEFrpLauncherX.Views;

public partial class SettingsPage : UserControl
{
    private bool _isInit;

    public SettingsPage()
    {
        _isInit = true;
        InitializeComponent();
        AttachedToVisualTree += (s, e) =>
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
            _isInit = true;
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

            TerminalEngineTypeBox.SelectedIndex =
                ConfigManager.CurrentConfig.TerminalEngineType.ToUpper() switch
                {
                    "XTERM" => 1,
                    _ => 0
                };
            TerminalCliComboBox.SelectedIndex =
                ConfigManager.CurrentConfig.TerminalCli.ToLower() switch
                {
                    "pwsh" => 1,
                    "cmd" => 2,
                    "bash" => 3,
                    "zsh" => 4,
                    _ => 0
                };
            AutoLogin.IsChecked = ConfigManager.CurrentConfig.AutoLogin;
            AutoSign.IsChecked = ConfigManager.CurrentConfig.AutoSign;
            LanguageSelectComboBox.SelectedIndex = ConfigManager.CurrentConfig.Language switch
            {
                "zh-CN" => 0,
                "en-US" => 1,
                "zh-Hant" => 2,
                _ => 0
            };

            AnimationLevelBox.SelectedIndex = ConfigManager.CurrentConfig.AnimationLevel switch
            {
                0 => 0,
                1 => 1,
                _ => 2
            };
            var renderConfig = RenderConfigManager.Load();
            RenderingModeBox.SelectedIndex = (renderConfig.RenderingMode ?? "Auto").ToUpper() switch
            {
                "VULKAN" => 1,
                "OPENGL" => 2,
                "SOFTWARE" => 3,
                _ => 0
            };
            GpuMemoryBox.SelectedIndex = renderConfig.GpuMemoryLimitMb switch
            {
                128 => 0,
                512 => 2,
                1024 => 3,
                _ => 1
            };
            LowLatencySwitch.IsChecked = renderConfig.LowLatencyRendering;

            MainPageFrameViewModel.Instance?.IsLoading = false;
            _isInit = false;
        };
        if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
        {
            NoSenseValidation.Content = Core.Languages.Languages.Text_Settings_Captcha_Implicit;
            BrowserValidation.Content = Core.Languages.Languages.Text_Settings_Captcha_ExplicitRecommended;
        }
        else
        {
            NoSenseValidation.Content = Core.Languages.Languages.Text_Settings_Captcha_ImplicitRecommended;
            BrowserValidation.Content = Core.Languages.Languages.Text_Settings_Captcha_Explicit;
        }
    }

    private void SkinChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInit)
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
        if (_isInit)
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
        if (_isInit)
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
        if (_isInit)
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
        if (_isInit)
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
        if (_isInit)
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
            else if (OperatingSystem.IsMacOS())
            {
                SetAutoStartMacOS(isAutoStartEnabled);
            }

            // 可以添加其他操作系统支持
            Growl.Success(isAutoStartEnabled
                ? Core.Languages.Languages.Text_Settings_AutoStartAdded
                : Core.Languages.Languages.Text_Settings_AutoStartRemoved);
        }
        catch (Exception ex)
        {
            // 记录错误并提供用户反馈
            Core.App.CurrentLogger.Log($"设置开机自启动失败: {ex.Message}");
            Core.App.CurrentLogger.Error(ex);
            ShowErrorMessage(Core.Languages.Languages.Text_Settings_AutoStartChangeFailed);

            // 回滚UI状态
            checkBox.IsChecked = !isAutoStartEnabled;
        }
    }

    private void SetAutoStartWindows(bool enable)
    {
        const string appName = "PML Ⅱ"; // 替换为你的应用名称
        var executablePath = Environment.ProcessPath;

        if (string.IsNullOrEmpty(executablePath) || !OperatingSystem.IsWindows())
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
        const string appName = "pml-2"; // 替换为你的应用ID
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

    /// <summary>
    ///     macOS 开机自启：写入用户 LaunchAgent plist（登录时由 launchd 启动）
    /// </summary>
    private void SetAutoStartMacOS(bool enable)
    {
        const string label = "tech.rycb.pml2";
        var launchAgentsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "LaunchAgents");
        var plistPath = Path.Combine(launchAgentsDir, $"{label}.plist");
        var executablePath = Environment.ProcessPath;

        if (string.IsNullOrEmpty(executablePath))
        {
            throw new InvalidOperationException("无法获取可执行文件路径");
        }

        if (enable)
        {
            var plist = $"""
                         <?xml version="1.0" encoding="UTF-8"?>
                         <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                         <plist version="1.0">
                         <dict>
                             <key>Label</key>
                             <string>{label}</string>
                             <key>ProgramArguments</key>
                             <array>
                                 <string>{executablePath}</string>
                             </array>
                             <key>RunAtLoad</key>
                             <true/>
                         </dict>
                         </plist>
                         """;

            Directory.CreateDirectory(launchAgentsDir);
            File.WriteAllText(plistPath, plist);
        }
        else if (File.Exists(plistPath))
        {
            File.Delete(plistPath);
        }
    }

    // 可选：添加错误显示方法
    private async void ShowErrorMessage(string message)
    {
        // 使用Avalonia的MessageBox或自定义对话框
        await MessageBox.ShowAsync(message, Core.Languages.Languages.Caption_Error, MessageBoxIcon.Error);
    }

    private void AutoLaunchChanged(object? sender, RoutedEventArgs e)
    {
        if (_isInit)
        {
            return;
        }

        ConfigManager.UpdateConfig(config =>
        {
            config.AutoLaunch = (bool)(sender as ToggleButton).IsChecked;
        });
    }

    private void OpenALPSettingsWindow(object? sender, RoutedEventArgs e) =>
        new ALPSettings().ShowDialog(Core.App.MainWindow);

    private void ExpireDaysChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isInit)
        {
            return;
        }

        ConfigManager.UpdateConfig(config =>
            config.ExpireDays = (int)e.NewValue);
        MainPageFrameViewModel.Instance.NeedRestart = true;
    }

    private void ThemeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInit)
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

    private void SetProxyMonitorBar(object? sender, RoutedEventArgs e) =>
        new ProxyFloatSettings().ShowDialog(Core.App.MainWindow);

    private void PMChanged(object? sender, RoutedEventArgs e)
    {
        if (_isInit)
        {
            return;
        }

        ConfigManager.UpdateConfig(config => config.PMSettings.Enabled = (sender as ToggleButton).IsChecked.Value);
    }

    private async void ChooseBackground(object? sender, RoutedEventArgs e) =>
        FooterButtonSettingsItem.SelectBackgroundImpl();

    private void StretchChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInit)
        {
            return;
        }

        ConfigManager.UpdateConfig(config =>
        {
            config.BackgroundSettings.Stretch = (string)((sender as ComboBox)?.SelectedItem as ComboBoxItem)?.Tag ?? "";
        });
        if (File.Exists(ConfigManager.CurrentConfig.BackgroundSettings.BackgroundImage))
        {
            AppearanceSettings.UpdateBackground(ConfigManager.CurrentConfig.BackgroundSettings.ShouldFillTitleBar);
        }

        Core.App.MainWindow.InvalidateVisual();
    }

    private void ClearBackground(object? sender, RoutedEventArgs e)
    {
        Core.App.MainWindow.Background = null;
        MainWindow.Instance.MainBackground.Hide();
        ConfigManager.UpdateConfig(config => config.BackgroundSettings.BackgroundImage = "");
        Core.App.MainWindow.InvalidateVisual();
    }

    private void SetAppearanceAdvanced(object? sender, RoutedEventArgs e) =>
        new AppearanceSettings { DataContext = new AppearanceSettingsViewModel() }.Show(Core.App.MainWindow);

    private void CaptchaModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInit)
        {
            return;
        }

        ConfigManager.UpdateConfig(config =>
        {
            config.CaptchaMode = (string)((sender as ComboBox)?.SelectedItem as ComboBoxItem)?.Tag ?? "";
        });
    }

    private void DownloadSourceChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInit)
        {
            return;
        }

        ConfigManager.UpdateConfig(config =>
            config.DownloadSource = (string)((sender as ComboBox)?.SelectedItem as ComboBoxItem)?.Tag ?? "");
    }

    private void DoNotShowResponseSettingsChanged(object? sender, RoutedEventArgs e)
    {
        if (_isInit)
        {
            return;
        }

        ConfigManager.UpdateConfig(config => config.DoNotShowSuccessMsg = (sender as ToggleSwitch).IsChecked.Value);
    }

    private void TerminalEngineTypeChanged(object? sender, RoutedEventArgs e)
    {
        if (_isInit)
        {
            return;
        }

        ConfigManager.UpdateConfig(config =>
            config.TerminalEngineType = ((sender as ComboBox).SelectedItem as ComboBoxItem).Tag.ToString() ?? "");
    }

    private void SetTerminalCli(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInit)
        {
            return;
        }

        var cb = sender as FAComboBox;
        ConfigManager.UpdateConfig(config =>
            config.TerminalCli = cb?.SelectedItem is FAComboBoxItem item
                ? item.Tag?.ToString() ?? CliUtils.GetOSSpeceficDefaultCli()
                : cb?.Text ?? CliUtils.GetOSSpeceficDefaultCli());
    }

    private void AutoLoginChanged(object? sender, RoutedEventArgs e)
    {
        if (_isInit)
        {
            return;
        }

        ConfigManager.UpdateConfig(config => config.AutoLogin = (sender as ToggleSwitch).IsChecked.Value);
    }

    private void AutoSignChanged(object? sender, RoutedEventArgs e)
    {
        if (_isInit)
        {
            return;
        }

        ConfigManager.UpdateConfig(config => config.AutoSign = (sender as ToggleSwitch).IsChecked.Value);
    }

    private void AnimationLevelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInit)
        {
            return;
        }

        var level = (sender as ComboBox).SelectedIndex;
        ConfigManager.UpdateConfig(config => config.AnimationLevel = level);
        AnimationStyles.Apply(level);
    }

    private void RenderingModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInit)
        {
            return;
        }

        RenderConfigManager.UpdateConfig(cfg =>
            cfg.RenderingMode = ((sender as ComboBox).SelectedItem as ComboBoxItem).Tag.ToString() ?? "Auto");
        RenderRestartNotice.IsOpen = true;
        MainPageFrameViewModel.Instance.NeedRestart = true;
    }

    private void GpuMemoryChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInit)
        {
            return;
        }

        RenderConfigManager.UpdateConfig(cfg =>
            cfg.GpuMemoryLimitMb =
                int.TryParse(((sender as ComboBox).SelectedItem as ComboBoxItem).Tag.ToString(), out var mb)
                    ? mb
                    : 256);
        RenderRestartNotice.IsOpen = true;
        MainPageFrameViewModel.Instance.NeedRestart = true;
    }

    private void LowLatencyChanged(object? sender, RoutedEventArgs e)
    {
        if (_isInit)
        {
            return;
        }

        RenderConfigManager.UpdateConfig(cfg => cfg.LowLatencyRendering = (sender as ToggleSwitch).IsChecked.Value);
        RenderRestartNotice.IsOpen = true;
        MainPageFrameViewModel.Instance.NeedRestart = true;
    }

    private void LanguageChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isInit)
        {
            return;
        }
        
        ConfigManager.UpdateConfig(config =>
        {
            config.Language = (string)((sender as ComboBox)?.SelectedItem as ComboBoxItem)?.Tag ?? "";
        });
        MainPageFrameViewModel.Instance.NeedRestart = true;
    }
}

public class ValidationModeConverter : IValueConverter
{
    public static readonly ValidationModeConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not int i)
        {
            return null;
        }

        return i switch
        {
            0 => Core.Languages.Languages.Text_Settings_Captcha_ImplicitDesc,
            1 => Core.Languages.Languages.Text_Settings_Captcha_ExplicitDesc,
            _ => Core.Languages.Languages.Text_Settings_Captcha_UnknownMode
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}