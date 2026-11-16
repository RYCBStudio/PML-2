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
using FluentAvalonia.MarkdownRender.Helper;
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
using RYCB.PML2.MEFrpCaptchaLib;

namespace MEFrpLauncherX.Views;

public partial class LoginPage : UserControl
{
    private const string nil = "nil";

    private readonly LoginViewModel _loginViewModel;
    private bool _autoLogin;

    /// <summary>
    ///     当前登录页实例（26.5.1 C2）。
    ///     <para>
    ///         <see cref="GetCaptchaResultAsync" /> 是静态方法（被用户中心、主页、About 页共用），
    ///         需要它把「网络不可用」状态回写到登录页的 ViewModel，只能通过静态实例引用。
    ///     </para>
    /// </summary>
    public static LoginPage? Instance
    {
        get;
        private set;
    }

    public LoginPage()
    {
        InitializeComponent();
        _autoLogin = ConfigManager.CurrentConfig.AutoLogin;
        AutoLoginSwitch.IsChecked = _autoLogin;
        _loginViewModel = new LoginViewModel();

        // 26.5.1 C2：把「重试」的实际动作注入 ViewModel —— NoConnection 只负责触发命令
        _loginViewModel.RetryRequested = RetryLoginAsync;

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
        Instance = this;
    }

    /// <summary>
    ///     网络不可用时的「重试」动作（26.5.1 C2）：重新发起一次登录流程。
    /// </summary>
    private Task RetryLoginAsync()
    {
        _loginViewModel.IsNetworkUnavailable = false;

        if (string.IsNullOrEmpty(UsrNameBox.Text) || string.IsNullOrEmpty(PwdBox.Text))
        {
            // 账号密码没填就没法重试，交给用户自己补全
            return Task.CompletedTask;
        }

        // LoginBtn_Click 是 async void（事件处理器签名），内部已捕获所有异常；
        // 重试过程由 IsBusy 呈现进度。这里只负责触发。
        LoginBtn_Click(LoginBtn, new RoutedEventArgs());
        return Task.CompletedTask;
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
                    message = Languages.Text_Login_LocalStoredLogin
                };

                UserCache.CurrentUser = stored;

                // 26.4：本地自动登录同样清空缓存，保证账号数据隔离
                Core.Services.ApiCacheService.InvalidateAll();
                MEFrpApiConverter.ResetInMemoryCaches();

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

