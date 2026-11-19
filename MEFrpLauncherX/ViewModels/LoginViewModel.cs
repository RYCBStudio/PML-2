using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Data.Converters;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Core.Storage;
using ReactiveUI;
using RxVoid = ReactiveUI.Primitives.RxVoid;

namespace MEFrpLauncherX.ViewModels;

public class LoginViewModel : ViewModelBase
{
    public LoginViewModel()
    {
        // ReactiveUI 25：无参 ReactiveCommand 的载体类型 RxVoid（替代 System.Reactive.Unit）位于此命名空间。
        RetryCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            IsNetworkUnavailable = false;
            if (RetryRequested is { } retry)
            {
                await retry();
            }
        });

        if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
        {
            AuthModes =
            [
                Languages.Text_Settings_Captcha_ExplicitRecommended, Languages.Text_Settings_Captcha_Implicit
            ];
        }
        else
        {
            AuthModes =
            [
                Languages.Text_Settings_Captcha_Explicit, Languages.Text_Settings_Captcha_ImplicitRecommended
            ];
        }

        AuthMode = ConfigManager.CurrentConfig.CaptchaMode.ToLower() switch
        {
            "implicit" => 1,
            "explicit" => 0
        };

        RefreshStoredUsernames();
        if (StoredUsernames.Count >= 2)
        {
            SelectedStoredIndex = 1;
            SelectedStoredUsername = StoredUsernames[1];
        }
    }

    public double Progress
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public AvaloniaList<string> AuthModes
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public int AuthMode
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            ConfigManager.UpdateConfig(x => x.CaptchaMode = AuthMode switch
            {
                0 => "explicit",
                1 => "implicit",
                _ => "explicit"
            });
        }
    }

    /// <summary>
    /// 已存储的登录过的用户名列表
    /// </summary>
    public AvaloniaList<string> StoredUsernames
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    // guard to avoid recursive updates between index and text
    private bool _suppressSelectionPropagation = false;

    /// <summary>
    /// 当前选中的已存储账号索引，-1 表示未选中
    /// </summary>
    public int SelectedStoredIndex
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            if (_suppressSelectionPropagation)
            {
                return;
            }

            if (value >= 0 && value < StoredUsernames.Count)
            {
                try
                {
                    _suppressSelectionPropagation = true;
                    // index 0 reserved for "新账号" placeholder -> clear text for typing
                    SelectedStoredUsername = value == 0 ? string.Empty : StoredUsernames[value];
                }
                finally
                {
                    _suppressSelectionPropagation = false;
                }
            }
            else
            {
                try
                {
                    _suppressSelectionPropagation = true;
                    SelectedStoredUsername = null;
                }
                finally
                {
                    _suppressSelectionPropagation = false;
                }
            }
        }
    } = 0;

    /// <summary>
    /// 当前选中的已存储账号用户名
    /// </summary>
    public string? SelectedStoredUsername
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);

            if (_suppressSelectionPropagation)
            {
                return;
            }

            // If user typed a username (or cleared), update SelectedStoredIndex accordingly
            if (string.IsNullOrEmpty(value))
            {
                // Treat empty as "新账号" placeholder
                SelectedStoredIndex = 0;
                return;
            }

            var idx = StoredUsernames.IndexOf(value);
            if (idx >= 0)
            {
                SelectedStoredIndex = idx;
            }
            else
            {
                // User typed a username not in the stored list; reset index to 0 (new account)
                // but preserve the text by suppressing the propagation back to SelectedStoredUsername
                try
                {
                    _suppressSelectionPropagation = true;
                    SelectedStoredIndex = 0;
                }
                finally
                {
                    _suppressSelectionPropagation = false;
                }
            }
        }
    }

    /// <summary>
    /// 刷新已存储账号列表
    /// </summary>
    public void RefreshStoredUsernames()
    {
        var usernames = UserCache.GetStoredUsernames();
        // 过滤掉 "default" 键
        var filteredUsernames = usernames
            .Where(u => !string.Equals(u, "default", StringComparison.OrdinalIgnoreCase))
            .ToList();
        // Insert a placeholder at index 0 for "use new account"
        var list = new List<string> { Languages.Text_Login_UseNewAccount };
        list.AddRange(filteredUsernames);
        StoredUsernames = [.. list];
        this.RaisePropertyChanged(nameof(HasStoredUsernames));
        // Ensure default selection is the "new account" placeholder
        SelectedStoredIndex = 0;
    }

    /// <summary>
    /// 是否有已存储的账号
    /// </summary>
    public bool HasStoredUsernames => StoredUsernames.Count > 0;

    public bool IsDark => ConfigManager.CurrentConfig.Theme.Equals("dark", StringComparison.CurrentCultureIgnoreCase);
    public bool IsLight => !IsDark;

    public int SelectedLoginMode
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsMEFrp));
            this.RaisePropertyChanged(nameof(IsLoginFormVisible));
            this.RaisePropertyChanged(nameof(IsAcmeFormVisible));
        }
    }
    
    public bool IsMEFrp => SelectedLoginMode == 0;

    /// <summary>
    ///     MEFrp 登录表单是否可见（网络不可用时让位给 <see cref="Controls.NoConnection" />）。
    /// </summary>
    public bool IsLoginFormVisible => IsMEFrp && !IsNetworkUnavailable;

    /// <summary>
    ///     云上极致登录表单是否可见（网络不可用时让位给 <see cref="Controls.NoConnection" />）。
    /// </summary>
    public bool IsAcmeFormVisible => !IsMEFrp && !IsNetworkUnavailable;

    /// <summary>
    ///     网络不可用（26.5.1 C2）。
    ///     <para>
    ///         人机验证请求失败（断网 / 超时）时置为 true，登录页会显示
    ///         <see cref="Controls.NoConnection" /> 占位并提供「重试」，
    ///         而不是让用户面对一个卡在加载态的界面。
    ///     </para>
    /// </summary>
    public bool IsNetworkUnavailable
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsLoginFormVisible));
            this.RaisePropertyChanged(nameof(IsAcmeFormVisible));
        }
    }

    /// <summary>
    ///     「重试」动作（26.5.1 C2）。由 <see cref="Controls.NoConnection" /> 的重试按钮触发。
    /// </summary>
    public ReactiveCommand<RxVoid, RxVoid> RetryCommand { get; }

    /// <summary>
    ///     由视图注册的「重试」回调（26.5.1 C2）。
    ///     <para>
    ///         <see cref="Controls.NoConnection" /> 只负责展示与触发命令，
    ///         真正的重试动作（重新发起人机验证）由 <c>LoginPage</c> 注入。
    ///     </para>
    /// </summary>
    public Func<Task>? RetryRequested { get; set; }
}

public class ProgressToTextConverter : IValueConverter
{
    public static ProgressToTextConverter Instance
    {
        get;
    } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var progress = value as double?;
        return progress.HasValue
            ? progress > 0 ? string.Format(Languages.Text_Login_LoggingInProgress, progress) : Languages.Text_Login_Login
            : Languages.Text_Login_Login;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}