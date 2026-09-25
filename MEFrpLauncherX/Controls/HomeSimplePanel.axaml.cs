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
///     三块：用户与额度 / 系统状态 / 为你推荐（规则引擎）。
///     数据上下文为 <see cref="HomePageViewModel" />，与经典主页共用同一套数据加载逻辑。
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
