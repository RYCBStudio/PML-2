using System.Threading.Tasks;
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
}
