using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Windowing;
using MarkdownAIRender.Controls.MarkdownRender;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Analysis;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Core.MEFIntegrated;
using MEFrpLauncherX.Core.Storage;
using MEFrpLauncherX.Views;
using MsBox.Avalonia;
using MsBox.Avalonia.ViewModels.Commands;
using ReactiveUI;
using SecretLib;

namespace MEFrpLauncherX.ViewModels;

public class HomePageViewModel : ViewModelBase, IDisposable
{
    public HomePageViewModel()
    {
        // 初始化命令
        SignCommand = ReactiveCommand.CreateFromTask(SignAsync);
        LoadDataCommand = ReactiveCommand.CreateFromTask(LoadUserDataAsync);
        CopyUserIdCommand = ReactiveCommand.Create(() =>
        {
            if (UserId != null)
                TopLevel.GetTopLevel(Core.App.MainWindow)?.Clipboard?.SetTextAsync(UserId);
        });

        CopyEmailCommand = ReactiveCommand.Create(() =>
        {
            if (Email != null)
                TopLevel.GetTopLevel(Core.App.MainWindow)?.Clipboard?.SetTextAsync(Email);
        });

        IsLoading = LoadDataCommand.IsExecuting
            .ToProperty(this, x => x.IsLoading).Value;
        LoadDataCommand.ThrownExceptions.Subscribe(ex =>
        {
            Core.App.CurrentLogger?.Error(ex);
        });
        SignCommand.ThrownExceptions.Subscribe(ex =>
        {
            Core.App.CurrentLogger?.Error(ex);
        });
        // 初始加载数据
        MainPageFrameViewModel.Instance?.IsLoading = false;
        _ = LoadUserDataAsync();
    }

    public bool IsLoading
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    // 用户信息属性
    public string? UserName
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string? UserId
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string? Email
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string? RegisterTime
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string? Traffic
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string? LongTraffic
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string? InBound
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string? OutBound
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string? Group
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool? IsAdmin
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string? RealNameStatus
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool? IsRealNamed
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string? AccountStatus
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool? IsBanned
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string? ProxiesCount
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool CanSign
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string? SignButtonText
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string? NoticeContent
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    // 命令
    public ReactiveCommand<Unit, Unit> SignCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> LoadDataCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> CopyUserIdCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> CopyEmailCommand
    {
        get;
    }

    public bool IsDark => ConfigManager.CurrentConfig.Theme.Equals("Dark", StringComparison.OrdinalIgnoreCase);

    public int SystemStatus
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = -2;

    public string SystemStatusRemark
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public int PlatformNodes
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public int PlatformUsers
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public int PlatformProxies
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public long PlatformTraffic
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public AvaloniaList<NoticeContent> SoftwareNotice
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    public bool IsLoadingNotice
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsNoData
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool ShowSoftwareNotice => ConfigManager.CurrentConfig.HomeSettings.ShowSoftwareNotice;
    public bool ShowStatistics => ConfigManager.CurrentConfig.HomeSettings.ShowStatistics;
    public bool StatisticsSpan2 => ShowStatistics && !ShowUserInfo;
    public bool ShowUserInfo => ConfigManager.CurrentConfig.HomeSettings.ShowUserInfo;
    public bool UserInfoRow1 => ShowStatistics && ShowUserInfo;
    public bool ShowSystemStatus => ConfigManager.CurrentConfig.HomeSettings.ShowSystemInfo;
    public bool ShowSystemNotice => ConfigManager.CurrentConfig.HomeSettings.ShowSystemNotice;
    public bool SystemNoticeSpan2 => ShowSystemNotice && !ShowSoftwareNotice;
    public bool SoftwareNoticeSpan2 => ShowSoftwareNotice && !ShowSystemNotice;

    public void Dispose() => GC.RemoveMemoryPressure(100 * 1024 * 1024);

