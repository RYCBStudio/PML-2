using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using FluentAvalonia.UI.Controls;
using MarkdownAIRender.Controls.MarkdownRender;
using MEFrpLauncherX.Controls;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.ViewModels;
using MsBox.Avalonia.ViewModels.Commands;
using RestSharp;

#pragma warning disable CS8602 // 解引用可能出现空引用。

namespace MEFrpLauncherX.Views;

public partial class AboutPage : UserControl
{
    private readonly AboutViewModel vm;
    private int debug_count;

    public AboutPage()
    {
        InitializeComponent();
        vm = new AboutViewModel();
        DataContext = vm;
        AttachedToVisualTree += async (s, e) =>
        {
            HitokotoStatus.Show();
            if (Design.IsDesignMode)
            {
                return;
            }

            HitokotoBox.Text = string.Empty;
            HitokotoResource Hitokoto = new()
            {
                hitokoto = "用代码表达言语的魅力，用代码书写山河的壮丽。",
                from = "一言「一言开发者中心」",
                from_who = "一言开发者"
            };
            MainPageFrameViewModel.Instance?.IsLoading = true;
            try
            {
                Hitokoto = await ExecuteHitokotoRequest(CreateRequest(), "一言");
            }
            catch (ArgumentNullException)
            {
                Core.App.CurrentLogger.Log("获取一言失败，使用备用源", EnumLogType.Warn, EnumLogPort.Client, EnumLogModule.Net);
                Hitokoto = await Task.Run(() => ExecuteHitokotoBackupRequest(CreateRequest(), "一言"));
            }
            catch (Exception ex)
            {
                if (Random.Shared.Next() % 2 == 0)
                {
                    Hitokoto = new HitokotoResource
                    {
                        hitokoto = CrashHandler.Jokes[Random.Shared.Next(CrashHandler.Jokes.Length)],
                        from = "微软式中文",
                        creator = "Microsoft"
                    };
                }

                Core.App.CurrentLogger.Log("获取一言失败", EnumLogType.Error, EnumLogPort.Client, EnumLogModule.Net);
                Core.App.CurrentLogger.Error(ex, port: EnumLogPort.Server, type: EnumLogType.Warn);
            }

            vm.Hitokoto = Hitokoto.hitokoto;
            vm.From = Hitokoto.from;
            vm.Author = Hitokoto.from_who;
            Hitokoto = null;
            //MainPageFrameViewModel.Instance.CurrentPage = this;
            MainPageFrameViewModel.AboutPage = this;
            MainPageFrameViewModel.Instance.IsLoading = false;
            HitokotoStatus.Hide();
        };
    }

    private async void Debug_TestMarkdown(object? sender, RoutedEventArgs e)
    {
        var cd = new ContentDialog
        {
            Content = new MarkdownRender
            {
                Value = """
                        # Markdown 渲染测试

                        ## HTML 块测试

                        <div>这是一个 HTML div 块</div>

                        <p>这是一个段落标签</p>

                        <strong>这是粗体文本</strong> 和普通文本混合

                        <br/>换行测试

                        ## GitHub 风格警告框测试

                        > [!NOTE]
                        > 这是一个注意事项，用于提醒用户重要信息。

                        > [!TIP]
                        > 这是一个提示，提供有用的建议或技巧。

                        > [!WARNING]
                        > 这是一个警告，提醒用户注意潜在的问题。

                        > [!CAUTION]
                        > 这是一个重要提示，需要用户特别注意的事项。

                        ## 其他 Markdown 功能测试

                        ### 代码块

                        ```csharp
                        public class Test
                        {
                            public void Hello()
                            {
                                Console.WriteLine("Hello, World!");
                            }
                        }
                        ```

                        ### 列表

                        - 项目 1
                        - 项目 2
                        - 项目 3

                        ### 引用

                        > 这是一段引用文本

                        ### 链接

                        [这是一个链接](https://example.com)

                        ### 图片

                        ![示例图片](https://via.placeholder.com/150)

                        """
            },
            CloseButtonText = "关闭"
        };
        await cd.ShowAsync();
    }

    #region 帮助/支持相关

