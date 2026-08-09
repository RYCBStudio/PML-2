using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaEdit.Utils;
using FluentAvalonia.UI.Controls;
using MEFrpLauncherX.Controls;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Analysis;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Core.MEFIntegrated;
using MEFrpLauncherX.Core.Storage;
using MEFrpLauncherX.Core.ViewModels;
using MEFrpLauncherX.Plugin.Services;
using MEFrpLauncherX.ViewModels;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using RYCB.PML2.MEFrpCaptchaLib;

namespace MEFrpLauncherX.Views;

public partial class LoginPage : UserControl
{
    private const string nil = "nil";

    private readonly LoginViewModel _loginViewModel;
    private bool _autoLogin;

    public LoginPage()
    {
        InitializeComponent();
        _autoLogin = ConfigManager.CurrentConfig.AutoLogin;
        AutoLoginSwitch.IsChecked = _autoLogin;
        _loginViewModel = new LoginViewModel();

        // 当用户从下拉选择已存账号（非"<使用新账号>"占位项）时，直接使用本地存储的 token 登录并跳转
        var __os = _loginViewModel
            .WhenAnyValue(x => x.SelectedStoredIndex)
            .Where(idx => idx > 0);
        ExtensionMethods.Subscribe(__os, idx =>
        {
            _ = TryLocalLoginAsync(idx);
            _autoLogin = true;
        });
        DataContext = _loginViewModel;
    }

