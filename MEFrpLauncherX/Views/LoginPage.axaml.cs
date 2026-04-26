using System;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using MEFrpLauncherX.Controls;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Analysis;
using MEFrpLauncherX.Core.MEFIntergrated;
using MEFrpLauncherX.Core.Storage;
using MEFrpLauncherX.ViewModels;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using RYCB.PML.MEFrpCaptchaLib;

namespace MEFrpLauncherX.Views;

public partial class LoginPage : UserControl
{
    private const string nil = "nil";

    private readonly LoginViewModel _loginViewModel;

    public LoginPage()
    {
        InitializeComponent();
        _loginViewModel = new LoginViewModel();
        DataContext = _loginViewModel;
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
                PrimaryButtonText = "确定",
                DefaultButton = ContentDialogButton.Primary,
                IsSecondaryButtonEnabled = false,
                CloseButtonText = "取消"
            };
            var input = new TextBox();
            cd.Content = input;
            return await cd.ShowAsync() == ContentDialogResult.Primary
                ? MEpiConverter.GetCaptchaResult(input.Text).Split("||")[0]
                : nil;
        }

        MainWindowViewModel.Instance.AppMessage = "正在人机验证 步骤1/5";
        var c = CaptchaHelper.GetChallengeContent();
        MainWindowViewModel.Instance.Progress = 20.0;

        MainWindowViewModel.Instance.AppMessage = "正在人机验证 步骤2/5";
        var ci = await MEpiConverter.PostChallengeAsync(c);

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
        var (ri, _err) = await MEpiConverter.GetRedeemAsync(JsonSerializer.Serialize(rb));

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
            UsrNameBox.IsReadOnly = true;
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

            var (success, message) = MEpiConverter.SendLoginInfo(usr, pwd, captchaResult.Trim());

            Core.App.CurrentLogger.LogDebug($"API响应: {success}, {message}");

            if (success)
            {
                var userInfo =
                    JsonSerializer.Deserialize<InfoClasses.ApiInfo<InfoClasses.UserInfo>>(message,
                        AppJsonSerializerContext.Default.ApiInfoUserInfo);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    MEpiConverter.CurrentUserInfo = userInfo;
                    UserCache.CurrentUser = new InfoClasses.UserInfo
                    {
                        username = userInfo.data.username,
                        token = userInfo.data.token,
                        group = userInfo.data.group
                    };
                    MainWindow.Instance.LoginBackground.IsVisible = false;
                    MainWindowViewModel.Instance.IsLoggedIn = true;
                    MainWindow.Instance.MainContentControl.Content = null;
                    MainWindow.Instance.MainContentControl.Content = new MainPageFrame();
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
                .GetMessageBoxStandard("错误", $"验证失败: {ex.Message}", icon: Icon.Error)
                .ShowAsync();
        }
        finally
        {
            LoginBtn.IsEnabled = true;
        }
    }


    private void Control_OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (Design.IsDesignMode)
        {
            return;
        }

        if (!UserCache.IsLoggedIn())
        {
            return;
        }

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
}