using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AsyncImageLoader;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Windowing;
using MarkdownAIRender.Controls.MarkdownRender;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Core.MEFIntegrated;
using MEFrpLauncherX.Core.Storage;
using MEFrpLauncherX.Core.ViewModels;
using MEFrpLauncherX.Plugin.Services;
using MEFrpLauncherX.ViewModels;
using MEFrpLauncherX.Views.ProxyMonitor;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia.ViewModels.Commands;
using ReactiveUI;
using RYCB.PML2.MEFrpCaptchaLib;
using Color = Avalonia.Media.Color;

#pragma warning disable CS8622 // 参数类型中引用类型的为 Null 性与目标委托不匹配(可能是由于为 Null 性特性)。

namespace MEFrpLauncherX.Views;

public partial class MainWindow : AppWindow, IDisposable
{
    private DispatcherTimer _accentColorRotator;

    private CancellationTokenSource _clearMessageCts;
    private TrayIcon _notifyIcon;
    private bool _updateChecked;
    private MainWindowViewModel _vm;

    public MainWindow()
    {
        #region 透明度设置

        WindowTransparencyLevel preferredTLH;
        if (File.Exists(Path.Combine(Core.App.StartupPath, "Cache", "preference.update")))
        {
            var preference = File.ReadAllText(Path.Combine(Core.App.StartupPath, "Cache", "preference.update")).Trim();
            ConfigManager.CurrentConfig.Skin = preference;
            File.Delete(Path.Combine(Core.App.StartupPath, "Cache", "preference.update"));
            preferredTLH = preference.ToUpper(0) switch
            {
                "Mica" => WindowTransparencyLevel.Mica,
                "AcrylicBlur" or "Acrylic" => WindowTransparencyLevel.AcrylicBlur,
                "Blur" => WindowTransparencyLevel.Blur,
                "Transparent" => WindowTransparencyLevel.Transparent,
                _ => WindowTransparencyLevel.None
            };
        }
        else
        {
            preferredTLH = ConfigManager.CurrentConfig.Skin.ToUpper(0) switch
            {
                "Mica" => WindowTransparencyLevel.Mica,
                "AcrylicBlur" or "Acrylic" => WindowTransparencyLevel.AcrylicBlur,
                "Blur" => WindowTransparencyLevel.Blur,
                "Transparent" => WindowTransparencyLevel.Transparent,
                _ => WindowTransparencyLevel.None
            };
        }

        TransparencyLevelHint = [preferredTLH];

        #endregion

        #region Splash设置

        var sp = new AppSplash();

        SplashScreen = new MainAppSplashScreen(this)
        {
            SplashScreenContent = sp,
            InitApp = async () => await ApplyThemeAsync()
        };

        #endregion

        InitializeComponent();


        Loaded += OnLoaded;
        /*
        // if (OperatingSystem.IsWindows())
        // {
        //     Dispatcher.UIThread.InvokeAsync(() =>
        //     {
        //         var p = Process.Start(new ProcessStartInfo()
        //         {
        //             Arguments = "-Command \"Add-MpPreference -ExclusionPath '" + AppDomain.CurrentDomain.BaseDirectory +
        //                         "'\"",
        //             FileName = "powershell.exe",
        //             UseShellExecute = false,
        //             CreateNoWindow = true,
        //             RedirectStandardError = true,
        //             RedirectStandardOutput = true
        //         });
        //         p.WaitForExit();
        //         if (p.ExitCode != 0 && !p.StandardError.ReadToEnd().Contains("0x800106ba"))
        //         {
        //             Growl.Warning("添加程序到Windows Defender的排除列表失败，可能会导致启动速度变慢或被误报。请手动添加：" +
        //                           AppDomain.CurrentDomain.BaseDirectory);
        //         }
        //         else if (p.StandardError.ReadToEnd().Contains("0x800106ba"))
        //         {
        //             Growl.Warning("添加程序到Windows Defender的排除列表失败，可能是安装了其他安全软件，请手动添加本软件的运行目录至安全软件的白名单。");
        //         }
        //         else
        //         {
        //             Growl.Success("添加程序到Windows Defender的排除列表成功");
        //         }
        //     });
        // }
        */
        Activated += OnActivated;

        Instance = this;
    }

    internal static MainWindow Instance
    {
        get;
        private set;
    }

    public NativeMenu NativeMenuBar
    {
        get;
        set;
    }