    /// <summary>
    ///     获取人机验证码结果。
    ///     <para>
    ///         26.5.1 C2：原实现有三条「卡死/崩溃」路径：
    ///         <list type="number">
    ///             <item>
    ///                 challenge / redeem 请求没有超时（RestSharp 默认 100 秒），断网时这里会一直挂着，
    ///                 <c>vm.IsBusy</c> 永远为 true —— 表现为登录界面卡死无响应。
    ///             </item>
    ///             <item>
    ///                 <c>ri?.success == false</c> 在 <c>ri == null</c>（请求失败）时求值为 false，
    ///                 于是失败会被当成成功放行，继续走到 <c>CaptchaHelper.GetCaptchaCode(null)</c> ——
    ///                 实测该方法对 null 参数直接抛 <see cref="NullReferenceException" />。
    ///             </item>
    ///             <item>
    ///                 整个方法没有 try/finally，任何未捕获异常都会让 <c>IsBusy</c> / <c>IsFakeProgress</c>
    ///                 永久停在 true，进度条一直转。
    ///             </item>
    ///         </list>
    ///         现在改为：整体 try/finally 保证状态复位；失败一律返回空串（调用方据此放弃本次登录）。
    ///     </para>
    /// </summary>
    public static async Task<string> GetCaptchaResultAsync()
    {
        Core.App.CurrentLogger.Log("开始获取验证码...");
        if (ConfigManager.CurrentConfig.CaptchaMode == "explicit" ||
            ConfigManager.CurrentConfig.CaptchaMode.Equals("browser", StringComparison.CurrentCultureIgnoreCase))
        {
            await Core.App.MainWindow.Launcher.LaunchUriAsync(
                new Uri("https://www.mefrp.com/3rdparty/captcha?client=PML%202"));
            var cd = new FAContentDialog
            {
                Title = Languages.Text_Login_EnterCaptcha,
                PrimaryButtonText = Languages.Text_Global_Confirm,
                DefaultButton = FAContentDialogButton.Primary,
                IsSecondaryButtonEnabled = false,
                CloseButtonText = Languages.Text_Global_Cancel
            };
            var input = new TextBox();
            cd.Content = input;
            
            Core.App.CurrentLogger.Log("获取验证码成功");
            return await cd.ShowAsync() == FAContentDialogResult.Primary
                ? MEFrpApiConverter.GetCaptchaResult(input.Text).Split("||")[0]
                : null;
        }

        var vm = MainWindowViewModel.Instance;

        // 开始：开启假进度
        vm.IsFakeProgress = true;
        vm.IsBusy = true;
        vm.Progress = 0;
        vm.AppMessage = string.Format(Languages.Text_Login_CaptchaStep, 1);

        try
        {
            var c = CaptchaHelper.GetChallengeContent();

            vm.AppMessage = string.Format(Languages.Text_Login_CaptchaStep, 2);
            var ci = await MEFrpApiConverter.PostChallengeAsync(
                JsonSerializer.Serialize(c, App.AppJsonSerializerContext.ChallengeInfo));

            // 26.5.1 C2：请求失败时 PostChallengeAsync 返回 null，必须显式判空，
            // 否则会把 null 传给 CaptchaHelper.GetRedeemBody（实测抛 NRE）。
            if (ci is null)
            {
                // challenge 请求都发不出去 → 判定为网络不可用，登录页切到「网络不可用」占位并提供重试
                MarkNetworkUnavailable();
                await ShowCaptchaFailureAsync(Languages.Text_Login_CaptchaNetworkFailed);
                return string.Empty;
            }

            vm.AppMessage = string.Format(Languages.Text_Login_CaptchaStep, 3);
            var (rb, err) = await CaptchaHelper.GetRedeemBody(ci);

            if (rb is null)
            {
                await ShowCaptchaFailureAsync(err.IsNullOrEmpty()
                    ? Languages.Text_Login_CaptchaValidationFailed
                    : err);
                return string.Empty;
            }

            vm.AppMessage = string.Format(Languages.Text_Login_CaptchaStep, 4);
            var (ri, _err) = await MEFrpApiConverter.GetRedeemAsync(
                JsonSerializer.Serialize(rb, App.AppJsonSerializerContext.RedeemInfo));

            // 26.5.1 C2：ri 为 null 表示 redeem 请求失败/响应无法解析。
            // 原判定 `ri?.success == false` 在 ri == null 时为 false，会把失败当成功放行，
            // 随后 CaptchaHelper.GetCaptchaCode(null) 抛 NRE。
            if (ri is null)
            {
                // redeem 请求失败 → 同样是网络层问题
                MarkNetworkUnavailable();
                await ShowCaptchaFailureAsync(_err.IsNullOrEmpty()
                    ? Languages.Text_Login_CaptchaNetworkFailed
                    : _err);
                return string.Empty;
            }

            if (!ri.success)
            {
                // 服务端明确返回「验证未通过」—— 网络是通的，只是答案错了。
                // 此时不应把登录表单换成「网络不可用」占位，保持表单让用户重试。
                await ShowCaptchaFailureAsync(_err.IsNullOrEmpty()
                    ? Languages.Text_Login_CaptchaValidationFailed
                    : _err);
                return string.Empty;
            }

            vm.AppMessage = string.Format(Languages.Text_Login_CaptchaStep, 5);

            var captchaResult = CaptchaHelper.GetCaptchaCode(ri);
            if (captchaResult.IsNullOrEmpty())
            {
                await ShowCaptchaFailureAsync(Languages.Text_Login_CaptchaValidationFailed);
                return string.Empty;
            }

            // 结束：关掉假进度，Behavior 会自动平滑冲到 100%
            Core.App.CurrentLogger.Log("获取验证码成功");
            await Task.Delay(1000);
            vm.IsFakeProgress = false;
            vm.Progress = 100;
            return captchaResult;
        }
        catch (Exception ex)
        {
            // 26.5.1 C2：兜底捕获 —— 验证码链路上的任何异常都不该让登录界面卡在加载态
            Core.App.CurrentLogger.Error(ex, "获取人机验证码失败");
            await ShowCaptchaFailureAsync(string.Format(Languages.Text_Login_ValidationFailedWithMsg, ex.Message));
            return string.Empty;
        }
        finally
        {
            // 26.5.1 C2：无论成功、失败还是异常，都必须复位加载态，
            // 否则主窗口底部的进度条会一直转（IsBusy 永久为 true）。
            vm.IsBusy = false;
            vm.IsFakeProgress = false;
        }
    }