    private async Task LoadUserDataAsync()
    {
        if (Design.IsDesignMode)
        {
            return;
        }

        Core.App.CurrentLogger.LogDebug("开始加载用户数据");

        IsLoading = true;
        var ss = await MEFrpApiConverter.GetSystemStatusAsync();
        SystemStatus = ss.data?.status ?? -1;
        SystemStatusRemark = ss.data?.remark ?? $"网络服务不可用，返回代码: {ss.code}";
        var networkOk = ss.code == 200;
        if (!networkOk)
        {
            IsLoading = false;
            IsLoadingNotice = false;
        }

        var platform = await MEFrpApiConverter.GetPublicInfoAsync();
        if (platform.code == 200)
        {
            PlatformNodes = platform.data.nodes;
            PlatformUsers = platform.data.users;
            PlatformProxies = platform.data.proxies;
            PlatformTraffic = platform.data.traffic;
        }


        try
        {
            if (networkOk)
            {
                var res = await MEFrpApiConverter.GetExtraUserInfoAsync();
                var data = res.data;
                Core.App.CurrentLogger.LogDebug("结束加载用户数据, 状态码：" + res.code);

                UserName = data.username;
                Email = data.email;
                RegisterTime = DateTimeOffset.FromUnixTimeSeconds(data.regTime).LocalDateTime
                    .ToString("yyyy-MM-dd HH:mm:ss");
                Traffic = ProcessFileSize(data.traffic);
                LongTraffic = ProcessFileSize(data.traffic, 1);
                InBound = ProcessBoundSize(data.inBound);
                OutBound = ProcessBoundSize(data.outBound);
                UserId = $"# {data.userId}";
                Group = data.friendlyGroup;

                if (UserCache.CurrentUser?.Email.IsNullOrEmpty() == true)
                {
                    UserCache.CurrentUser = new InfoClasses.UserInfo
                    {
                        group = UserCache.CurrentUser.group,
                        username = UserCache.CurrentUser.username,
                        token = UserCache.CurrentUser.token,
                        Email = Email
                    };
                    AppAnalytics.SetUserId(DeviceIdHelper.GetDeviceUniqueId(), UserCache.CurrentUser.username,
                        UserCache.CurrentUser.Email);
                }

                // 用户组样式
                IsAdmin = data.group == "admin";

                // 实名状态
                if (data.isRealname)
                {
                    RealNameStatus = Languages.Text_Main_UserInfo_RealNameAuthenticationStatus_Authenticated;
                    IsRealNamed = true;
                }
                else
                {
                    RealNameStatus = Languages.Text_Main_UserInfo_RealNameAuthenticationStatus_UnAuthenticated;
                    IsRealNamed = false;
                }

                // 账户状态
                AccountStatus = data.status switch
                {
                    0 => Languages.Text_Main_UserInfo_AccountStatus_Normal,
                    1 => Languages.Text_Main_UserInfo_AccountStatus_Banned,
                    2 => Languages.Text_Main_UserInfo_AccountStatus_OverTraffic,
                    _ => Languages.Text_Main_UserInfo_AccountStatus_Unknown
                };

                IsBanned = data.status == 1;

                // 签到按钮状态
                CanSign = !data.todaySigned;
                SignButtonText = !data.todaySigned
                    ? Languages.Text_Main_UserInfo_SignIn
                    : Languages.Text_Main_UserInfo_SignedIn;

                ProxiesCount = $"{data.usedProxies}/{data.maxProxies}";
                // 加载公告
                NoticeContent = HtmlToMarkdownConverter.ConvertRawLinkToMarkdown(
                    HtmlToMarkdownConverter.ConvertHtmlImagesToMarkdown((await MEFrpApiConverter.GetNoticeAsync())
                        .data));

                if (NoticeContent.IsNullOrEmpty())
                {
                    IsNoData = true;
                }

                IsLoading = false;

                var popUp = await MEFrpApiConverter.GetPopupNoticeAsync();

                Core.App.CurrentLogger.Log($"数据已加载，用户名: {data.username}");
                MainPageFrameViewModel.Instance?.IsLoading = false;
                if (popUp?.data.IsNullOrEmpty() == false)
                {
                    var markdownRender = new MarkdownRender
                    {
                        Value = popUp?.data
                    };
                    await NoticeManager.CheckAndShowNotice(popUp?.data, markdownRender);
                }
            }

            if (Path.Exists(Path.Combine(Core.App.StartupPath, "RYCB.MEFrpLauncherX.CrashDisplayer.pmla")))
            {
                var btn = new TaskDialogButton
                {
                    DialogResult = TaskDialogStandardResult.Cancel,
                    Text = Languages.Text_Global_Cancel,
                    Command = new RelayCommand(async _ =>
                    {
                    })
                };
                var cnt = "";
                var td = new TaskDialog
                {
                    Title = Languages.Text_Main_Initialize_Title,
                    ShowProgressBar = true,
                    IconSource = new SymbolIconSource { Symbol = Symbol.Download },
                    SubHeader = Languages.Text_Main_Initialize_Resource,
                    Content = cnt,
                    Buttons =
                    {
                        btn
                    }
                };
                td.SetProgressBarState(0, TaskDialogProgressState.Indeterminate);
                td.XamlRoot = TopLevel.GetTopLevel(Core.App.MainWindow);
                td.ShowAsync();
                if (!Path.Exists(Path.Combine(Core.App.StartupPath, "RYCB.MEFrpLauncherX.CrashDisplayer.pmla")))
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        td.Hide(TaskDialogStandardResult.Cancel);
                    });
                    return;
                }

