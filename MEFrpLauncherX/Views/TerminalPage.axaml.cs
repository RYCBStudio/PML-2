using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using Iciclecreek.TerminalWindow;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Plugin.Services;
using MEFrpLauncherX.ViewModels;
using MEFrpLauncherX.Views.ProxyMonitor;
using TerminalControl = MEFrpLauncherX.Console.TerminalControl;

namespace MEFrpLauncherX.Views;

public partial class TerminalPage : UserControl
{
    public TerminalPage()
    {
        MainPageFrameViewModel.Instance?.IsLoading = true;
        InitializeComponent();
        Loaded += TerminalPage_Loaded;
        Instance = this;
        MainPageFrameViewModel.TerminalPage = this;
    }

    public static TerminalPage Instance
    {
        get;
        private set;
    }

    private async void TerminalPage_Loaded(object? sender, RoutedEventArgs? e) =>
        MainPageFrameViewModel.Instance?.IsLoading = false;

    private void VisitMEFDoc(object sender, RoutedEventArgs e)
    {
        try
        {
            var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "https://www.mefrp.com/docs/usage/common",
                UseShellExecute = true
            };
            process.Start();
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger.Log($"Error opening URL: {ex.Message}");
        }
    }

    private async Task SendCtrlCCommandToSelected()
    {
        if (MainTabCtrl.SelectedItem is TabItem { Content: TerminalControl terminalControl } tabItem)
        {
            terminalControl.SendCtrlCCommand();
            ProxyFloatViewModel.Instance?.Proxies.Remove(tabItem.Header?.ToString());
        }
        else if (MainTabCtrl.SelectedItem is TabItem
                 {
                     Content: TerminalView alternativeTerminalView
                 })
        {
            await alternativeTerminalView.SendCtrlC();
        }
    }

    public async Task SendCtrlCCommandToSelected(string header)
    {
        if (MainTabCtrl.SelectedItem is TabItem { Content: TerminalControl terminalControl } tabItem)
        {
            if (tabItem.Header?.ToString() != header)
            {
                return;
            }

            terminalControl.SendCtrlCCommand();
            ProxyFloatViewModel.Instance?.Proxies.Remove(tabItem.Header?.ToString());
        }
        else if (MainTabCtrl.SelectedItem is TabItem
                 {
                     Content: TerminalView alternativeTerminalView
                 })
        {
            if (alternativeTerminalView.Terminal.Title != header)
            {
                return;
            }

            await alternativeTerminalView.SendCtrlC();
        }
    }

    public async Task SendCtrlCCommandAll()
    {
        foreach (var item in MainTabCtrl.Items)
        {
            if (item is TabItem { Content: TerminalControl terminalControl })
            {
                terminalControl.SendCtrlCCommand();
            }
            else if (item is TabItem
                     {
                         Content: TerminalView alternativeTerminalView
                     })
            {
                await alternativeTerminalView.SendCtrlC();
            }
        }
    }

    private async void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        if (MainTabCtrl.SelectedItem is TabItem { Content: TerminalControl terminalControl })
        {
            await terminalControl.SendCommandAsync("clear");
        }
        else if (MainTabCtrl.SelectedItem is TabItem
                 {
                     Content: TerminalView alternativeTerminalView
                 })
        {
            await alternativeTerminalView.SendToPtyAsync("clear \r");
        }
    }

    private async void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        var index = MainTabCtrl.SelectedIndex;
        if (index >= 0 && index < MainTabCtrl.Items.Count)
        {
            // 安全释放终端资源
            if (MainTabCtrl.Items[index] is TabItem { Content: TerminalControl terminalControl })
            {
                terminalControl.SendCtrlCCommand();
                terminalControl.Dispose(); // 显式释放资源
            }
            else if (MainTabCtrl.Items[index] is TabItem
                     {
                         Content: TerminalView alternativeTerminalView
                     })
            {
                await alternativeTerminalView.SendToPtyAsync("exit \r");
                alternativeTerminalView.Terminal.Dispose();
            }

            MainTabCtrl.Items.RemoveAt(index);

            if (MainTabCtrl.Items.Count > 0)
            {
                MainTabCtrl.SelectedIndex = Math.Min(index, MainTabCtrl.Items.Count - 1);
            }
        }
    }

    private void NewConsoleButton_Click(object sender, RoutedEventArgs e) => CreateNewTerminal();

    private async void CreateNewTerminal()
    {
        try
        {
            var captchaResult = await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var iw = new InputControl(Languages.Text_Terminal_InputPrompt);
                var cd = new ContentDialog
                {
                    Title = Languages.Text_Terminal_InputTitle,
                    Content = iw,
                    PrimaryButtonText = Languages.Text_Global_Confirm,
                    DefaultButton = ContentDialogButton.Primary,
                    IsSecondaryButtonEnabled = false,
                    CloseButtonText = Languages.Text_Global_Cancel
                };
                var captchaWindow = await cd.ShowAsync(Core.App.MainWindow);

                return captchaWindow == ContentDialogResult.Primary ? iw.CaptchaResult : "cancel";
            });

            if (captchaResult.Equals("cancel", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            TabItem newTab;
            if (ConfigManager.CurrentConfig.TerminalEngineType.ToLower() == "original")
            {
                newTab = new TabItem
                {
                    Header = string.Format(Languages.Text_Terminal_ConsoleTabFormat, MainTabCtrl.Items.Count),
                    Content = new TerminalControl()
                };
            }
            else
            {
                newTab = new TabItem
                {
                    Header = string.Format(Languages.Text_Terminal_ConsoleTabFormat, MainTabCtrl.Items.Count),
                    Content = new TerminalView
                    {
                        Process = CliUtils.GetCliWithArguments(ConfigManager.CurrentConfig.TerminalCli.IsNullOrEmpty()
                            ? "powershell.exe"
                            : ConfigManager.CurrentConfig.TerminalCli),
                        FontFamily = Application.Current.TryGetResource("Jbm", out var value)
                            ? value as FontFamily
                            : new FontFamily("Consolas")
                    }
                };
            }

            MainTabCtrl.Items.Add(newTab);
            MainTabCtrl.SelectedIndex = MainTabCtrl.Items.Count - 1;

            var shell = string.IsNullOrEmpty(captchaResult)
                ? TerminalControl.GetDefaultShell()
                : captchaResult.Trim();

            // 变量替换
            var res = shell.Replace("{mefrpc}", Path.Combine(Core.App.StartupPath, "bin", "mefrpc.exe"))
                .Replace("{mefrpcp}", Path.Combine(Core.App.StartupPath, "bin"))
                .Replace("{startup}", Core.App.StartupPath);
            var isMEFrpCExe = shell.Contains("{mefrpc}");

            if (newTab.Content is TerminalControl terminal)
            {
                // 只启动一次，且确保参数正确
                terminal.StartTerminal(isMEFrpCExe ? null : res);

                if (isMEFrpCExe)
                {
                    await Task.Delay(500); // 确保终端初始化完成
                    await terminal.SendCommandAsync(res);
                }
            }
        }
        catch (Exception e)
        {
            Core.App.CurrentLogger.Error(e);
        }
    }

    public async void CreateNewTerminalWithoutNotification(string rs, string consoleTitle = "")
    {
        TabItem newTab;
        if (ConfigManager.CurrentConfig.TerminalEngineType.ToLower() == "original")
        {
            newTab = new TabItem
            {
                Header = "控制台" + MainTabCtrl.Items.Count,
                Content = new TerminalControl()
            };
        }
        else
        {
            newTab = new TabItem
            {
                Header = consoleTitle.IsNullOrEmpty()
                    ? string.Format(Languages.Text_Terminal_ConsoleTabFormat, MainTabCtrl.Items.Count)
                    : consoleTitle,
                Content = new TerminalView
                {
                    Process = CliUtils.GetCliWithArguments(ConfigManager.CurrentConfig.TerminalCli.IsNullOrEmpty()
                        ? CliUtils.GetOSSpeceficDefaultCli()
                        : ConfigManager.CurrentConfig.TerminalCli),
                    FontFamily = Application.Current.TryGetResource("Jbm", out var value)
                        ? value as FontFamily
                        : new FontFamily("Consolas")
                }
            };
        }

        MainTabCtrl.Items.Add(newTab);
        MainTabCtrl.SelectedIndex = MainTabCtrl.Items.Count - 1;
        if (OperatingSystem.IsWindows())
        {
            var res = rs.Replace("{mefrpc}",
                    (ConfigManager.CurrentConfig.TerminalCli.ToLower() is "cmd" or "cmd.exe" ? "" : "& ") +
                    $"\"{Path.Combine(Core.App.StartupPath, "bin", "mefrpc.exe")}\"")
                .Replace("{mefrpcp}", $"\"{Path.Combine(Core.App.StartupPath, "bin")}\"")
                .Replace("{startup}", Core.App.StartupPath);

            Core.App.CurrentLogger.LogDebug(res);
            var isMEFrpCExe = rs.Contains("{mefrpc}");

            if (newTab.Content is TerminalControl terminal)
            {
                if (isMEFrpCExe)
                {
                    // 修改5: 移除CurrentConhostId检查，直接发送命令

                    await terminal.SendCommandAsync(res);
                }
            }
            else if (newTab.Content is TerminalView terminal1)
            {
                if (isMEFrpCExe)
                {
                    await Task.Delay(500); // 确保终端初始化完成
                    await terminal1.SendToPtyAsync(res + " \r");
                }
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            var res = rs.Replace("{mefrpc}",
                    Path.Combine(Core.App.StartupPath, "bin", GetArchiveFileName(), "mefrpc"))
                .Replace("{mefrpcp}", Path.Combine(Core.App.StartupPath, "bin", GetArchiveFileName()))
                .Replace("{startup}", Core.App.StartupPath);

            var isMEFrpCExe = rs.Contains("{mefrpc}");


            if (newTab.Content is TerminalControl terminal)
            {
                await terminal.SendCommandAsync("cd /" + Path.Combine("opt", "pml-2"));
                await terminal.SendCommandAsync($"""
                                                echo -e "\e[33m{Languages.Text_Terminal_Unpacking}\e[0m"
                                                """);
                await terminal.SendCommandAsync("tar -xvf " +
                                                Path.Combine(Core.App.StartupPath, "bin",
                                                    "mefrpc.tar") +
                                                $" -C {Path.Combine(Core.App.StartupPath, "bin")} > /dev/null 2>&1");
                if (isMEFrpCExe)
                {
                    // 修改5: 移除CurrentConhostId检查，直接发送命令

                    Core.App.CurrentLogger.Log(res, EnumLogType.Debug);
                    await terminal.SendCommandAsync(res);
                }
            }
            else if (newTab.Content is TerminalView terminal1)
            {
                await terminal1.SendToPtyAsync("cd /" + Path.Combine("opt", "pml-2") + " \r");
                await terminal1.SendToPtyAsync($""" 
                                               echo -e "\e[33m{Languages.Text_Terminal_Unpacking}\e[0m" 
                                               """ + " \r");
                await terminal1.SendToPtyAsync("tar -xvf " +
                                               Path.Combine(Core.App.StartupPath, "bin",
                                                   "mefrpc.tar") +
                                               $" -C {Path.Combine(Core.App.StartupPath, "bin")} > /dev/null 2>&1" +
                                               " \r");
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            var res = rs.Replace("{mefrpc}",
                    Path.Combine(Core.App.StartupPath, "bin", GetArchiveFileName(), "mefrpc"))
                .Replace("{mefrpcp}", Path.Combine(Core.App.StartupPath, "bin", GetArchiveFileName()))
                .Replace("{startup}", Core.App.StartupPath);

            var isMEFrpCExe = rs.Contains("{mefrpc}");


            if (newTab.Content is TerminalControl terminal)
            {
                await terminal.SendCommandAsync("cd " + Core.App.StartupPath);
                await terminal.SendCommandAsync($"""
                                                echo -e "\e[33m{Languages.Text_Terminal_Unpacking}\e[0m"
                                                """);
                await terminal.SendCommandAsync("tar -xvf " +
                                                Path.Combine(Core.App.StartupPath, "bin",
                                                    "mefrpc.tar") +
                                                $" -C {Path.Combine(Core.App.StartupPath, "bin")} > /dev/null 2>&1");
                if (isMEFrpCExe)
                {
                    // 修改5: 移除CurrentConhostId检查，直接发送命令

                    Core.App.CurrentLogger.Log(res, EnumLogType.Debug);
                    await terminal.SendCommandAsync(res);
                }
            }
            else if (newTab.Content is TerminalView terminal1)
            {
                await terminal1.SendToPtyAsync("cd " + Core.App.StartupPath + " \r");
                await terminal1.SendToPtyAsync($""" 
                                               echo -e "\e[33m{Languages.Text_Terminal_Unpacking}\e[0m" 
                                               """ + " \r");
                await terminal1.SendToPtyAsync("tar -xvf " +
                                               Path.Combine(Core.App.StartupPath, "bin",
                                                   "mefrpc.tar") +
                                               $" -C {Path.Combine(Core.App.StartupPath, "bin")} > /dev/null 2>&1" +
                                               " \r");
            }
        }

        // 触发插件事件：代理启动
        _ = PluginService.Instance.TriggerAsync("proxy.start", new Dictionary<string, object>
        {
            ["proxyName"] = consoleTitle,
            ["command"] = rs
        });
    }

    private string GetArchiveFileName()
    {
        string platform;
        if (OperatingSystem.IsWindows())
        {
            platform = "windows";
        }
        else if (OperatingSystem.IsLinux())
        {
            platform = "linux";
        }
        else if (OperatingSystem.IsMacOS())
        {
            platform = "darwin";
        }
        else
        {
            throw new NotSupportedException("Unsupported OS");
        }

        if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
        {
            return $"mefrpc_{platform}_arm64_{Core.App.MEFrpVersion}";
        }

        if (RuntimeInformation.OSArchitecture == Architecture.X64)
        {
            return $"mefrpc_{platform}_amd64_{Core.App.MEFrpVersion}";
        }

        throw new NotSupportedException("Unsupported architecture");
    }

    private void VisitRCDoc(object sender, RoutedEventArgs e) => VisitMEFDoc(sender, e); // Reuse the same method

    private void CtrlCButton_Click(object sender, RoutedEventArgs e) => SendCtrlCCommandToSelected();
}