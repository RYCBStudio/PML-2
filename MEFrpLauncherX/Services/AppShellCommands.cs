using System;
using System.Diagnostics;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Threading;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.ViewModels;
using MEFrpLauncherX.Views;
using MEFrpLauncherX.Views.ProxyMonitor;

namespace MEFrpLauncherX.Services;

/// <summary>
///     应用外壳共享命令：系统托盘、macOS 原生菜单、流量悬浮窗统一从这里取命令，
///     避免各处复制业务逻辑（26.3 跨阶段约定：M5 / M6 / M6b 只绑定，不复制业务）。
/// </summary>
public interface IAppShellCommands
{
    /// <summary>显示并激活主窗口（未登录时保持现有提示语义）</summary>
    void ShowMainWindow();

    /// <summary>停止全部隧道后退出应用（含 Ctrl+C 失败提示）</summary>
    void ExitApplication();

    /// <summary>导航到设置页并显示主窗口</summary>
    void OpenSettings();

    /// <summary>向所有终端发送 Ctrl+C，停止全部隧道</summary>
    Task StopAllTunnelsAsync();

    /// <summary>刷新流量悬浮窗统计</summary>
    void RefreshFloatTraffic();

    // ---- 26.3.1 M3：macOS 菜单主路径命令（与托盘/主界面同源）----

    /// <summary>打开日志目录（跨平台文件管理器）</summary>
    void OpenLogDirectory();

    /// <summary>刷新隧道列表</summary>
    Task RefreshProxiesAsync();

    /// <summary>打开节点监控页（重新进入即重新加载数据）</summary>
    void ShowNodesMonitoring();

    /// <summary>显示 / 隐藏流量悬浮窗</summary>
    void ToggleProxyFloat();

    /// <summary>打开官方文档站点</summary>
    void OpenDocumentation();

    /// <summary>导航到更新页并触发检查更新</summary>
    Task CheckUpdateAsync();
}

/// <summary>
///     默认实现：全部委托给 <see cref="MainWindow" /> / 终端页 / 悬浮窗 VM 的既有方法。
/// </summary>
public sealed class AppShellCommands : IAppShellCommands
{
    /// <summary>全局单例</summary>
    public static IAppShellCommands Instance { get; } = new AppShellCommands();

    private AppShellCommands()
    {
    }

    public void ShowMainWindow() => MainWindow.Instance.NotifyIcon_DoubleClick();

    public void ExitApplication() => MainWindow.Instance.ExitApplication();

    public void OpenSettings()
    {
        MainPageFrameViewModel.Instance?.NavigateToPage("Settings");
        MainWindow.Instance.Show();
    }

    public async Task StopAllTunnelsAsync()
    {
        if (TerminalPage.Instance is { } terminalPage)
        {
            await terminalPage.SendCtrlCCommandAll();
        }
    }

    public void RefreshFloatTraffic() => ProxyFloatViewModel.Instance?.RefreshTraffic();

    public void OpenLogDirectory()
    {
        try
        {
            var logDir = Path.Combine(Core.App.StartupPath, "Logs");
            Directory.CreateDirectory(logDir);
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo("explorer.exe", logDir) { UseShellExecute = true });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", logDir);
            }
            else
            {
                Process.Start("xdg-open", logDir);
            }
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger?.Error(ex, "打开日志目录失败");
        }
    }

    public async Task RefreshProxiesAsync()
    {
        if (ManageProxyPage.Instance is { } page)
        {
            await page.LoadProxies();
        }
    }

    public void ShowNodesMonitoring() =>
        MainPageFrameViewModel.Instance?.NavigateToPage("Monitoring");

    public void ToggleProxyFloat()
    {
        if (ProxyFloat.Instance is { IsVisible: true } floatWindow)
        {
            floatWindow.Close();
        }
        else
        {
            ProxyFloat.Instance ??= new ProxyFloat();
            ProxyFloat.Instance.Show();
        }
    }

    public void OpenDocumentation()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://docs.rycb.tech/",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger?.Error(ex, "打开文档失败");
        }
    }

    public async Task CheckUpdateAsync()
    {
        MainPageFrameViewModel.Instance?.NavigateToPage("Update");
        if (MainPageFrameViewModel.UpdatePage?.DataContext is UpdatePageViewModel vm)
        {
            await Dispatcher.UIThread.InvokeAsync(() => vm.CheckUpdateCommand.Execute().Subscribe());
        }
    }
}