    private async Task TryLocalLoginAsync(int selectedIndex)
    {
        try
        {
            var username = _loginViewModel.SelectedStoredUsername.IsNullOrEmpty()
                ? UsrNameBox.Text
                : _loginViewModel.SelectedStoredUsername;
            if (string.IsNullOrEmpty(username) || AutoLoginSwitch.IsChecked != true)
            {
                return;
            }

            var stored = UserCache.GetUserInfo(username);
            if (stored == null || !_autoLogin)
            {
                return;
            }

            // Use the stored user info to set current user and navigate to main page
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                MEFrpApiConverter.CurrentUserInfo = new InfoClasses.ApiInfo<InfoClasses.UserInfo>
                {
                    data = stored,
                    code = 200,
                    message = "本地已存登录"
                };

                UserCache.CurrentUser = stored;

                if (stored.Email.IsNullOrEmpty() == false)
                {
                    AppAnalytics.SetUserId(DeviceIdHelper.GetDeviceUniqueId(), stored.username, stored.Email);
                }

                _loginViewModel.RefreshStoredUsernames();
                MainWindow.Instance.LoginBackground.IsVisible = false;
                MainWindowViewModel.Instance.IsLoggedIn = true;
                MainWindow.Instance.MainContentControl.Content = null;
                MainWindow.Instance.MainContentControl.Content = new MainPageFrame();
            });
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger.Error(ex);
        }
    }

    public static async Task<string> GetCaptchaResultAsync()
    {
        if (ConfigManager.CurrentConfig.CaptchaMode == "explicit" ||
            ConfigManager.CurrentConfig.CaptchaMode.Equals("browser", StringComparison.CurrentCultureIgnoreCase))
        {
            await Core.App.MainWindow.Launcher.LaunchUriAsync(
                new Uri("https://www.mefrp.com/3rdparty/captcha?client=PML%202"));
            var cd = new ContentDialog
            {
                Title = "请输入验证码",
                PrimaryButtonText = Languages.Text_Global_Confirm,
                DefaultButton = ContentDialogButton.Primary,
                IsSecondaryButtonEnabled = false,
                CloseButtonText = Languages.Text_Global_Cancel
            };
            var input = new TextBox();
            cd.Content = input;
            return await cd.ShowAsync() == ContentDialogResult.Primary
                ? MEFrpApiConverter.GetCaptchaResult(input.Text).Split("||")[0]
                : nil;
        }

        MainWindowViewModel.Instance.AppMessage = "正在人机验证 步骤1/5";
        var c = CaptchaHelper.GetChallengeContent();
        MainWindowViewModel.Instance.Progress = 20.0;

        MainWindowViewModel.Instance.AppMessage = "正在人机验证 步骤2/5";
        var ci = await MEFrpApiConverter.PostChallengeAsync(JsonSerializer.Serialize(c,
            App.AppJsonSerializerContext.ChallengeInfo));

        MainWindowViewModel.Instance.Progress = 40.0;
        MainWindowViewModel.Instance.AppMessage = "正在人机验证 步骤3/5";
        var (rb, err) = await CaptchaHelper.GetRedeemBody(ci);

        MainWindowViewModel.Instance.Progress = 60.0;
        if (err != nil && rb is null)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                MessageBoxManager
                    .GetMessageBoxStandard("验证失败", err, icon: Icon.Error)
                    .ShowAsync());
            return string.Empty;
        }

        MainWindowViewModel.Instance.AppMessage = "正在人机验证 步骤4/5";
        var (ri, _err) =
            await MEFrpApiConverter.GetRedeemAsync(
                JsonSerializer.Serialize(rb, App.AppJsonSerializerContext.RedeemInfo));

        MainWindowViewModel.Instance.Progress = 80.0;
        if (!ri.success)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                MessageBoxManager
                    .GetMessageBoxStandard("验证失败", _err, icon: Icon.Error)
                    .ShowAsync());
            return string.Empty;
        }

        // 1. 获取验证码
        Core.App.CurrentLogger.Log("开始获取验证码...");
        MainWindowViewModel.Instance.AppMessage = "正在人机验证 步骤5/5";
        MainWindowViewModel.Instance.Progress = 90.0;
        var captchaResult = CaptchaHelper.GetCaptchaCode(ri);
        MainWindowViewModel.Instance.Progress = 100.0;
        Core.App.CurrentLogger.Log("获取验证码成功");
        return captchaResult;
    }

    private async void LoginBtn_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(UsrNameBox.Text) || string.IsNullOrEmpty(PwdBox.Text))
        {
            return;
        }

        try
        {
            // ComboBox does not have IsReadOnly in the same way as TextBox; disable during login
            UsrNameBox.IsEnabled = false;
            PwdBox.IsReadOnly = true;
            LoginBtn.IsEnabled = false;
            LoginBtn.Content = new LoadingTip("登录中");
            (sender as Control)?.IsEnabled = false;
            CaptchaHelper.Init((progress, current, completed, nonce) =>
            {
                _loginViewModel.Progress = progress;
            });

            var captchaResult = await GetCaptchaResultAsync();

            // 4. 执行登录
            Core.App.CurrentLogger.Log("开始登录流程...");

            var usr = UsrNameBox.Text;
            var pwd = PwdBox.Text;

            var (success, message) = MEFrpApiConverter.SendLoginInfo(usr, pwd, captchaResult.Trim());

            Core.App.CurrentLogger.LogDebug($"API响应: {success}, {message}");

            if (success)
            {
                var userInfo =
                    JsonSerializer.Deserialize<InfoClasses.ApiInfo<InfoClasses.UserInfo>>(message,
                        App.AppJsonSerializerContext.ApiInfoUserInfo);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    MEFrpApiConverter.CurrentUserInfo = userInfo;
                    UserCache.CurrentUser = new InfoClasses.UserInfo
                    {
                        username = userInfo?.data?.username,
                        token = userInfo.data.token,
                        group = userInfo.data.group
                    };
                    _loginViewModel.RefreshStoredUsernames();
                    MainWindow.Instance.LoginBackground.IsVisible = false;
                    MainWindowViewModel.Instance.IsLoggedIn = true;
                    MainWindow.Instance.MainContentControl.Content = null;
                    MainWindow.Instance.MainContentControl.Content = new MainPageFrame();

                    // 触发插件事件：用户登录
                    _ = PluginService.Instance.TriggerAsync("user.login", new Dictionary<string, object>
                    {
                        ["username"] = userInfo?.data?.username ?? "",
                        ["group"] = userInfo?.data?.group ?? ""
                    });
                });
            }
            else
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                    MessageBoxManager
                        .GetMessageBoxStandard("登录失败", message, icon: Icon.Error)
                        .ShowAsync());
                LoginBtn.Content = "登录";
            }
        }
        catch (OperationCanceledException)
        {
            // 用户取消了操作
            Core.App.CurrentLogger.Log("用户取消操作");
        }

        catch (Exception ex)
        {
            Core.App.CurrentLogger.Error(ex);
            await MessageBoxManager
                .GetMessageBoxStandard(Languages.Caption_Error, $"验证失败: {ex.Message}", icon: Icon.Error)
                .ShowAsync();
        }
        finally
        {
            LoginBtn.IsEnabled = true;
            UsrNameBox.IsEnabled = true;
            PwdBox.IsReadOnly = false;
        }
    }


    private async void Control_OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (Design.IsDesignMode)
        {
            return;
        }

        // 情况1：已登录且启用自动登录 → 直接跳转主页
        if (UserCache.IsLoggedIn() && ConfigManager.CurrentConfig.AutoLogin)
        {
            MainWindowViewModel.Instance.IsLoggedIn = true;
            MainWindow.Instance.LoginBackground.IsVisible = false;
            var currentUser = UserCache.CurrentUser;

            if (currentUser?.Email.IsNullOrEmpty() == false)
            {
                AppAnalytics.SetUserId(DeviceIdHelper.GetDeviceUniqueId(), currentUser.username, currentUser.Email);
            }

            Core.App.CurrentLogger.Log($"用户: {currentUser.username}, 组: {currentUser.group}");
            MainWindow.Instance.MainContentControl.Content = null;
            MainWindow.Instance.MainContentControl.Content = new MainPageFrame();
            return;
        }

        // 情况2：未登录但启用自动登录且有已存储用户 → 自动选择第一个用户并登录
        if (!UserCache.IsLoggedIn() && ConfigManager.CurrentConfig.AutoLogin &&
            _loginViewModel.StoredUsernames.Count >= 2)
        {
            await TryLocalLoginAsync(1);
        }
    }

    private void PassWordOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            LoginBtn_Click(sender, e);
        }
    }

    private void SignUpBtn_OnClick(object? sender, RoutedEventArgs e) =>
        Core.Extensions.OpenUrl("https://www.mefrp.com/auth/register");

    private void AutoLogin(object? sender, RoutedEventArgs e)
    {
        ConfigManager.UpdateConfig(cfg =>
            cfg.AutoLogin = AutoLoginSwitch.IsChecked ?? false);
    }
}