                Directory.CreateDirectory(Path.Combine(Core.App.StartupPath, "Tools"));
                await Task.Run(() => PMLAHelper.UnpackPmla(
                    Path.Combine(Core.App.StartupPath, "RYCB.MEFrpLauncherX.CrashDisplayer.pmla"),
                    Path.Combine(Core.App.StartupPath, "Tools"),
                    (progress, status) =>
                    {
                        td.SetProgressBarState(progress, TaskDialogProgressState.Normal);
                        cnt = status;
                    }));
                var cdFile = Path.Combine(Core.App.StartupPath, "Tools", "RYCB.MEFrpLauncherX.CrashDisplayer");
                if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                {
                    if (File.Exists(cdFile))
                    {
                        var psi = new ProcessStartInfo("/bin/chmod", $"+x \"{cdFile}\"")
                        {
                            UseShellExecute = false
                        };
                        await Process.Start(psi)?.WaitForExitAsync();
                    }
                }
                Dispatcher.UIThread.Post(() =>
                {
                    td.Hide(TaskDialogStandardResult.OK);
                });
                File.Delete(Path.Combine(Core.App.StartupPath, "RYCB.MEFrpLauncherX.CrashDisplayer.pmla"));
            }

            if (Directory.GetFiles(Path.Combine(Core.App.StartupPath, "Cache")).Select(x => x.StartsWith("update_tmp"))
                .Any())
            {
                var btn = new TaskDialogButton
                {
                    DialogResult = TaskDialogStandardResult.Cancel,
                    Text = Languages.Text_Global_Cancel,
                    Command = new RelayCommand(async _ =>
                    {
                    })
                };
                var cnt = "";
                var td = new TaskDialog
                {
                    Title = Languages.Text_Main_PostUpdateProcess_Title,
                    ShowProgressBar = true,
                    IconSource = new SymbolIconSource { Symbol = Symbol.Download },
                    SubHeader = Languages.Text_Main_PostUpdateProcess_Cleaning,
                    Content = cnt,
                    Buttons =
                    {
                        btn
                    }
                };
                td.SetProgressBarState(0, TaskDialogProgressState.Indeterminate);
                td.XamlRoot = TopLevel.GetTopLevel(Core.App.MainWindow);
                td.ShowAsync();

                await Task.Run(() => Directory.Delete(Path.Combine(Core.App.StartupPath, "Cache"), true));
                Dispatcher.UIThread.Post(() =>
                {
                    Directory.CreateDirectory(Path.Combine(Core.App.StartupPath, "Cache"));
                    try
                    {
                        Core.App.MainWindow?.PlatformFeatures.SetTaskBarProgressBarState(TaskBarProgressBarState
                            .Normal);
                        Core.App.MainWindow?.PlatformFeatures.SetTaskBarProgressBarValue(100, 100);
                    }
                    catch
                    {
                        /*Ignore*/
                    }

                    td.Hide(TaskDialogStandardResult.OK);
                    try
                    {
                        Core.App.MainWindow?.PlatformFeatures.SetTaskBarProgressBarState(TaskBarProgressBarState.None);
                    }
                    catch
                    {
                        /*Ignore*/
                    }
                });
            }

            IsLoadingNotice = true;
            var notice = await RYCBApiConverter.GetAllNoticeAsync();
            SoftwareNotice.Clear();
            if (notice.success)
            {
                SoftwareNotice.AddRange(notice.data);
            }
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger.Error(ex);
            await MessageBoxManager
                .GetMessageBoxStandard(Languages.Caption_Error, $"加载用户数据失败: {ex.Message}")
                .ShowAsync();
        }
        finally
        {
            MainPageFrameViewModel.Instance?.IsLoading = false;
            IsLoading = false;
            IsLoadingNotice = false;
        }
    }

    private async Task SignAsync()
    {
        var captchaResult = await LoginPage.GetCaptchaResultAsync();
        if (captchaResult.IsNullOrEmpty())
        {
            return;
        }

        // 执行签到
        try
        {
            var (success, message) = await MEFrpApiConverter.SendSignRequestAsync(captchaResult.Trim());
            var signInfo =
                JsonSerializer.Deserialize<InfoClasses.ApiInfo<object>>(message ??
                                                                        """
                                                                        {
                                                                            "code": -1,
                                                                            "data": null,
                                                                            "message": "未知错误"
                                                                        }
                                                                        """,
                    App.AppJsonSerializerContext.ApiInfoObject);

            Core.App.CurrentLogger.Log($"API返回结果: {success}, {message}");
            if (success)
            {
                Growl.Success(signInfo?.message ?? Languages.Text_Main_UserInfo_SignIn + Languages.Text_Global_Success,
                    Languages.Text_Main_UserInfo_SignIn + Languages.Text_Global_Success);
            }
            else
            {
                Growl.Error(signInfo?.message ?? Languages.Text_Main_UserInfo_SignIn + Languages.Text_Global_Failed,
                    Languages.Text_Main_UserInfo_SignIn + Languages.Text_Global_Failed);
            }
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger.Error(ex);
            Growl.Error(ex.Message, "签到失败");
        }
        finally
        {
            await LoadUserDataAsync();
        }
    }

    private static string ProcessFileSize(ulong size, int maxUnitIndex = -1)
    {
        string[] units = ["MB", "GB", "TB", "PB", "EB", "ZB", "YB"];
        var unitIndex = 0;
        double adjustedSize = size;
        while (adjustedSize >= 1024 && unitIndex < (maxUnitIndex == -1 ? units.Length - 1 : maxUnitIndex))
        {
            adjustedSize /= 1024;
            unitIndex++;
        }

        return $"{adjustedSize:F2} {units[unitIndex]}";
    }

    private static string ProcessBoundSize(long size)
    {
        string[] units = ["Kbps", "Mbps", "Gbps", "Tbps"];
        var unitIndex = 0;
        double adjustedSize = size;

        // 自定义换算：1 Mbps = 128 Kbps
        while (adjustedSize >= 128 && unitIndex < units.Length - 2)
        {
            adjustedSize /= 128;
            unitIndex++;
        }

        return $"{adjustedSize:F2} {units[unitIndex]}";
    }

    ~HomePageViewModel()
    {
        GC.SuppressFinalize(this);
    }
}

