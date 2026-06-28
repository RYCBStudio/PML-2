using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia.Collections;
using Avalonia.Data.Converters;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Storage;
using ReactiveUI;

namespace MEFrpLauncherX.ViewModels;

public class LoginViewModel : ViewModelBase
{
    public LoginViewModel()
    {
        if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
        {
            AuthModes = ["(推荐) 浏览器验证", "无感验证"];
        }
        else
        {
            AuthModes = ["浏览器验证", "(推荐) 无感验证"];
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
        var list = new List<string> { "<使用新账号>" };
        list.AddRange(filteredUsernames);
        StoredUsernames = new AvaloniaList<string>(list);
        this.RaisePropertyChanged(nameof(HasStoredUsernames));
        // Ensure default selection is the "new account" placeholder
        SelectedStoredIndex = 0;
    }

    /// <summary>
    /// 是否有已存储的账号
    /// </summary>
    public bool HasStoredUsernames => StoredUsernames.Count > 0;
}

public class ProgressToTextConverter : IValueConverter
{
    public static ProgressToTextConverter Instance
    {
        get;
    } = new();

    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        var progress = value as double?;
        return progress.HasValue ? progress > 0 ? $"登录中... {progress:F2}%" : "登录" : "登录";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}