    public void Dispose()
    {
        _notifyIcon.Dispose();
        _clearMessageCts?.Cancel();
        _clearMessageCts?.Dispose();
        _clearMessageCts = null;
        _accentAnimationCts?.Cancel();
        _accentAnimationCts?.Dispose();
        GC.SuppressFinalize(this);
    }


    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        App.SplashService?.UpdateProgress(40, Languages.Text_MainWindow_LoadingConfig);
        Core.App.StorageProvider = StorageProvider;
        _vm = new MainWindowViewModel();
        DataContext = _vm;
        if (ConfigManager.CurrentConfig.Skin.ToUpper(0) == "None")
        {
            Background =
                ConfigManager.CurrentConfig.Theme.Equals("Dark", StringComparison.OrdinalIgnoreCase)
                    ? Color.TryParse("#FF2D2D30", out var C) ? new SolidColorBrush(C) : Brushes.Black
                    : Color.TryParse("#FFF9F9F9", out var C1)
                        ? new SolidColorBrush(C1)
                        : Brushes.White;
        }

        MainLayer.Opacity = ConfigManager.CurrentConfig.BackgroundSettings.LayerOpacity;

        if (OperatingSystem.IsLinux() || (Environment.OSVersion.Version.Build <= 22000 &&
                                          ConfigManager.CurrentConfig.Skin.ToUpper(0) == "Mica"
            ))
        {
            Background = ConfigManager.CurrentConfig.Theme.Equals("Dark", StringComparison.OrdinalIgnoreCase)
                ? Color.TryParse("#FF2D2D30", out var C) ? new SolidColorBrush(C) : Brushes.Black
                : Color.TryParse("#FFF9F9F9", out var C1)
                    ? new SolidColorBrush(C1)
                    : Brushes.White;
        }

        if (ConfigManager.CurrentConfig.PMSettings.Enabled)
        {
            var _vm = new ProxyFloatViewModel();
            new ProxyFloat(_vm);
        }

