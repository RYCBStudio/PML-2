using System;
using System.Reactive;
using Avalonia.Controls;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Views;
using ReactiveUI;

namespace MEFrpLauncherX.ViewModels;

public class MainPageFrameViewModel : ViewModelBase
{
    public MainPageFrameViewModel()
    {
        // 初始化命令
        NavigateToHomeCommand = CreateNavigationCommand(() => new HomePage());
        NavigateToCreateProxyCommand = CreateNavigationCommand(() => new CreateProxyPage());
        NavigateToManageProxyCommand = CreateNavigationCommand(() => new ManageProxyPage());
        NavigateToNodesMonitoringCommand = CreateNavigationCommand(() => new NodesMonitoringPage());
        NavigateToUserCenterCommand = CreateNavigationCommand(() => new UserCenterPage());
        NavigateToSettingsCommand = CreateNavigationCommand(() => new SettingsPage());
        NavigateToAboutCommand = CreateNavigationCommand(() => AboutPage ?? new AboutPage());
        NavigateToTerminalCommand = CreateNavigationCommand(() => TerminalPage ?? new TerminalPage());
        NavigateToUpdateCommand = CreateNavigationCommand(() => UpdatePage ?? new UpdatePage());
        NavigateToThemeCommand = CreateNavigationCommand(() => new ThemesPage());
        NavigateToPluginCommand = CreateNavigationCommand(() => new PluginListPage());
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

    // 静态页面实例
    public static AboutPage AboutPage
    {
        get;
        set;
    }

    public static TerminalPage? TerminalPage
    {
        get;
        set;
    }

    public static MainPageFrameViewModel? Instance
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

    private ReactiveCommand<Unit, Unit> CreateNavigationCommand(Func<UserControl> pageFactory)
    {
        return ReactiveCommand.Create(() =>
        {
            CurrentPage = null;
            try
            {
                IsLoading = true;
                var page = pageFactory();
                CurrentPage = page;
            }
            finally
            {
                IsLoading = false;
            }
        });
    }
}