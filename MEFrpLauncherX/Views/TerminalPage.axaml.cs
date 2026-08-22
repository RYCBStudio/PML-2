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
            // 26.3 M6b-Extended：隧道停止 → 悬浮窗移除该项
            ProxyFloatViewModel.ReportTunnelRemoved(tabItem.Header?.ToString());
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
        // 按标签头查找对应标签（隧道管理页点「停止」时当前选中项可能不是目标标签，不能依赖 SelectedItem）
        foreach (var item in MainTabCtrl.Items)
        {
            if (item is not TabItem { } tabItem || tabItem.Header?.ToString() != header)
            {
                continue;
            }

            if (tabItem.Content is TerminalControl terminalControl)
            {
                terminalControl.SendCtrlCCommand();
                // 26.3 M6b-Extended：隧道停止 → 悬浮窗移除该项
                ProxyFloatViewModel.ReportTunnelRemoved(tabItem.Header?.ToString());
                return;
            }

            if (tabItem.Content is TerminalView alternativeTerminalView)
            {
                await alternativeTerminalView.SendCtrlC();
                // 26.3 M6b-Extended：隧道停止 → 悬浮窗移除该项（PTY 引擎）
                ProxyFloatViewModel.ReportTunnelRemoved(header);
                return;
            }
        }
    }

    public async Task SendCtrlCCommandAll()
    {
        foreach (var item in MainTabCtrl.Items)
        {
            if (item is TabItem { Content: TerminalControl terminalControl } tabItem)
            {
                terminalControl.SendCtrlCCommand();
                // 26.3 M6b-Extended：全部停止 → 悬浮窗移除对应项
                ProxyFloatViewModel.ReportTunnelRemoved(tabItem.Header?.ToString());
            }
            else if (item is TabItem
                     {
                         Content: TerminalView alternativeTerminalView
                     })
            {
                await alternativeTerminalView.SendCtrlC();
                // 26.3 M6b-Extended：全部停止 → 悬浮窗移除对应项（PTY 引擎）
                ProxyFloatViewModel.ReportTunnelRemoved(alternativeTerminalView.Terminal.Title);
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
            await alternativeTerminalView.SendToPtyAsync(OperatingSystem.IsWindows() ? "clear \r" : "clear\n");
        }
    }

    private async void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        var index = MainTabCtrl.SelectedIndex;
        if (index >= 0 && index < MainTabCtrl.Items.Count)
        {
            var closedTabName = (MainTabCtrl.Items[index] as TabItem)?.Header?.ToString() ?? "";

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
                await alternativeTerminalView.SendToPtyAsync(OperatingSystem.IsWindows() ? "exit \r" : "exit\n");
                alternativeTerminalView.Terminal.Dispose();
            }

            MainTabCtrl.Items.RemoveAt(index);

            if (MainTabCtrl.Items.Count > 0)
            {
                MainTabCtrl.SelectedIndex = Math.Min(index, MainTabCtrl.Items.Count - 1);
            }

            // 26.3 M6b-Extended：隧道标签关闭 → 悬浮窗移除该项
            ProxyFloatViewModel.ReportTunnelRemoved(closedTabName);

            // 触发插件事件：代理停止
            _ = PluginService.Instance.TriggerAsync("proxy.stop", new Dictionary<string, object>
            {
                ["proxyName"] = closedTabName
            });
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

            // 变量替换（客户端路径按平台生成；macOS/Linux 路径可能含空格，加引号包裹）
            var res = shell.Replace("{mefrpc}", OperatingSystem.IsWindows()
                    ? Path.Combine(Core.App.StartupPath, "bin", "mefrpc.exe")
                    : '"' + Path.Combine(Core.App.StartupPath, "bin", GetArchiveFileName(), "mefrpc") + '"')
                .Replace("{mefrpcp}", OperatingSystem.IsWindows()
                    ? Path.Combine(Core.App.StartupPath, "bin")
                    : '"' + Path.Combine(Core.App.StartupPath, "bin", GetArchiveFileName()) + '"')
                .Replace("{startup}", OperatingSystem.IsWindows()
                    ? Core.App.StartupPath
                    : '"' + Core.App.StartupPath + '"');
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

    public async void CreateNewTerminalWithoutNotification(string rs, string consoleTitle = "",
        Action<string>? onOutput = null)
    {
        TabItem newTab;
        if (ConfigManager.CurrentConfig.TerminalEngineType.ToLower() == "original")
        {
            newTab = new TabItem
            {
                Header = consoleTitle.IsNullOrEmpty()
                    ? string.Format(Languages.Text_Terminal_ConsoleTabFormat, MainTabCtrl.Items.Count)
                    : consoleTitle,
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

        // 26.3 M3: 可选订阅程序输出（Original=TerminalControl / PTY=TerminalView 两引擎都要接）
        if (onOutput != null)
        {
            switch (newTab.Content)
            {
                case TerminalControl tc:
                    tc.OutputReceived += onOutput;
                    break;
                case TerminalView tv:
                    tv.OutputReceived += onOutput;
                    break;
            }
        }

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
            var res = rs.Replace("{mefrpc}", '"' +
                    Path.Combine(Core.App.StartupPath, "bin", GetArchiveFileName(), "mefrpc") + '"')
                .Replace("{mefrpcp}", Path.Combine(Core.App.StartupPath, "bin", GetArchiveFileName()))
                .Replace("{startup}", Core.App.StartupPath);

            var isMEFrpCExe = rs.Contains("{mefrpc}");


            if (newTab.Content is TerminalControl terminal)
            {
                await terminal.SendCommandAsync("cd /" + Path.Combine("opt", "pml-2"));
                await terminal.SendCommandAsync($"""
                                                 echo -e "\e[33m{Languages.Text_Terminal_Unpacking}\e[0m"
                                                 """);
                await terminal.SendCommandAsync("tar -xvf \"" +
                                                Path.Combine(Core.App.StartupPath, "bin",
                                                    "mefrpc.tar") +
                                                $"\" -C \"{Path.Combine(Core.App.StartupPath, "bin")}\" > /dev/null 2>&1");
                if (isMEFrpCExe)
                {
                    // 修改5: 移除CurrentConhostId检查，直接发送命令

                    Core.App.CurrentLogger.Log(res, EnumLogType.Debug);
                    await terminal.SendCommandAsync(res);
                }
            }
            else if (newTab.Content is TerminalView terminal1)
            {
                await terminal1.SendToPtyAsync("cd /" + Path.Combine("opt", "pml-2") + "\n");
                await terminal1.SendToPtyAsync($""" 
                                                echo -e "\e[33m{Languages.Text_Terminal_Unpacking}\e[0m" 
                                                """ + "\n");
                await terminal1.SendToPtyAsync("tar -xvf \"" +
                                               Path.Combine(Core.App.StartupPath, "bin",
                                                   "mefrpc.tar") +
                                               $"\" -C \"{Path.Combine(Core.App.StartupPath, "bin")}\" > /dev/null 2>&1" +
                                               "\n");
                if (isMEFrpCExe)
                {
                    // 修改5: 移除CurrentConhostId检查，直接发送命令
                    Core.App.CurrentLogger.Log(res, EnumLogType.Debug);
                    await terminal1.SendToPtyAsync(res + "\n");
                }
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            // macOS 应用路径含空格（如 /Applications/PML 2.app/...），必须加引号包裹
            var res = rs.Replace("{mefrpc}",
                    '"' + Path.Combine(Core.App.StartupPath, "bin", GetArchiveFileName(), "mefrpc") + '"')
                .Replace("{mefrpcp}", '"' + Path.Combine(Core.App.StartupPath, "bin", GetArchiveFileName()) + '"')
                .Replace("{startup}", '"' + Core.App.StartupPath + '"');

            var isMEFrpCExe = rs.Contains("{mefrpc}");


            if (newTab.Content is TerminalControl terminal)
            {
                await terminal.SendCommandAsync("cd \"" + Core.App.StartupPath + '"');
                await terminal.SendCommandAsync($"""
                                                 echo -e "\e[33m{Languages.Text_Terminal_Unpacking}\e[0m"
                                                 """);
                await terminal.SendCommandAsync("tar -xvf \"" +
                                                Path.Combine(Core.App.StartupPath, "bin",
                                                    "mefrpc.tar") +
                                                $"\" -C \"{Path.Combine(Core.App.StartupPath, "bin")}\" > /dev/null 2>&1");
                if (isMEFrpCExe)
                {
                    // 修改5: 移除CurrentConhostId检查，直接发送命令

                    Core.App.CurrentLogger.Log(res, EnumLogType.Debug);
                    await terminal.SendCommandAsync(res);
                }
            }
            else if (newTab.Content is TerminalView terminal1)
            {
                await terminal1.SendToPtyAsync("cd \"" + Core.App.StartupPath + "\"\n");
                await terminal1.SendToPtyAsync($""" 
                                                echo -e "\e[33m{Languages.Text_Terminal_Unpacking}\e[0m" 
                                                """ + "\n");
                await terminal1.SendToPtyAsync("tar -xvf \"" +
                                               Path.Combine(Core.App.StartupPath, "bin",
                                                   "mefrpc.tar") +
                                               $"\" -C \"{Path.Combine(Core.App.StartupPath, "bin")}\" > /dev/null 2>&1" +
                                               "\n");
                if (isMEFrpCExe)
                {
                    // 修改5: 移除CurrentConhostId检查，直接发送命令
                    Core.App.CurrentLogger.Log(res, EnumLogType.Debug);
                    await terminal1.SendToPtyAsync(res + "\n");
                }
            }
        }

        // 触发插件事件：代理启动
        await PluginService.Instance.TriggerAsync("proxy.start", new Dictionary<string, object>
        {
            ["proxyName"] = consoleTitle
        }).ConfigureAwait(false);

        // 26.3 M6b-Extended：隧道启动 → 悬浮窗状态同步（仅具名隧道，手动终端不进入）
        if (!consoleTitle.IsNullOrEmpty())
        {
            ProxyFloatViewModel.ReportTunnelStarted(consoleTitle);
        }
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