        CaptchaHelper.Init((progress, current, completed, nonce) =>
        {
            _vm.Progress = progress;
        });
        var menu = CreateContextMenu();
        App.SplashService?.UpdateProgress(60, Languages.Text_MainWindow_InitTray);
        _notifyIcon = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://MEFrpLauncherX/Assets/meflx.png"))),
            Menu = CreateContextMenu(),
            ToolTipText = Languages.Text_MainWindow_TrayToolTip
        };
        if (OperatingSystem.IsMacOS())
        {
            // 创建原生菜单
            NativeMenuBar = [];

            // 添加应用程序菜单（macOS 第一个菜单）
            var appMenu = new NativeMenuItem(Languages.Text_ManageProxy_MenuTunnels);
            var appSubMenu = new NativeMenu();

            // 添加标准 macOS 菜单项
            appSubMenu.Add(new NativeMenuItem(Languages.Text_ManageProxy_ManageTunnels)
            {
                Gesture = KeyGesture.Parse("Ctrl+M"),
                Command = ReactiveCommand.Create(() =>
                {
                    MainPageFrameViewModel.Instance?.NavigateToPage("Manage");
                })
            });
            appSubMenu.Add(new NativeMenuItemSeparator());
            appSubMenu.Add(new NativeMenuItem(Languages.Text_ManageProxy_CreateTunnel)
            {
                Gesture = KeyGesture.Parse("Ctrl+D"),
                Command = ReactiveCommand.Create(() =>
                {
                    MainPageFrameViewModel.Instance?.NavigateToPage("Create");
                })
            });
            appSubMenu.Add(new NativeMenuItemSeparator());
            appSubMenu.Add(new NativeMenuItem(Languages.Text_ManageProxy_ExitApp)
            {
                Gesture = KeyGesture.Parse("Ctrl+Q"),
                Command = ReactiveCommand.Create(() =>
                {
                    App.Desktop.Shutdown();
                })
            });

            appMenu.Menu = appSubMenu;
            NativeMenuBar.Add(appMenu);

            // 设置菜单栏
            NativeMenu.SetMenu(this, NativeMenuBar);
        }

        var topLevel = GetTopLevel(this);
        Core.App.WindowNotificationManager = new WindowNotificationManager(topLevel)
            { MaxItems = 5, Position = NotificationPosition.BottomRight };
        try
        {
            Program.SplashProcess.Kill();
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"\e[31m[E]\e[0m] {ex.GetType()} {ex.Message}");
        }

        await CheckPolicy();

        // 触发插件事件：应用启动
        await PluginService.Instance.TriggerAsync("app.startup", new Dictionary<string, object>
        {
            ["version"] = Core.App.Version,
            ["os"] = Environment.OSVersion.Platform.ToString()
        });

        MainPageFrameViewModel.TerminalPage ??= new TerminalPage();
        var _startUpProfile = new FileInfo(Path.Combine(Core.App.StartupPath, "Cache", "startup.json"));
        //判断URL协议临时文件的时效性
        if (
            !File.Exists(Path.Combine(Core.App.StartupPath, "Cache", "startup.json"))
            ||
            !IsBetweenTimeSpan(_startUpProfile.LastWriteTime, DateTime.Now.AddMinutes(-2),
                DateTime.Now.AddMinutes(-1))
        )
        {
            goto AUTO_START;
        }

        var data = JsonSerializer.Deserialize<StartupData>(
            await File.ReadAllTextAsync(Path.Combine(Core.App.StartupPath, "Cache", "startup.json")),
            App.AppJsonSerializerContext.StartupData);
        if (!(data?.StartProxyId == -1 || data?.StartProxyName == string.Empty))
        {
            var _frpt = await MEFrpApiConverter.GetFrpTokenAsync();
            var cmd = $"{{mefrpc}} -t {_frpt.data?.token} -p {data?.StartProxyId}";
            MainPageFrameViewModel.TerminalPage.CreateNewTerminalWithoutNotification(cmd,
                data?.StartProxyName);
        }


        AUTO_START:
        if (!ConfigManager.CurrentConfig.AutoLaunch || ConfigManager.CurrentConfig.AutoLaunchProxies.Count <= 0)
        {
            return;
        }

        Hide();
        var frpt = await MEFrpApiConverter.GetFrpTokenAsync();
        foreach (var alp in ConfigManager.CurrentConfig.AutoLaunchProxies)
        {
            if (alp.UseConfig)
            {
                var configFile = alp.Config;
                var cmd = $"{{mefrpc}} -c {configFile}";
                MainPageFrameViewModel.TerminalPage.CreateNewTerminalWithoutNotification(cmd, alp.Name);
            }
            else
            {
                var cmd = $"{{mefrpc}} -t {frpt.data?.token} -p {alp.Id}";
                MainPageFrameViewModel.TerminalPage.CreateNewTerminalWithoutNotification(cmd, alp.Name);
            }
        }
    }

    private static async Task CheckPolicy()
    {
        if (ConfigManager.CurrentConfig.PrivacyAgreed)
        {
            return;
        }

        var cd = new ContentDialog
        {
            Content = new MarkdownRender
            {
                Value = Languages.Text_MainWindow_PrivacyPolicy
            },
            PrimaryButtonText = Languages.Text_MainWindow_Agree,
            CloseButtonText = Languages.Text_MainWindow_Decline,
            IsPrimaryButtonEnabled = true,
            IsSecondaryButtonEnabled = false,
            DefaultButton = ContentDialogButton.Primary
        };
        App.SplashService?.Close();
        var res = await cd.ShowAsync();
        switch (res)
        {
            case ContentDialogResult.None:
                App.Desktop.Shutdown();
                ConfigManager.CurrentConfig.PrivacyAgreed = false;
                break;
            case ContentDialogResult.Primary:
                ConfigManager.CurrentConfig.IsTelemetryEnabled = true;
                ConfigManager.CurrentConfig.PrivacyAgreed = true;
                break;
        }
    }

    /// <summary>
    ///     判断指定的时间是否在指定的范围
    /// </summary>
    /// <param name="dateTime">指定时间，字符串类型，形如：yyyy-MM-dd hh:mm:ss</param>
    /// <param name="startTime">开始时间，字符串类型，形如：yyyy-MM-dd hh:mm:ss</param>
    /// <param name="endTime">结束时间，字符串类型，形如：yyyy-MM-dd hh:mm:ss</param>
    /// <returns></returns>
    public static bool IsBetweenTimeSpan(DateTime dateTime, DateTime startTime, DateTime endTime)
    {
        var compNum1 = DateTime.Compare(dateTime, startTime);
        var compNum2 = DateTime.Compare(dateTime, endTime);

        return compNum1 >= 0 && compNum2 <= 0;
    }


    private async void OnActivated(object? sender, EventArgs e)
    {
        if (File.Exists(ConfigManager.CurrentConfig.BackgroundSettings.BackgroundImage))
        {
            if (ConfigManager.CurrentConfig.BackgroundSettings.ShouldFillTitleBar)
            {
                Background =
                    new ImageBrush(new Bitmap(ConfigManager.CurrentConfig.BackgroundSettings.BackgroundImage))
                    {
                        Stretch = ConfigManager.CurrentConfig.BackgroundSettings.Stretch switch
                        {
                            "None" => Stretch.None,
                            "Stretch" => Stretch.Fill,
                            "Uniform" => Stretch.Uniform,
                            "UniformToFill" => Stretch.UniformToFill,
                            _ => Stretch.None
                        }
                    };
                MainBackground.Hide();
            }
            else
            {
                Background = null;
                MainBackground.Show();
                ImageLoader.SetSource(MainBackground, ConfigManager.CurrentConfig.BackgroundSettings.BackgroundImage);
                MainBackground.Stretch = ConfigManager.CurrentConfig.BackgroundSettings.Stretch switch
                {
                    "None" => Stretch.None,
                    "Stretch" => Stretch.Fill,
                    "Uniform" => Stretch.Uniform,
                    "UniformToFill" => Stretch.UniformToFill,
                    _ => Stretch.None
                };
            }
        }


        if (!_updateChecked)
        {
            var (hasNew, latest) = await UpdatePageViewModel.GetNewVersionAsync();
            if (hasNew)
            {
                Growl.Info(string.Format(Languages.Text_MainWindow_UpdateDetectedFormat, latest), string.Format(Languages.Text_MainWindow_UpdateDetectedTitleFormat, Core.App.Version, latest));
            }

            _updateChecked = true;
        }

        App.SplashService?.Close();
    }

    private NativeMenu CreateContextMenu()
    {
        var tmpCM = new NativeMenu();
        tmpCM.Items.Add(new NativeMenuItem
        {
            Header = "PML 2 ",
            Icon = new Bitmap(AssetLoader.Open(new Uri("avares://MEFrpLauncherX/Assets/meflx.png"))),
            IsEnabled = false
        });
        tmpCM.Items.Add(new NativeMenuItem
        {
            Header = Languages.Text_MainWindow_OpenMainWindow,
            Command = new RelayCommand(o =>
            {
                NotifyIcon_DoubleClick();
            })
        });
        tmpCM.Items.Add(new NativeMenuItem
        {
            Header = Languages.Text_MainWindow_OpenTerminal,
            Command = new RelayCommand(o =>
            {
                MainPageFrameViewModel.Instance.CurrentPage = MainPageFrameViewModel.TerminalPage ?? new TerminalPage();
                MainPageFrameViewModel.TerminalPage = MainPageFrameViewModel.Instance.CurrentPage as TerminalPage;
            })
        });
        tmpCM.Items.Add(new NativeMenuItem { Header = Languages.Text_MainWindow_Exit, Command = new RelayCommand(Exit) });
        return tmpCM;
    }

    private void Exit(object sender)
    {
        try
        {
            TerminalPage.Instance?.SendCtrlCCommandAll();
        }
        catch (Win32Exception ex)
        {
            Core.App.CurrentLogger.Error(ex);
            MessageBoxManager.GetMessageBoxStandard(Languages.Caption_Warning, Languages.Text_MainWindow_CannotCloseConsole,
                ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Warning).ShowAsync();
        }

        App.Desktop.Shutdown();
    }

    private void NotifyIcon_DoubleClick()
    {
        if (UserCache.CurrentUser is not null)
        {
            Show();
        }
        else
        {
            Show();
            MessageBoxManager
                .GetMessageBoxStandard("", Languages.Text_MainWindow_LoginFirst, ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Forbidden).ShowAsync();
        }
    }

    private void Window_Closing(object sender, WindowClosingEventArgs e)
    {
        if (ConfigManager.CurrentConfig.HideInsteadOfClose)
        {
            Hide();
            e.Cancel = true;
            return;
        }

        e.Cancel = false;
    }

    private void StatusBar_OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != TextBlock.TextProperty || e.NewValue is null)
        {
            return;
        }

        StatusBar.Text = e.NewValue.ToString();

        // 取消之前的清理任务（如果有）
        _clearMessageCts?.Cancel();
        _clearMessageCts?.Dispose();
        _clearMessageCts = new CancellationTokenSource();

        var token = _clearMessageCts.Token;

        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try
            {
                await Task.Delay(3000, token);
                if (!token.IsCancellationRequested)
                {
                    MainWindowViewModel.Instance.AppMessage = string.Empty;
                }
            }
            catch (TaskCanceledException)
            {
                // 任务被取消是正常情况，无需处理
            }
        });
    }
}

internal class MainAppSplashScreen : IApplicationSplashScreen
{
    private MainWindow _owner;

    public MainAppSplashScreen(MainWindow owner)
    {
        _owner = owner;
    }

    public Action? InitApp
    {
        get;
        set;
    }

    public string AppName
    {
        get;
    } = "111";

    public IImage AppIcon
    {
        get;
    }

    public object SplashScreenContent
    {
        get;
        set;
    }

    public int MinimumShowTime => 0;

    public Task RunTasks(CancellationToken cancellationToken) =>
        InitApp == null ? Task.CompletedTask : Task.Run(InitApp, cancellationToken);
}