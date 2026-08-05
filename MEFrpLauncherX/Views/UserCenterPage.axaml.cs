using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.MEFIntegrated;
using MEFrpLauncherX.Core.MEFIntergrated;
using MEFrpLauncherX.Core.Storage;
using MEFrpLauncherX.ViewModels;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using ReactiveUI;

namespace MEFrpLauncherX.Views;

public partial class UserCenterPage : UserControl
{
    private UserCenterViewModel _vm;

    public UserCenterPage()
    {
        InitializeComponent();
        _vm = new UserCenterViewModel();
        DataContext = _vm;
        _vm.IcpDomainsChanged += (s, e) =>
        {
            UserControl_Loaded(null, null);
        };
    }

    public bool IsDark => ConfigManager.CurrentConfig.Theme.Equals("Dark", StringComparison.OrdinalIgnoreCase);

    private void OpenWeb(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://www.mefrp.com/dashboard/profile",
            UseShellExecute = true
        });
    }

    private async void UserControl_Loaded(object? s, VisualTreeAttachmentEventArgs? e)
    {
        if (Design.IsDesignMode)
        {
            return;
        }

        Core.App.CurrentLogger.LogDebug("开始加载用户数据");

        // Show loading mask
        MainPageFrameViewModel.Instance.IsLoading = true;

        try
        {
            await Task.Run(async () =>
            {
                var res = await MEFrpApiConverter.GetExtraUserInfoAsync();
                var data = res.data;
                Core.App.CurrentLogger.LogDebug("结束加载用户数据, 状态码：" + res.code);
                if (res.code != 200)
                {
                    return;
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    UserNameText.Text = data.username;
                    regMail.Text = data.email;
                    regTime.Text = DateTimeOffset.FromUnixTimeSeconds(data.regTime).LocalDateTime
                        .ToString("yyyy-MM-dd HH:mm:ss");
                    traffic.Text = ProcessFileSize(data.traffic);
                    inBound.Text = ProcessBoundSize(data.inBound);
                    outBound.Text = ProcessBoundSize(data.outBound);
                    usrId.Text = $"# {data.userId}";
                    group.Text = data.friendlyGroup;

                    group.Classes.Clear();
                    group.Classes.Add(data.group switch
                    {
                        "正式用户" => "Success",
                        "赞助者" => "Warning",
                        "管理员" => "Danger",
                        _ => "DefaultInfo"
                    });

                    if (data.isRealname)
                    {
                        isRealNamed.Text = "已实名";
                        isRealNamed.Classes.Clear();
                        isRealNamed.Classes.Add("Success");
                    }
                    else
                    {
                        isRealNamed.Text = "未实名";
                        isRealNamed.Classes.Clear();
                        isRealNamed.Classes.Add("Danger");
                    }

                    status.Text = data.status switch
                    {
                        0 => "正常",
                        1 => "封禁",
                        2 => "流量超限",
                        _ => "未知状态"
                    };

                    status.Classes.Clear();
                    status.Classes.Add(data.status switch
                    {
                        0 => "Success",
                        1 => "Danger",
                        2 => "Warning",
                        _ => "DefaultInfo"
                    });

                    TodaySignButton.IsVisible = true;
                    TodaySignButton.IsEnabled = !data.todaySigned;
                    TodaySignButton.Content = !data.todaySigned ? "签到" : "已签到";
                    proxies.Text = $"{data.usedProxies}/{data.maxProxies}";
                }, DispatcherPriority.Background);
                MainPageFrameViewModel.Instance.IsLoading = false;
                var trafficStatusData = await Task.Run(() => MEFrpApiConverter.GetTrafficStatusAsync(7));
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    trafficControl.UpdateTrafficData(trafficStatusData.data);
                    trafficControl.IsVisible = true;
                });
                var domains = await MEFrpApiConverter.GetIcpDomainListAsync();
                if (domains.code == 200)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        _vm.IcpDomains.Clear();
                        foreach (var domain in domains.data ?? [])
                        {
                            _vm.IcpDomains.Add(domain);
                        }

                        _vm.AddedDomains = _vm.IcpDomains.Count;
                    });
                }

                Core.App.CurrentLogger.Log($"数据已加载，用户名: {data.username}");
            });
        }
        finally
        {
            MainPageFrameViewModel.Instance.IsLoading = false;
        }
    }


    private async void Sign(object sender, RoutedEventArgs e)
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
                Growl.Success(signInfo?.message ?? "签到成功", "签到成功");
            }
            else
            {
                Growl.Error(signInfo?.message ?? "签到失败", "签到失败");
            }
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger.Error(ex);
            Growl.Error(ex.Message, "签到失败");
        }
        finally
        {
            UserControl_Loaded(sender, null); // 刷新用户数据
        }
    }

    private static string ProcessFileSize(ulong size)
    {
        string[] units = ["MB", "GB", "TB", "PB", "EB", "ZB", "YB"];
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

        while (adjustedSize >= 128 && unitIndex < units.Length - 1)
        {
            adjustedSize /= 128;
            unitIndex++;
        }

        return $"{adjustedSize:F2} {units[unitIndex]}";
    }

    private async void ExitLogin(object sender, RoutedEventArgs e)
    {
        var result = await MessageBoxManager.GetMessageBoxStandard("警告", "确定要退出登录吗？退出登录后软件将重启。", ButtonEnum.YesNo)
            .ShowAsync();

        if (result != ButtonResult.Yes)
        {
            return;
        }

        UserCache.Logout();
        try
        {
            // 使用独立的重启器进程（避免文件占用问题）
            var tempBat = Path.Combine(Path.GetTempPath(), "restart.bat");
            await File.WriteAllTextAsync(tempBat, $"""

                                                   @echo off
                                                   timeout /t 1 /nobreak >nul
                                                   start "" "{Environment.ProcessPath}"
                                                   del "%~f0"
                                                   """);

            Process.Start(new ProcessStartInfo
            {
                FileName = tempBat,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            });
            App.Desktop.Shutdown();
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger.Error(ex);
            Environment.Exit(0); // 最简化的强制退出
        }
    }
}