    private async void OpenSource_Click(object? sender, RoutedEventArgs e)
    {
        var stackPanel = new StackPanel { Spacing = 8, Margin = new Avalonia.Thickness(0, 8) };
        foreach (var lib in vm.OpenSourceLibraries)
        {
            var row = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
            var link = new HyperlinkButton
            {
                Content = lib.Name,
                FontFamily = (Avalonia.Media.FontFamily)Application.Current?.FindResource("GlobalFontFamily")!,
                BorderBrush = Avalonia.Media.Brushes.Transparent,
                Background = Avalonia.Media.Brushes.Transparent,
                BorderThickness = new Avalonia.Thickness(-1),
                Padding = new Avalonia.Thickness(0),
            };
            link.Click += (_, _) =>
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = lib.Url,
                    UseShellExecute = true
                });
            };
            var license = new TextBlock
            {
                Text = lib.License,
                FontFamily = (Avalonia.Media.FontFamily)Application.Current?.FindResource("GlobalFontFamily")!,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Foreground = Avalonia.Media.Brushes.Gray,
                FontSize = 12,
            };
            row.Children.Add(link);
            row.Children.Add(license);
            stackPanel.Children.Add(row);
        }

        var scrollViewer = new ScrollViewer
        {
            Content = stackPanel,
            MaxHeight = 450,
        };

        var cd = new ContentDialog
        {
            Title = "开源软件",
            Content = scrollViewer,
            PrimaryButtonText = "关闭",
            DefaultButton = ContentDialogButton.Primary,
            IsSecondaryButtonEnabled = false,
        };
        await cd.ShowAsync();
    }

    private void OS_Click(object sender, RoutedEventArgs e)
    {
        // var window = new OpenSourceDependenciesWindow
        // {
        //     DataContext = new OpenSourceDependenciesViewModel()
        // };
        // window.ShowDialog();
    }

    private async void OQ_Click(object sender, RoutedEventArgs e)
    {
        var res = await MessageBox.ShowAsync(
            "请友善、真诚提问，杜绝任何跳脸和违法行为，一经发现，立刻踢出。",
            "提示",
            [
                new TaskDialogButton("我已知晓, 前往", TaskDialogStandardResult.Yes),
                new TaskDialogButton("复制群号", TaskDialogStandardResult.No),
                new TaskDialogButton("取消", TaskDialogStandardResult.Cancel)
            ]);

        if (res == MessageBoxResult.Yes)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName =
                    "https://qm.qq.com/cgi-bin/qm/qr?authKey=W%2BsWnBZYMUyqre2CMvoILZ4TQniiva5PNFFYkBtY0TaMNb%2BSWiToLDbiglufNaaT&k=bqlThMvikRF4ZaOwEq_ckpedjzthHccE&noverify=0",
                UseShellExecute = true
            });
        }
        else if (res == MessageBoxResult.No)
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            await clipboard?.SetTextAsync("1019501085");
            Growl.Success("群号已复制到剪贴板");
        }
    }

    private async void OQ2_Click(object? sender, RoutedEventArgs e)
    {
        var res = await MessageBox.ShowAsync(
            "请友善、真诚提问，杜绝任何跳脸和违法行为，一经发现，立刻踢出。",
            "提示",
            [
                new TaskDialogButton("我已知晓, 前往", TaskDialogStandardResult.Yes),
                new TaskDialogButton("复制群号", TaskDialogStandardResult.No),
                new TaskDialogButton("取消", TaskDialogStandardResult.Cancel)
            ]);

        if (res == MessageBoxResult.Yes)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName =
                    "https://qm.qq.com/q/fBtW7lJ1Xa",
                UseShellExecute = true
            });
        }
        else if (res == MessageBoxResult.No)
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            await clipboard?.SetTextAsync("708797546");
            Growl.Success("群号已复制到剪贴板");
        }
    }

    private async void FS_Click(object sender, RoutedEventArgs e)
    {
        var res = await MessageBox.ShowAsync(
            "请友善、真诚提问，杜绝任何跳脸和违法行为，一经发现，立刻踢出。",
            "提示",
            [
                new TaskDialogButton("我已知晓, 前往", TaskDialogStandardResult.Yes),
                new TaskDialogButton("取消", TaskDialogStandardResult.Cancel)
            ]);
        if (res == MessageBoxResult.Yes)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName =
                    "https://applink.feishu.cn/client/chat/chatter/add_by_link?link_token=ccbqa557-698a-479d-a495-877f0c283c37",
                UseShellExecute = true
            });
        }
    }

    private void SM_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "mailto://support@mefrp.com",
            UseShellExecute = true
        });
    }

    private void MEFC_Click(object sender, RoutedEventArgs e)
    {
        var exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", "mefrpc.exe");
        OpenFileInExplorer(exePath);
    }

    private void OpenFileInExplorer(string filePath)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Windows 使用 explorer
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer",
                Arguments = $@"/select,""{filePath}""",
                UseShellExecute = true
            });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Linux 尝试使用 xdg-open 或具体的文件管理器
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = $@"{Path.GetDirectoryName(filePath)}",
                    UseShellExecute = true
                });
            }
            catch
            {
                // 如果 xdg-open 不可用，尝试常见的 Linux 文件管理器
                string[] fileManagers = ["nautilus", "dolphin", "thunar", "pcmanfm", "nemo"];

                foreach (var manager in fileManagers)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = manager,
                            Arguments = $@"{Path.GetDirectoryName(filePath)}",
                            UseShellExecute = true
                        });
                        break;
                    }
                    catch
                    {
                        /* 忽略错误，尝试下一个 */
                    }
                }
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // macOS 使用 open
            Process.Start(new ProcessStartInfo
            {
                FileName = "open",
                Arguments = $@"-R ""{filePath}""",
                UseShellExecute = true
            });
        }
    }

    private void UA_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://rycb.mxj.pub/mefl/useragreement.html",
            UseShellExecute = true
        });
    }

    private void Privacy_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://rycb.mxj.pub/mefl/privacy.html",
            UseShellExecute = true
        });
    }

    private async void Doc_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName =
                "https://docs.rycb.mxj.pub/pml-2/intro",
            UseShellExecute = true
        });
        await MessageBox.ShowAsync("已打开文档。若无法访问, 请选择以下备用源: ", buttons:
        [
            new TaskDialogButton("源1", MessageBoxResult.Yes)
            {
                Command = new RelayCommand((s) =>
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName =
                            "https://docs.rycb.tech/pml-2/intro",
                        UseShellExecute = true
                    });
                })
            },
            new TaskDialogButton("源2", MessageBoxResult.No)
            {
                Command = new RelayCommand((s) =>
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://docs.rycb.mxj.pub/pml-2/intro",
                        UseShellExecute = true
                    });
                })
            }
        ]);
    }

    private void MEFCL_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://mefrp-tpc.yealqp.fun/",
            UseShellExecute = true
        });
    }

    private async void Support_Click(object sender, RoutedEventArgs e)
    {
        var feedbackForm = new FeedbackForm();
        var cd = new ContentDialog
        {
            Content = feedbackForm,
            IsPrimaryButtonEnabled = true,
            PrimaryButtonText = "确定",
            MinHeight = 200
        };
        await cd.ShowAsync();
        if (!(feedbackForm.Email.IsNullOrEmpty() ||
              feedbackForm.Feedback.IsNullOrEmpty()))
        {
            var res = await RYCBApiConverter.SendFeedBackAsync(feedbackForm.Email, feedbackForm.Feedback);
            if (res.success)
            {
                await RYCBApiConverter.SendEmailAsync("html", "rycbqyf@163.com",
                    $"收到反馈: {feedbackForm.Feedback} <br>用户邮箱:{feedbackForm.Email} <br>时间:{DateTime.Now:O}",
                    "收到反馈 | RYCB 内部通知");
                await RYCBApiConverter.SendEmailAsync("html", feedbackForm.Email, "您的反馈已提交成功。我们将尽快处理您的反馈。",
                    "反馈已提交成功 | RYCB Studio");
                Growl.Success("反馈提交成功");
            }
            else
            {
                Growl.Error(res.message, "反馈提交失败");
            }
        }
    }

    #endregion

    #region HTTP请求

    private static RestRequest CreateRequest(Method method = Method.Get)
    {
        var request = new RestRequest { Method = method };
        request.AddHeader("Content-Type", "application/json");

        return request;
    }

    private static async Task<HitokotoResource> ExecuteHitokotoRequest(RestRequest request, string operationName)
    {
        Core.App.CurrentLogger.Log($"正在获取{operationName}", port: EnumLogPort.Client, module: EnumLogModule.Net);

        using var client = new RestClient(new RestClientOptions("https://v1.hitokoto.cn")
        {
            Timeout = TimeSpan.FromSeconds(3)
        });
        var response = await client.ExecuteAsync(request);

        Core.App.CurrentLogger.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);

        var result =
            JsonSerializer.Deserialize<HitokotoResource>(response.Content,
                App.AppJsonSerializerContext.HitokotoResource);
        return result;
    }

    private static async Task<HitokotoResource> ExecuteHitokotoBackupRequest(RestRequest request,
        string operationName)
    {
        Core.App.CurrentLogger.Log($"正在获取{operationName}", port: EnumLogPort.Client, module: EnumLogModule.Net);

        using var client = new RestClient("https://hitokoto.yealqp.cn/");
        var response = await client.ExecuteAsync(request);

        Core.App.CurrentLogger.Log($"状态: {response.StatusCode}", port: EnumLogPort.Server, module: EnumLogModule.Net);

        var result =
            JsonSerializer.Deserialize<HitokotoResource>(response.Content,
                App.AppJsonSerializerContext.HitokotoResource);
        return result;
    }

    #endregion

    #region 一言相关

    private async Task FadeOutAsync(Control control)
    {
        var animation = new Animation
        {
            Duration = TimeSpan.FromSeconds(0.3),
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0),
                    Setters = { new Setter(OpacityProperty, 1.0) }
                },
                new KeyFrame
                {
                    Cue = new Cue(1),
                    Setters = { new Setter(OpacityProperty, 0.0) }
                }
            }
        };

        await animation.RunAsync(control);
    }

    private async Task FadeInAsync(Control control)
    {
        var animation = new Animation
        {
            Duration = TimeSpan.FromSeconds(0.3),
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0),
                    Setters = { new Setter(OpacityProperty, 0.0) }
                },
                new KeyFrame
                {
                    Cue = new Cue(1),
                    Setters = { new Setter(OpacityProperty, 1.0) }
                }
            }
        };

        await animation.RunAsync(control);
    }

    private async void RefreshHitokoto_Click(object sender, RoutedEventArgs e)
    {
        HitokotoStatus.Show();
        // 确保控件存在
        if (HitokotoBox == null)
        {
            return;
        }

        vm.Hitokoto = "";
        vm.Author = "";
        vm.From = "";

        // 获取新一言
        HitokotoResource Hitokoto = new()
        {
            hitokoto = "用代码表达言语的魅力，用代码书写山河的壮丽。",
            from = "一言「一言开发者中心」"
        };
        try
        {
            Hitokoto = await Task.Run(() => ExecuteHitokotoRequest(CreateRequest(), "一言"));
        }
        catch (ArgumentNullException)
        {
            Core.App.CurrentLogger.Log("获取一言失败，使用备用源", EnumLogType.Warn, EnumLogPort.Client, EnumLogModule.Net);
            Hitokoto = await Task.Run(() => ExecuteHitokotoBackupRequest(CreateRequest(), "一言"));
        }
        catch (Exception ex)
        {
            if (Random.Shared.Next() % 2 == 0)
            {
                Hitokoto = new HitokotoResource
                {
                    hitokoto = CrashHandler.Jokes[Random.Shared.Next(CrashHandler.Jokes.Length)],
                    from = "微软式中文",
                    from_who = "Microsoft"
                };
            }

            Core.App.CurrentLogger.Log("获取一言失败", EnumLogType.Error, EnumLogPort.Client, EnumLogModule.Net);
            Core.App.CurrentLogger.Error(ex, port: EnumLogPort.Server);
        }

        vm.Hitokoto = Hitokoto.hitokoto;
        vm.From = Hitokoto.from;
        vm.Author = Hitokoto.from_who;
        Hitokoto = null;

        HitokotoStatus.Hide();
    }

    #endregion

    #region DEBUG

    private void EnableDebugMode(object? sender, PointerPressedEventArgs e)
    {
        debug_count++;
        if (debug_count == 7)
        {
            DebugPanel.IsVisible = true;
        }
    }

    private void Debug_ThrowException(object? sender, RoutedEventArgs e) =>
        throw new ApplicationException("这是一个测试异常", new StackOverflowException("Stack Overflow，小子!"));

    private async void Debug_TestSolve(object? sender, RoutedEventArgs e) => await LoginPage.GetCaptchaResultAsync();

    private void OpenProxyFloat(object? sender, RoutedEventArgs e) => new ProxyFloat().Show();

    #endregion
}

public class HitokotoResource
{
    public int id
    {
        get;
        set;
    }

    public string uuid
    {
        get;
        set;
    }

    public string hitokoto
    {
        get;
        set;
    }

    public string type
    {
        get;
        set;
    }

    public string from
    {
        get;
        set;
    }

    public string from_who
    {
        get;
        set;
    }

    public string creator
    {
        get;
        set;
    }

    public int creator_uid
    {
        get;
        set;
    }

    public int reviewer
    {
        get;
        set;
    }

    public string commit_from
    {
        get;
        set;
    }

    public string created_at
    {
        get;
        set;
    }

    public int length
    {
        get;
        set;
    }
}