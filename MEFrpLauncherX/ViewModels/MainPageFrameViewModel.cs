using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Threading;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Plugin.Services;
using MEFrpLauncherX.Tools;
using MEFrpLauncherX.Views;
using ReactiveUI;
using RxVoid = ReactiveUI.Primitives.RxVoid;

// ReSharper disable MemberCanBePrivate.Global

namespace MEFrpLauncherX.ViewModels;

public class MainPageFrameViewModel : ViewModelBase
{
    public MainPageFrameViewModel()
    {
        NeedRestart = false || Design.IsDesignMode;

        // 初始化命令
        NavigateToHomeCommand = CreateNavigationCommand("Home", () => new HomePage());
        NavigateToCreateProxyCommand = CreateNavigationCommand("CreateProxy", () => new CreateProxyPage());
        NavigateToManageProxyCommand = CreateNavigationCommand("ManageProxy",
            // 26.4：精简主页可能已预热管理页实例（隧道数据源），这里复用同一实例，
            // 既避免重复请求隧道列表，也保证主页「我的隧道」列表与管理页指向同一数据源。
            () => ManageProxyPage.Instance ?? new ManageProxyPage());
        NavigateToNodesMonitoringCommand = CreateNavigationCommand("NodesMonitoring", () => new NodesMonitoringPage());
        NavigateToUserCenterCommand = CreateNavigationCommand("UserCenter", () => new UserCenterPage());
        NavigateToSettingsCommand = CreateNavigationCommand("Settings", () => new SettingsPage());
        NavigateToAboutCommand = CreateNavigationCommand("About", () => AboutPage ?? new AboutPage());
        NavigateToTerminalCommand = CreateNavigationCommand("Terminal", () => TerminalPage ?? new TerminalPage());
        NavigateToUpdateCommand = CreateNavigationCommand("Update", () => UpdatePage ?? new UpdatePage());
        NavigateToThemeCommand = CreateNavigationCommand("Theme", () => new ThemesPage());
        NavigateToPluginCommand = CreateNavigationCommand("Plugin", () => new PluginListPage());

        RestartCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            try
            {
                await DesktopUtils.RestartAsync();
            }
            catch (Exception ex)
            {
                Core.App.CurrentLogger.Error(ex);
                Environment.Exit(0); // 最简化的强制退出
            }
        });
    }

    public bool IsMenuOpen
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = true;


    public UserControl CurrentPage
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = new HomePage();

    // 当前导航选中项（对应 FANavigationView 菜单项的 Tag），代码导航时用于同步选中指示条
    public string SelectedTag
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "Home";

    public bool IsLoading
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    // 页面命令
    public ReactiveCommand<RxVoid, RxVoid> NavigateToHomeCommand
    {
        get;
    }

    public ReactiveCommand<RxVoid, RxVoid> NavigateToCreateProxyCommand
    {
        get;
    }

    public ReactiveCommand<RxVoid, RxVoid> NavigateToManageProxyCommand
    {
        get;
    }

    public ReactiveCommand<RxVoid, RxVoid> NavigateToNodesMonitoringCommand
    {
        get;
    }

    public ReactiveCommand<RxVoid, RxVoid> NavigateToUserCenterCommand
    {
        get;
    }

    public ReactiveCommand<RxVoid, RxVoid> NavigateToSettingsCommand
    {
        get;
    }

    public ReactiveCommand<RxVoid, RxVoid> NavigateToAboutCommand
    {
        get;
    }

    public ReactiveCommand<RxVoid, RxVoid> NavigateToTerminalCommand
    {
        get;
    }

    public ReactiveCommand<RxVoid, RxVoid> NavigateToUpdateCommand
    {
        get;
    }


    public ReactiveCommand<RxVoid, RxVoid> NavigateToThemeCommand
    {
        get;
    }

    public ReactiveCommand<RxVoid, RxVoid> NavigateToPluginCommand
    {
        get;
    }

    public bool NeedRestart
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ReactiveCommand<RxVoid, RxVoid> RestartCommand
    {
        get;
    }

    // 主界面尚未创建（用户未登录）时挂起的「切换到终端页」请求
    private static bool _pendingTerminalNavigation;

    // 静态页面实例
    public static AboutPage? AboutPage
    {
        get;
        set;
    }

    public static TerminalPage? TerminalPage
    {
        get;
        set;
    }

    public static MainPageFrameViewModel Instance
    {
        get;
        set;
    }

    public static UpdatePage? UpdatePage
    {
        get;
        set;
    }

    /// <summary>
    ///     请求切换到终端页（26.4 修复）。
    ///     <para>
    ///         PTY 终端进程只在控件 <c>Loaded</c>（即页面可见）时才启动，而
    ///         <c>TerminalView.SendToPtyAsync</c> 在页面不可见时不会主动拉起进程，
    ///         只会一直等待（上游注释说明是等「页面可见」），因此从链接启动隧道时
    ///         若不切到终端页，命令会一直挂起，直到用户手动切过去才发出。
    ///     </para>
    ///     <para>
    ///         与手动启动隧道的路径保持一致（<c>UserProxyViewModel.LaunchViaConfigImpl</c>
    ///         同样是「建 Tab → NavigateToPage("Terminal")」）。
    ///     </para>
    /// </summary>
    /// <remarks>
    ///     冷启动场景下本方法可能在用户登录之前被调用（主界面 <see cref="Instance" />
    ///     尚未创建），此时只记录请求，待主界面构造完成后自动补做导航。
    /// </remarks>
    public static void RequestTerminalNavigation()
    {
        if (Instance is null)
        {
            // 用户尚未登录、主界面未创建：挂起请求，登录后由构造函数补做
            _pendingTerminalNavigation = true;
            return;
        }

        // 导航必须在 UI 线程上执行（可能来自命名管道的后台线程）
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => RequestTerminalNavigation());
            return;
        }

        _pendingTerminalNavigation = false;
        Instance.NavigateToPage("Terminal");
    }

    /// <summary>
    ///     消费挂起的终端页导航请求。仅由 <see cref="Views.MainPageFrame" /> 构造完成后调用。
    /// </summary>
    internal static void ConsumePendingTerminalNavigation()
    {
        if (!_pendingTerminalNavigation)
        {
            return;
        }

        _pendingTerminalNavigation = false;
        Instance?.NavigateToPage("Terminal");
    }

    public void NavigateToPage(object pageName)
    {
        switch (pageName)
        {
            case "Home":
                NavigateToHomeCommand.Execute().Subscribe();
                SelectedTag = "Home";
                break;
            case "Create":
                NavigateToCreateProxyCommand.Execute().Subscribe();
                SelectedTag = "Create";
                break;
            case "Manage":
                NavigateToManageProxyCommand.Execute().Subscribe();
                SelectedTag = "Manage";
                break;
            case "User":
                NavigateToUserCenterCommand.Execute().Subscribe();
                SelectedTag = "User";
                break;
            case "Monitoring":
                NavigateToNodesMonitoringCommand.Execute().Subscribe();
                SelectedTag = "Monitoring";
                break;
            case "Settings":
                NavigateToSettingsCommand.Execute().Subscribe();
                SelectedTag = "Settings";
                break;
            case "Terminal":
                NavigateToTerminalCommand.Execute().Subscribe();
                SelectedTag = "Terminal";
                break;
            case "About":
                NavigateToAboutCommand.Execute().Subscribe();
                SelectedTag = "About";
                break;
            case "Update":
                NavigateToUpdateCommand.Execute().Subscribe();
                SelectedTag = "Update";
                break;
            case "Theme":
                NavigateToThemeCommand.Execute().Subscribe();
                SelectedTag = "Theme";
                break;
            case "Plugin":
                NavigateToPluginCommand.Execute().Subscribe();
                SelectedTag = "Plugin";
                break;
            default:
                NavigateToHomeCommand.Execute().Subscribe();
                SelectedTag = "Home";
                break;
        }
    }

    private ReactiveCommand<RxVoid, RxVoid> CreateNavigationCommand(string pageName, Func<UserControl> pageFactory)
    {
        var command = ReactiveCommand.Create(() =>
        {
            CurrentPage = null;
            try
            {
                IsLoading = true;
                var page = pageFactory();
                CurrentPage = page;

                // 触发插件事件：页面导航
                _ = PluginService.Instance.TriggerAsync("page.navigate", new Dictionary<string, object>
                {
                    ["page"] = pageName
                });
            }
            catch (Exception ex)
            {
                // 页面构造失败时以前会静默吞掉异常，表现为「整页空白/消失」且无任何日志。
                // 这里显式记录，保证任何平台（含 AOT 裁剪）都能定位到根因。
                Core.App.CurrentLogger?.Error(ex, $"导航到页面 '{pageName}' 失败，页面将保持空白");
            }
            finally
            {
                IsLoading = false;
            }
        });

        // ReactiveCommand 会把命令体抛出的异常转到 ThrownExceptions（默认无人订阅 => 静默），
        // 订阅后同样落盘，避免以后再次出现「页面无声消失」。
        command.ThrownExceptions.Subscribe(ex =>
            Core.App.CurrentLogger?.Error(ex, $"导航命令 '{pageName}' 执行失败"));

        return command;
    }
}