public partial class UserCenterViewModel : ViewModelBase
{
    public event EventHandler? IcpDomainsChanged;

    public int AddedDomains
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public AvaloniaList<InfoClasses.IcpDomain> IcpDomains
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ReactiveCommand<InfoClasses.IcpDomain, Unit> DeleteIcpDomainCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> AddDomainCommand
    {
        get;
    }

    public bool IsWorking
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public UserCenterViewModel()
    {
        IcpDomains = [];
        DeleteIcpDomainCommand = ReactiveCommand.CreateFromTask<InfoClasses.IcpDomain>(async (domain) =>
        {
            try
            {
                IsWorking = true;
                await MEFrpApiConverter.DeleteIcpDomainAsync(domain.domain);
                IcpDomains.Remove(domain);
                AddedDomains--;
                Growl.Success($"删除成功: {domain.domain}");
            }
            catch (Exception ex)
            {
                Core.App.CurrentLogger.Error(ex);
                Growl.Error(ex.Message, "删除失败");
            }
            finally
            {
                IsWorking = false;
            }
        }, this.WhenAnyValue(x => x.IsWorking, (working) => !working));
        AddDomainCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            var domainBox = new TextBox()
            {
                Watermark = "请输入备案域名",
                Margin = new Thickness(10),
            };
            var inputDialog = new ContentDialog()
            {
                Title = "添加备案域名",
                Content = domainBox,
                PrimaryButtonText = "添加",
                CloseButtonText = "取消",
                IsPrimaryButtonEnabled = true,
                DefaultButton = ContentDialogButton.Primary,
            };
            var res = await inputDialog.ShowAsync();
            if (res != ContentDialogResult.Primary || domainBox.Text.IsNullOrEmpty())
            {
                return;
            }

            if (IcpDomains.Any(d => d.domain == domainBox.Text))
            {
                Growl.Info("该备案域名已存在", "添加失败");
                return;
            }

            var domainRegex = DomainRegex();
            if (!domainRegex.IsMatch(domainBox.Text))
            {
                Growl.Warning("请输入正确的备案域名", "添加失败");
                return;
            }

            var result = domainBox.Text;

            try
            {
                IsWorking = true;
                await MEFrpApiConverter.AddIcpDomainAsync(result.Trim());
                var domains = await MEFrpApiConverter.GetIcpDomainListAsync();
                IcpDomains.Clear();
                if (domains.data == null || domains.data?.Count == 0)
                {
                    Growl.Warning("没有备案域名", "添加失败");
                    return;
                }

                foreach (var domain in domains.data)
                {
                    IcpDomains.Add(domain);
                }

                AddedDomains = IcpDomains.Count;
                Growl.Success($"添加成功: {result.Trim()}");
            }
            catch (Exception ex)
            {
                Core.App.CurrentLogger.Error(ex);
                Growl.Warning(ex.Message, "添加失败");
            }
            finally
            {
                IsWorking = false;
            }
        }, this.WhenAnyValue(x => x.IsWorking, (working) => !working));
    }

    [GeneratedRegex(@"^(?=.{1,255}$)([0-9A-Za-z](?:[0-9A-Za-z-]{0,61}[0-9A-Za-z])?\.)+[A-Za-z]{2,}$")]
    private static partial Regex DomainRegex();
}