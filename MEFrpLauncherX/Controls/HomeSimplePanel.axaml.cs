using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Core.Models;
using MEFrpLauncherX.Core.Services;
using MEFrpLauncherX.Core.ViewModels;
using MEFrpLauncherX.ViewModels;
using MEFrpLauncherX.Views;

namespace MEFrpLauncherX.Controls;

/// <summary>
///     精简主页面板（26.4，<c>HomeConfig.Layout == "simple"</c> 时显示）。
///     三块：账户一行 / 我的隧道（核心列表，行内启停与复制） / 快速创建 + 为你推荐（规则引擎）。
///     数据上下文为 <see cref="HomePageViewModel" />，与经典主页共用同一套数据加载逻辑；
///     隧道行内操作不在此操作进程，全部复用管理页既有命令。
/// </summary>
public partial class HomeSimplePanel : UserControl
{
    public HomeSimplePanel()
    {
        InitializeComponent();
    }

    /// <summary>未登录时切回登录页（与主窗口装载 LoginPage 的既有流程一致）。</summary>
    private void GoToLogin(object? sender, RoutedEventArgs e)
    {
        try
        {
            MainWindowViewModel.Instance.IsLoggedIn = false;
            MainWindow.Instance.LoginBackground.IsVisible = true;
            MainWindow.Instance.MainContentControl.Content = null;
            MainWindow.Instance.MainContentControl.Content = new LoginPage();
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger?.Error(ex, "打开登录页失败");
        }
    }

    private void GoToUserCenter(object? sender, RoutedEventArgs e) =>
        MainPageFrameViewModel.Instance?.NavigateToPage("User");

    private void GoToUpdate(object? sender, RoutedEventArgs e) =>
        MainPageFrameViewModel.Instance?.NavigateToPage("Update");

    /// <summary>「管理」导航：前往隧道管理页（完整功能仍以管理页为准）。</summary>
    private void GoToManage(object? sender, RoutedEventArgs e) =>
        MainPageFrameViewModel.Instance?.NavigateToPage("Manage");

    /// <summary>空态创建入口：前往创建隧道页。</summary>
    private void GoToCreate(object? sender, RoutedEventArgs e) =>
        MainPageFrameViewModel.Instance?.NavigateToPage("Create");

    /// <summary>
    ///     「我的隧道」标题行刷新（26.4）：只重拉隧道数据并刷新列表/推荐，
    ///     不触发整页用户数据请求。
    /// </summary>
    private async void RefreshTunnels(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (DataContext is HomePageViewModel vm)
            {
                await vm.RefreshTunnelsAsync();
            }
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger?.Error(ex, "刷新我的隧道列表失败");
        }
    }

    /// <summary>
    ///     行内启停：由 ViewModel 路由到管理页既有的启动/停止命令（与「最近启动的隧道」同一路径），
    ///     面板本身不直接操作 frpc 进程。
    /// </summary>
    private void ToggleProxy(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int proxyId })
        {
            return;
        }

        (DataContext as HomePageViewModel)?.ToggleSimpleProxy(proxyId);
    }

    /// <summary>行内复制地址：规则与管理页「复制隧道信息」一致，无地址时给出提示。</summary>
    private void CopyProxyAddress(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int proxyId })
        {
            return;
        }

        (DataContext as HomePageViewModel)?.CopySimpleProxyAddress(proxyId);
    }

    /// <summary>
    ///     快速创建（26.4）：按协议预设创建页的默认协议并导航过去，
    ///     复用既有创建流程（<see cref="CreateProxyPage.RequestPreferredProtocol" />），不新开创建通道。
    /// </summary>
    private void QuickCreate(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string protocol } || string.IsNullOrWhiteSpace(protocol))
        {
            return;
        }

        try
        {
            // 26.4：先登记协议，再走既有导航（导航会构造新页面实例，静态登记值随之生效）
            CreateProxyPage.RequestPreferredProtocol(protocol);
            MainPageFrameViewModel.Instance?.NavigateToPage("Create");
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger?.Error(ex, "快速创建导航失败");
        }
    }

    /// <summary>重新计算推荐（不改变忽略记录，仅刷新显示）。</summary>
    private void RefreshRecommendations(object? sender, RoutedEventArgs e) =>
        (DataContext as HomePageViewModel)?.RefreshAllData();

    /// <summary>
    ///     执行推荐的主按钮动作。动作类型由 Core 定义（<see cref="HomeRecommendAction" />），
    ///     在此映射到具体导航或系统操作，避免 Core 依赖 UI。
    /// </summary>
    private void RunRecommendation(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: HomeRecommendKind kind })
        {
            return;
        }

        switch (kind)
        {
            case HomeRecommendKind.RecentTunnel:
                // 26.4：直接启动推荐中指定的隧道（不跳转页面）
                if (sender is Button { DataContext: HomeRecommendation { ProxyId: > 0 } rec })
                {
                    var launched = (DataContext as HomePageViewModel)?.LaunchRecentTunnel(rec.ProxyId) ?? false;
                    if (!launched)
                    {
                        Growl.Warning(Languages.Text_Home_Recommend_TunnelMissing);
                    }
                }

                break;
            case HomeRecommendKind.FailedTunnel:
            case HomeRecommendKind.StartAnyTunnel:
                MainPageFrameViewModel.Instance?.NavigateToPage("Manage");
                break;
            case HomeRecommendKind.TrafficExceeded:
            case HomeRecommendKind.TrafficLow:
                MainPageFrameViewModel.Instance?.NavigateToPage("User");
                break;
            case HomeRecommendKind.CreateTunnel:
                MainPageFrameViewModel.Instance?.NavigateToPage("Create");
                break;
            case HomeRecommendKind.UpdateAvailable:
                MainPageFrameViewModel.Instance?.NavigateToPage("Update");
                break;
            case HomeRecommendKind.ExploreNodes:
                MainPageFrameViewModel.Instance?.NavigateToPage("Monitoring");
                break;
            default:
                Services.AppShellCommands.Instance.OpenDocumentation();
                break;
        }
    }

    /// <summary>
    ///     「暂时忽略」：记录到 <c>Cache/home-recommend.json</c> 后刷新列表，
    ///     下次进入主页该条不再出现（不写入用户配置）。
    /// </summary>
    private void IgnoreRecommendation(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: HomeRecommendKind kind })
        {
            return;
        }

        HomeRecommendStateStore.Dismiss(kind);
        (DataContext as HomePageViewModel)?.RefreshRecommendations();
    }
}
