using System;
using System.Collections.Generic;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Plugin.Services;
using MEFrpLauncherX.Tools;
using MEFrpLauncherX.Views;
using ReactiveUI;

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

    // 当前导航选中项（对应 NavigationView 菜单项的 Tag），代码导航时用于同步选中指示条
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
    public ReactiveCommand<Unit, Unit> NavigateToHomeCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> NavigateToCreateProxyCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> NavigateToManageProxyCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> NavigateToNodesMonitoringCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> NavigateToUserCenterCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> NavigateToSettingsCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> NavigateToAboutCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> NavigateToTerminalCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> NavigateToUpdateCommand
    {
        get;
    }


    public ReactiveCommand<Unit, Unit> NavigateToThemeCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> NavigateToPluginCommand
    {
        get;
    }

    public bool NeedRestart
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ReactiveCommand<Unit, Unit> RestartCommand
    {
        get;
    }

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

    private ReactiveCommand<Unit, Unit> CreateNavigationCommand(string pageName, Func<UserControl> pageFactory)
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

                // 诊断：页面构造完成/失败后导出可视树，用于定位「AOT 下渲染为空白」的问题。
                if (CurrentPage is null)
                {
                    Core.App.CurrentLogger?.Log($"导航到页面 '{pageName}' 后 CurrentPage 为 null（页面构造未成功）");
                }
                else
                {
                    Dispatcher.UIThread.Post(async () =>
                    {
                        try
                        {
                            await Task.Delay(3000); // 等待过渡动画/布局完成
                            var page = CurrentPage;
                            if (page is null)
                            {
                                return;
                            }

                            var parent = page.GetVisualParent();
                            Core.App.CurrentLogger?.Log(
                                $"页面 '{pageName}' 可视树（延迟3s）:\n{Tools.VisualTreeDiagnostics.Dump(page, 8, 400)}");
                            Core.App.CurrentLogger?.Log(
                                $"页面 '{pageName}' 父级={parent?.GetType().Name ?? "(null)"} " +
                                $"父级Bounds={(parent as Control)?.Bounds} 页面Bounds={page.Bounds} " +
                                $"Content={(page as ContentControl)?.Content?.GetType().Name ?? "(null)"}");
                        }
                        catch (Exception dumpEx)
                        {
                            Core.App.CurrentLogger?.Error(dumpEx, "导出可视树失败");
                        }
                    }, DispatcherPriority.Background);
                }
            }
        });

        // ReactiveCommand 会把命令体抛出的异常转到 ThrownExceptions（默认无人订阅 => 静默），
        // 订阅后同样落盘，避免以后再次出现「页面无声消失」。
        command.ThrownExceptions.Subscribe(ex =>
            Core.App.CurrentLogger?.Error(ex, $"导航命令 '{pageName}' 执行失败"));

        return command;
    }
}