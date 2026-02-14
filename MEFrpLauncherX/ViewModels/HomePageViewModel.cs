using System;
using System.IO;
using System.Reactive;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using MarkdownAIRender.Controls.MarkdownRender;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.MEFIntergrated;
using MEFrpLauncherX.Views;
using MsBox.Avalonia;
using MsBox.Avalonia.ViewModels.Commands;
using Newtonsoft.Json;
using ReactiveUI;
using SecretLib;

namespace MEFrpLauncherX.ViewModels;

public class HomePageViewModel : ViewModelBase, IDisposable
{
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

    public bool IsDark
    {
        get => ConfigManager.CurrentConfig.Theme.Equals("Dark", StringComparison.OrdinalIgnoreCase);
    }

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

    public HomePageViewModel()
    {
        // 初始化命令
        SignCommand = ReactiveCommand.CreateFromTask(SignAsync);
        LoadDataCommand = ReactiveCommand.CreateFromTask(LoadUserDataAsync);

        IsLoading = LoadDataCommand.IsExecuting
            .ToProperty(this, x => x.IsLoading).Value;
        // 初始加载数据
        MainPageFrameViewModel.Instance?.IsLoading = false;
        _ = LoadUserDataAsync();
    }

    private async Task LoadUserDataAsync()
    {
        if (Design.IsDesignMode)
        {
            return;
        }

        Core.App.CurrentLogger.LogDebug("开始加载用户数据");

        IsLoading = true;
        var ss = await MEFApiConverter.GetSystemStatusAsync();
        SystemStatus = ss.data?.status ?? -1;
        SystemStatusRemark = ss.data?.remark ?? $"网络服务不可用，返回代码: {ss.code}";
        if (ss.code != 200)
        {
            IsLoading = false;
            return;
        }
        
        var platform = await MEFApiConverter.GetPublicInfoAsync();
        if (platform.code == 200)
        {
            PlatformNodes = platform.data.nodes;
            PlatformUsers = platform.data.users;
            PlatformProxies = platform.data.proxies;
            PlatformTraffic = platform.data.traffic;
        }


        try
        {
            var res = await Task.Run(async () => await MEFApiConverter.GetExtraUserInfoAsync());
            var data = res.data;
            Core.App.CurrentLogger.LogDebug("结束加载用户数据, 状态码：" + res.code);

            UserName = data.username;
            Email = data.email;
            RegisterTime = DateTimeOffset.FromUnixTimeSeconds(data.regTime).LocalDateTime
                .ToString("yyyy-MM-dd HH:mm:ss");
            Traffic = ProcessFileSize(data.traffic);
            InBound = ProcessBoundSize(data.inBound);
            OutBound = ProcessBoundSize(data.outBound);
            UserId = $"# {data.userId}";
            Group = data.friendlyGroup;

            // 用户组样式
            IsAdmin = data.group == "管理员";

            // 实名状态
            if (data.isRealname)
            {
                RealNameStatus = "已实名";
                IsRealNamed = true;
            }
            else
            {
                RealNameStatus = "未实名";
                IsRealNamed = false;
            }

            // 账户状态
            AccountStatus = data.status switch
            {
                0 => "正常",
                1 => "封禁",
                2 => "流量超限",
                _ => "未知状态"
            };

            IsBanned = data.status == 1;

            // 签到按钮状态
            CanSign = !data.todaySigned;
            SignButtonText = !data.todaySigned ? "签到" : "已签到";

            ProxiesCount = $"{data.usedProxies}/{data.maxProxies}";

            // 加载公告
            NoticeContent = HtmlToMarkdownConverter.ConvertRawLinkToMarkdown(
                HtmlToMarkdownConverter.ConvertHtmlImagesToMarkdown((await MEFApiConverter.GetNoticeAsync()).data));

           
            IsLoading = false;
            var popUp = await MEFApiConverter.GetPopupNoticeAsync();

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

            if (Path.Exists(Path.Combine(Core.App.StartupPath, "RYCB.MEFrpLauncherX.CrashDisplayer.pmla")))
            {
                var btn = new TaskDialogButton
                {
                    DialogResult = TaskDialogStandardResult.Cancel,
                    Text = "取消",
                    Command = new RelayCommand(async _ =>
                    {
                    })
                };
                var cnt = "";
                var td = new TaskDialog
                {
                    Title = "PML Ⅱ 正在初始化",
                    ShowProgressBar = true,
                    IconSource = new SymbolIconSource { Symbol = Symbol.Download },
                    SubHeader = "正在解压资源文件",
                    Content = cnt,
                    Buttons =
                    {
                        btn
                    }
                };
                td.SetProgressBarState(0, TaskDialogProgressState.Indeterminate);
                td.XamlRoot = Core.App.MainWindow;
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
                Dispatcher.UIThread.Post(() =>
                {
                    td.Hide(TaskDialogStandardResult.OK);
                });
                File.Delete(Path.Combine(Core.App.StartupPath, "RYCB.MEFrpLauncherX.CrashDisplayer.pmla"));
            }
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger.Error(ex);
            await MessageBoxManager
                .GetMessageBoxStandard("错误", $"加载用户数据失败: {ex.Message}")
                .ShowAsync();
        }
        finally
        {
            MainPageFrameViewModel.Instance?.IsLoading = false;
            IsLoading = false;
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
            var (success, message) = await MEFApiConverter.SendSignRequestAsync(captchaResult.Trim());
            var signInfo =
                JsonConvert.DeserializeObject<InfoClasses.ApiInfo<InfoClasses.SignInfo>>(message);

            Core.App.CurrentLogger.Log($"API返回结果: {success}, {message}");
            if (success)
            {
                await MessageBoxManager
                    .GetMessageBoxStandard("签到成功", signInfo?.message)
                    .ShowAsync();
            }
            else
            {
                await MessageBoxManager
                    .GetMessageBoxStandard("签到失败", signInfo?.message)
                    .ShowAsync();
            }
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger.Error(ex);
            await MessageBoxManager
                .GetMessageBoxStandard("错误", $"签到过程中发生错误: {ex.Message}")
                .ShowAsync();
        }
        finally
        {
            await LoadUserDataAsync();
        }
    }

    private static string ProcessFileSize(long size)
    {
        string[] units = ["MB", "GB", "TB"];
        var unitIndex = 0;
        double adjustedSize = size;
        while (adjustedSize >= 1024 && unitIndex < units.Length - 1)
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
        while (adjustedSize >= 128 && unitIndex < units.Length - 1)
        {
            adjustedSize /= 128;
            unitIndex++;
        }

        return $"{adjustedSize:F2} {units[unitIndex]}";
    }

    public void Dispose()
    {
        GC.RemoveMemoryPressure(100 * 1024 * 1024);
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
    } = false;
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
            return JsonConvert.DeserializeObject<NoticeData>(jsonString) ?? new NoticeData();
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
            var jsonString = JsonConvert.SerializeObject(data);
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
                content: markdownRender,
                caption: "重要通知");
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