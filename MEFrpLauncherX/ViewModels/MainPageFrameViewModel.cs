using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reactive;
using Avalonia.Controls;
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
        NavigateToManageProxyCommand = CreateNavigationCommand("ManageProxy", () => new ManageProxyPage());
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
                break;
            case "Create":
                NavigateToCreateProxyCommand.Execute().Subscribe();
                break;
            case "Manage":
                NavigateToManageProxyCommand.Execute().Subscribe();
                break;
            case "User":
                NavigateToUserCenterCommand.Execute().Subscribe();
                break;
            case "Monitoring":
                NavigateToNodesMonitoringCommand.Execute().Subscribe();
                break;
            case "Settings":
                NavigateToSettingsCommand.Execute().Subscribe();
                break;
            case "Terminal":
                NavigateToTerminalCommand.Execute().Subscribe();
                break;
            case "About":
                NavigateToAboutCommand.Execute().Subscribe();
                break;
            case "Update":
                NavigateToUpdateCommand.Execute().Subscribe();
                break;
            case "Theme":
                NavigateToThemeCommand.Execute().Subscribe();
                break;
            case "Plugin":
                NavigateToPluginCommand.Execute().Subscribe();
                break;
            default:
                NavigateToHomeCommand.Execute().Subscribe();
                break;
        }
    }

    private ReactiveCommand<Unit, Unit> CreateNavigationCommand(string pageName, Func<UserControl> pageFactory)
    {
        return ReactiveCommand.Create(() =>
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
            finally
            {
                IsLoading = false;
            }
        });
    }
}