public class NoticeData
{
    public string Notice
    {
        get;
        set;
    } = string.Empty;

    public bool Read
    {
        get;
        set;
    }
}

public class NoticeManager
{
    private static readonly string NoticeFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PML2", "popup_notice.dat");

    // 读取通知数据
    public static NoticeData ReadNoticeData()
    {
        try
        {
            if (!File.Exists(NoticeFilePath))
            {
                // 文件不存在，创建默认数据
                var defaultData = new NoticeData();
                SaveNoticeData(defaultData);
                return defaultData;
            }

            // 读取二进制文件并反序列化
            var fileBytes = File.ReadAllBytes(NoticeFilePath);
            var jsonString = Encoding.UTF8.GetString(fileBytes);
            return JsonSerializer.Deserialize<NoticeData>(jsonString, App.AppJsonSerializerContext.NoticeData) ??
                   new NoticeData();
        }
        catch (Exception ex)
        {
            // 如果读取失败，返回默认数据
            Core.App.CurrentLogger?.Error(ex);
            return new NoticeData();
        }
    }

    // 保存通知数据
    public static void SaveNoticeData(NoticeData data)
    {
        try
        {
            // 确保目录存在
            var directory = Path.GetDirectoryName(NoticeFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 序列化为 JSON 并转换为二进制
            var jsonString = JsonSerializer.Serialize(data, App.AppJsonSerializerContext.NoticeData);
            var binaryData = Encoding.UTF8.GetBytes(jsonString);

            File.WriteAllBytes(NoticeFilePath, binaryData);
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger?.Error(ex);
        }
    }

    // 检查并显示通知
    public static async Task CheckAndShowNotice(string currentNotice,
        MarkdownRender markdownRender)
    {
        var noticeData = ReadNoticeData();

        // 如果通知内容不同或未读，则显示通知
        if (noticeData.Notice != currentNotice || !noticeData.Read)
        {
            // 更新通知数据
            noticeData.Notice = currentNotice;
            noticeData.Read = true;
            SaveNoticeData(noticeData);

            // 显示消息框
            await MessageBox.ShowAsync(
                markdownRender,
                "重要通知");
        }
    }
}

public static class HtmlToMarkdownConverter
{
    public static string ConvertHtmlImagesToMarkdown(string html)
    {
        // 匹配 <img> 标签的正则表达式
        var imgTagPattern = @"<img\s+[^>]*src\s*=\s*[""']([^""']+)[""'][^>]*>";
        var regex = new Regex(imgTagPattern, RegexOptions.IgnoreCase);

        // 替换所有匹配的 <img> 标签为 Markdown 格式
        var markdown = regex.Replace(html, match =>
        {
            var src = match.Groups[1].Value;
            var alt = Regex.Match(match.Value, @"alt\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase) is
                { Success: true } altMatch
                ? altMatch.Groups[1].Value
                : "";
            return $"![{alt}]({src})";
        });

        return markdown;
    }

    public static string ConvertRawLinkToMarkdown(string html)
    {
        try
        {
            // 设置正则超时时间
            var regex = new Regex(@"<((?:https?://)[^>]+)>",
                RegexOptions.IgnoreCase,
                TimeSpan.FromSeconds(5)); // 5秒超时

            return regex.Replace(html, match =>
            {
                var src = match.Groups[1].Value;
                return $"[{src}]({src})";
            });
        }
        catch (RegexMatchTimeoutException)
        {
            // 超时处理：返回原文本或简单处理
            return html;
        }
    }
}