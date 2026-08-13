using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FluentAvalonia.UI.Controls;
using MarkdownAIRender.Controls.MarkdownRender;
using MarkdownAIRender.Helper;
using MEFrpLauncherX.Controls;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Core.Services;
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
                hitokoto = Languages.Text_About_HitokotoFallback,
                from = Languages.Text_About_HitokotoSource,
                from_who = Languages.Text_About_HitokotoAuthor
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
                Core.App.CurrentLogger.Error(ex, port: EnumLogPort.Server, type: EnumLogType.Warn);
                Hitokoto = null;
            }

            if (Hitokoto == null)
            {
                if (Random.Shared.Next() % 2 == 0)
                {
                    Hitokoto = new HitokotoResource
                    {
                        hitokoto = CrashHandler.Jokes[Random.Shared.Next(CrashHandler.Jokes.Length)],
                        from = Languages.Text_About_MicrosoftStyleChinese,
                        creator = "Microsoft"
                    };
                }

                Core.App.CurrentLogger.Log("获取一言失败", EnumLogType.Error, EnumLogPort.Client, EnumLogModule.Net);
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
            CloseButtonText = Languages.Text_Global_Close
        };
        await cd.ShowAsync();
    }

    #region 帮助/支持相关

    private async void OpenSource_Click(object? sender, RoutedEventArgs e)
    {
        var stackPanel = new StackPanel { Spacing = 8, Margin = new Thickness(0, 8) };
        foreach (var lib in vm.OpenSourceLibraries)
        {
            var row = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
            var link = new HyperlinkButton
            {
                Content = lib.Name,
                FontFamily = (Avalonia.Media.FontFamily)Application.Current?.FindResource("GlobalFontFamily")!,
                BorderBrush = Avalonia.Media.Brushes.Transparent,
                Background = Avalonia.Media.Brushes.Transparent,
                BorderThickness = new Thickness(-1),
                Padding = new Thickness(0),
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
            Title = Languages.Text_About_OpenSourceSoftware,
            Content = scrollViewer,
            PrimaryButtonText = Languages.Text_Global_Close,
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
            Languages.Text_About_GroupRules,
            Languages.Caption_Hint,
            [
                new TaskDialogButton(Languages.Text_About_AcknowledgeAndGo, TaskDialogStandardResult.Yes),
                new TaskDialogButton(Languages.Text_About_CopyGroupNumber, TaskDialogStandardResult.No),
                new TaskDialogButton(Languages.Text_Global_Cancel, TaskDialogStandardResult.Cancel)
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
            Growl.Success(Languages.Text_About_GroupNumberCopied);
        }
    }

    private async void OQ2_Click(object? sender, RoutedEventArgs e)
    {
        var res = await MessageBox.ShowAsync(
            Languages.Text_About_GroupRules,
            Languages.Caption_Hint,
            [
                new TaskDialogButton(Languages.Text_About_AcknowledgeAndGo, TaskDialogStandardResult.Yes),
                new TaskDialogButton(Languages.Text_About_CopyGroupNumber, TaskDialogStandardResult.No),
                new TaskDialogButton(Languages.Text_Global_Cancel, TaskDialogStandardResult.Cancel)
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
            Growl.Success(Languages.Text_About_GroupNumberCopied);
        }
    }

    private async void FS_Click(object sender, RoutedEventArgs e)
    {
        var res = await MessageBox.ShowAsync(
            Languages.Text_About_GroupRules,
            Languages.Caption_Hint,
            [
                new TaskDialogButton(Languages.Text_About_AcknowledgeAndGo, TaskDialogStandardResult.Yes),
                new TaskDialogButton(Languages.Text_Global_Cancel, TaskDialogStandardResult.Cancel)
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
            FileName = "https://docs.rycb.tech/pml-2/user_agreement",
            UseShellExecute = true
        });
    }

    private void Privacy_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://docs.rycb.tech/pml-2/policy",
            UseShellExecute = true
        });
    }

    private async void Doc_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName =
                "https://docs.rycb.mxj.pub/pml-2",
            UseShellExecute = true
        });
        await MessageBox.ShowAsync(Languages.Text_About_DocOpened, buttons:
        [
            new TaskDialogButton(Languages.Text_About_Source1, MessageBoxResult.Yes)
            {
                Command = new RelayCommand((s) =>
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName =
                            "https://docs.rycb.tech/pml-2",
                        UseShellExecute = true
                    });
                })
            },
            new TaskDialogButton(Languages.Text_About_Source2, MessageBoxResult.No)
            {
                Command = new RelayCommand((s) =>
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://docs.rycb.tech/pml-2/user_guide",
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
        vm.SubmitProgress = 0;
        vm.IsSubmittingFeedback = true;
        var askForm = new ContentDialog()
        {
            Content = Languages.Text_About_ReportIssue_Method,
            PrimaryButtonText = Languages.Text_About_Source1,
            CloseButtonText = Languages.Text_About_Source2,
            DefaultButton = ContentDialogButton.Primary
        };
        var askFormResult = await askForm.ShowAsync();
        if (askFormResult == ContentDialogResult.Primary)
        {
            var feedbackForm = new FeedbackForm();
            var cd = new ContentDialog
            {
                Content = feedbackForm,
                IsPrimaryButtonEnabled = true,
                PrimaryButtonText = Languages.Text_Global_Confirm,
                MinHeight = 200,
                DefaultButton = ContentDialogButton.Primary,
                CloseButtonText = Languages.Text_Global_Cancel,
            };
            var res = await cd.ShowAsync();
            if (!(feedbackForm.Email.IsNullOrEmpty() ||
                  feedbackForm.Feedback.IsNullOrEmpty()) || res == ContentDialogResult.Primary)
            {
                //var res = await RYCBApiConverter.SendFeedBackAsync(feedbackForm.Email, feedbackForm.Feedback);
                //if (res.success)
                //{
                vm.SubmitProgress = 0.4;
                await RYCBApiConverter.SendEmailAsync("html", "rycbqyf@163.com",
                    $"收到反馈: {feedbackForm.Feedback} <br>用户邮箱:{feedbackForm.Email} <br>时间:{DateTime.Now:O}",
                    "收到反馈 | RYCB 内部通知");
                vm.SubmitProgress = 1.5;
                await RYCBApiConverter.SendEmailAsync("html", feedbackForm.Email,
                    Languages.Text_About_FeedbackEmailBody,
                    Languages.Text_About_FeedbackEmailSubject);
                vm.SubmitProgress = 2.6;
                Growl.Success(Languages.Text_About_FeedbackSubmitted);
                vm.SubmitProgress = 3;
                await Task.Delay(500);
                vm.IsSubmittingFeedback = false;
            }
        }
        else
        {
            UrlHelper.OpenUrl("https://github.com/RYCBStudio/PML-2/issues");
        }
    }

    private void Gh_Click(object? sender, RoutedEventArgs e)
    {
        UrlHelper.OpenUrl("https://github.com/RYCBStudio/PML-2");
    }

    private async void CP_Click(object? sender, RoutedEventArgs e)
    {
        var cd = new ContentDialog()
        {
            Title = Languages.Text_About_CompleteCopyrightStatement,
            Content = Languages.Text_About_CompleteCopyrightStatement_Content,
            PrimaryButtonText = Languages.Text_Global_Confirm,
            CloseButtonText = Languages.Text_Global_Cancel,
            DefaultButton = ContentDialogButton.Primary
        };
        await cd.ShowAsync();
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

        if (response.StatusCode != HttpStatusCode.OK)
        {
            Core.App.CurrentLogger.Log("获取一言失败", EnumLogType.Error, EnumLogPort.Client, EnumLogModule.Net);
            return null;
        }

        var result =
            JsonSerializer.Deserialize<HitokotoResource>(response.Content,
                App.AppJsonSerializerContext.HitokotoResource);
        return result;
    }

    #endregion

    #region 一言相关

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
            hitokoto = Languages.Text_About_HitokotoFallback,
            from = Languages.Text_About_HitokotoSource
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
            Core.App.CurrentLogger.Error(ex, port: EnumLogPort.Server, type: EnumLogType.Warn);
            Hitokoto = null;
        }

        if (Hitokoto == null)
        {
            if (Random.Shared.Next() % 2 == 0)
            {
                Hitokoto = new HitokotoResource
                {
                    hitokoto = CrashHandler.Jokes[Random.Shared.Next(CrashHandler.Jokes.Length)],
                    from = Languages.Text_About_MicrosoftStyleChinese,
                    creator = "Microsoft"
                };
            }

            Core.App.CurrentLogger.Log("获取一言失败", EnumLogType.Error, EnumLogPort.Client, EnumLogModule.Net);
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

    private void OpenProxyFloat(object? sender, RoutedEventArgs e) => new ProxyMonitor.ProxyFloat().Show();

    #endregion

    #region 工具箱

    private async void ExportLog(object? sender, RoutedEventArgs e)
    {
        try
        {
            var folders = await Core.App.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = Languages.Text_About_ToolBox_ExportLog_SelectFolder,
                AllowMultiple = false
            });
            if (folders.Count == 0)
            {
                return;
            }

            var exportDir = await Task.Run(() => ToolboxService.ExportLogs(folders[0].Path.LocalPath));
            if (exportDir == null)
            {
                await MessageBox.ShowAsync(Languages.Text_About_ToolBox_ExportLog_NoLogs, Languages.Caption_Hint,
                    MessageBoxIcon.Warning);
                return;
            }

            Growl.Success($"{Languages.Text_About_ToolBox_ExportLog_Success}\n{exportDir}");
            OpenFolderInExplorer(exportDir);
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger?.Error(ex, port: EnumLogPort.Client, module: EnumLogModule.Main);
            await MessageBox.ShowAsync(Languages.Text_About_ToolBox_ExportLog_Failed, Languages.Caption_Error,
                MessageBoxIcon.Error);
        }
    }

    private async void ClearCache(object? sender, RoutedEventArgs e)
    {
        var dialog = new CacheCleanupDialog();
        var cd = new ContentDialog
        {
            Title = Languages.Text_About_ToolBox_ClearCache_Dialog_Title,
            Content = dialog,
            CloseButtonText = Languages.Text_Global_Close,
        };
        await cd.ShowAsync();
    }

    private void OpenLogsFolder(object? sender, RoutedEventArgs e)
    {
        var logsDir = Path.Combine(Core.App.StartupPath, "Logs");
        if (!Directory.Exists(logsDir))
        {
            Directory.CreateDirectory(logsDir);
        }

        OpenFolderInExplorer(logsDir);
    }

    private void OpenRootFolder(object? sender, RoutedEventArgs e) => OpenFolderInExplorer(Core.App.StartupPath);

    private async void CopyDiagnostics(object? sender, RoutedEventArgs e)
    {
        var info = $"""
                   PML {Core.App.Version} ({Core.App.ReleaseFlag})
                   MEFrp Client: {Core.App.MEFrpVersion}
                   OS: {Environment.OSVersion.VersionString} ({RuntimeInformation.OSArchitecture})
                   Runtime: {RuntimeInformation.FrameworkDescription}
                   Culture: {CultureInfo.CurrentUICulture.Name}
                   """;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(info);
            Growl.Success(Languages.Text_About_ToolBox_CopyDiagnostics_Copied);
        }
        else
        {
            Growl.Error(Languages.Text_About_ToolBox_CopyDiagnostics_Failed);
        }
    }

    /// <summary>
    ///     在系统文件管理器中打开指定文件夹（跨平台）。
    /// </summary>
    private void OpenFolderInExplorer(string folderPath)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer",
                    Arguments = $"\"{folderPath}\"",
                    UseShellExecute = true
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = $"\"{folderPath}\"",
                    UseShellExecute = true
                });
            }
            else
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = $"\"{folderPath}\"",
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger?.Error(ex, port: EnumLogPort.Client, module: EnumLogModule.Main);
        }
    }

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