    /// <summary>
    ///     展示人机验证码失败提示（统一走 UI 线程）。
    /// </summary>
    private static async Task ShowCaptchaFailureAsync(string message)
    {
        if (message.IsNullOrEmpty())
        {
            message = Languages.Text_Login_CaptchaValidationFailed;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
            MessageBoxManager.GetMessageBoxStandard(Languages.Text_Login_ValidationFailed, message, icon: Icon.Error)
                .ShowAsync());
    }

    /// <summary>
    ///     标记「网络不可用」（26.5.1 C2）。
    ///     <para>
    ///         只在登录页存在时生效 —— <see cref="GetCaptchaResultAsync" /> 同时被用户中心签到、
    ///         主页签到、About 页调试按钮调用，那些场景下把「整个登录表单换成断网占位」是错的。
    ///     </para>
    /// </summary>
    private static void MarkNetworkUnavailable()
    {
        if (Instance is { } page)
        {
            page._loginViewModel.IsNetworkUnavailable = true;
        }
    }

    /// <summary>
    /// 把进度强制抬高到指定值，然后立刻切回 Fake 模式继续渐近
    /// （利用 Behavior 的 _fakeBaseProgress 机制实现无缝衔接）
    /// </summary>
    private static async Task SnapProgressAsync(MainWindowViewModel vm, double target)
    {
        // 切到 Real 模式瞬间写入目标值
        vm.IsFakeProgress = false;
        vm.Progress = target;

        // 给 UI 一帧时间更新
        await Task.Delay(1);

        // 立刻切回 Fake，Behavior 会以当前 Progress 作为新的基线继续往 Target 渐近
        vm.IsFakeProgress = true;
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
            LoginBtn.Content = new LoadingTip(Languages.Text_Login_LoggingIn);
            (sender as Control)?.IsEnabled = false;
            CaptchaHelper.Init((progress, current, completed, nonce) =>
            {
                _loginViewModel.Progress = progress;
            });

            var captchaResult = await GetCaptchaResultAsync();

            // 26.5.1 C2：验证码获取失败（断网/超时/验证未通过）时直接放弃本次登录，
            // 否则会用空验证码去请求登录接口，只会得到一个含义不明的失败。
            if (captchaResult.IsNullOrEmpty())
            {
                LoginBtn.Content = Languages.Text_Login_Login;
                return;
            }

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
                    // 26.4：登录成功后清空统一缓存，确保看到的是本账号的最新数据
                    Core.Services.ApiCacheService.InvalidateAll();
                    MEFrpApiConverter.ResetInMemoryCaches();
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
                        .GetMessageBoxStandard(Languages.Text_Login_LoginFailed, message, icon: Icon.Error)
                        .ShowAsync());
                LoginBtn.Content = Languages.Text_Login_Login;
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
                .GetMessageBoxStandard(Languages.Caption_Error,
                    string.Format(Languages.Text_Login_ValidationFailedWithMsg, ex.Message), icon: Icon.Error)
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
        UrlHelper.OpenUrl("https://www.mefrp.com/auth/register");

    private void AutoLogin(object? sender, RoutedEventArgs e)
    {
        ConfigManager.UpdateConfig(cfg =>
            cfg.AutoLogin = AutoLoginSwitch.IsChecked ?? false);
    }
}