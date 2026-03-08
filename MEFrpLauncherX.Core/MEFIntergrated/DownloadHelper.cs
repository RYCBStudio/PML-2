using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Timers;
using Avalonia;
using Avalonia.Threading;
using Downloader;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Windowing;
using DownloadProgressChangedEventArgs = Downloader.DownloadProgressChangedEventArgs;
using RelayCommand = MEFrpLauncherX.Core.Controls.RelayCommand;
using Timer = System.Timers.Timer;

#pragma warning disable CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法

// ReSharper disable InconsistentNaming

namespace MEFrpLauncherX.Core.MEFIntergrated;

public partial class DownloadHelper
{
    private readonly Timer jokeTimer; // 添加计时器
    private string currentJoke; // 当前显示的笑话

    private string content =
        $"请稍等，正在下载文件\n文件名:{Environment.OSVersion.Platform switch {
            PlatformID.Win32NT => "mefrpc-windows.exe",
            PlatformID.Unix => OperatingSystem.IsMacOS() ? "mefrpc-darwin.tar" : "mefrpc-unix.tar",
            PlatformID.MacOSX => "mefrpc-darwin.tar",
            _ => "未知"
        }}" +
        $"\n当前线路: {{1}}\n下载速度: {{0}}\n------------------\nJOKE_PLACEHOLDER\n------------------";

    private readonly TaskDialog td;
    private readonly Visual? VisualRoot;
    private readonly CancellationTokenSource cts;

    internal DownloadService downloader;
    private bool isCancelled;

    public DownloadHelper(Visual? VisualRoot)
    {
        cts = new CancellationTokenSource();
        this.VisualRoot = VisualRoot;
        jokeTimer = new Timer(1000);
        jokeTimer.Elapsed += OnJokeTimerElapsed;
        jokeTimer.AutoReset = true;
        var DownloadOpt = new DownloadConfiguration
        {
            BufferBlockSize = 1024 * 32, // 通常，主机最大支持8000字节，默认值为8000。
            ChunkCount = 8, // 要下载的文件分片数量，默认值为1
            MaxTryAgainOnFailure = 5, // 失败的最大次数
            ParallelDownload = ConfigManager.CurrentConfig.ParallelDownload, // 下载文件是否为并行的。默认值为false
            ParallelCount = ConfigManager.CurrentConfig.ParallelCount,
            Timeout = 5000, // 每个 stream reader  的超时（毫秒），默认值是1000

            RequestConfiguration = // 定制请求头文件
            {
                Accept = "*/*",
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                CookieContainer = new CookieContainer(), // Add your cookies
                Headers = [], // Add your custom headers
                KeepAlive = true,
                ProtocolVersion = HttpVersion.Version11, // Default value is HTTP 1.1
                UseDefaultCredentials = false,
                UserAgent =
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36 Edg/126.0.0.0",
            }
        };
        downloader = new DownloadService(DownloadOpt);
        downloader.DownloadStarted += DownloaderOnDownloadStarted;
        downloader.DownloadProgressChanged += DownloaderOnDownloadProgressChanged;
        downloader.DownloadFileCompleted += DownloaderOnDownloadFileCompleted;
        var btn = new TaskDialogButton
        {
            DialogResult = TaskDialogStandardResult.Cancel,
            Text = "取消",
            Command = new RelayCommand(async _ =>
            {
                downloader.CancelAsync();
                await downloader.CancelTaskAsync();
                isCancelled = true;
                cts.Cancel();
            })
        };
        td = new TaskDialog
        {
            Title = "PML Ⅱ 正在下载文件",
            ShowProgressBar = true,
            IconSource = new SymbolIconSource { Symbol = Symbol.Download },
            SubHeader = "正在下载ME Frp客户端",
            Content =
                content,
            Buttons =
            {
                btn
            }
        };
        td.SetProgressBarState(0, TaskDialogProgressState.Indeterminate);
        currentJoke = GetRandomJoke();
    }

    // 获取随机笑话的方法
    private string GetRandomJoke()
    {
        return _jokes[Random.Shared.Next(0, _jokes.Length)];
    }

    // 计时器事件处理
    private void OnJokeTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        currentJoke = GetRandomJoke();
        UpdateDialogContent();
    }

    // 更新对话框内容的方法
    private void UpdateDialogContent()
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            // 保持其他内容不变，只更新笑话部分
            var currentContent = td.Content?.ToString() ?? "";

            // 提取非笑话部分
            var lines = currentContent.Split('\n');
            if (lines.Length > 4) // 确保有足够多的行
            {
                // 重建内容，保留前4行和最后的分隔线，替换中间的笑话
                var newContent = string.Join("\n", lines.Take(4)) +
                                 $"\n------------------\n{currentJoke}\n------------------";
                td.Content = newContent;
            }
        });
    }

    private void DownloaderOnDownloadProgressChanged(object? sender, DownloadProgressChangedEventArgs e)
    {
        try
        {
            if (downloader.IsCancelled || isCancelled)
            {
                File.Delete(OperatingSystem.IsWindows()
                    ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", "mefrpc.exe.tmp")
                    : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", "mefrpc.tar.tmp"));
                throw new OperationCanceledException();
            }

            // 移除原来的笑话更新逻辑，现在由计时器处理

            Dispatcher.UIThread.Invoke(() =>
                td.Content = string.Format(content, ProcessFileSize(e.BytesPerSecondSpeed)));

            try
            {
                App.MainWindow.PlatformFeatures.SetTaskBarProgressBarState(TaskBarProgressBarState.Normal);
                App.MainWindow.PlatformFeatures.SetTaskBarProgressBarValue((ulong)e.ReceivedBytesSize,
                    (ulong)e.TotalBytesToReceive);
            }
            catch
            {
            }

            td.SetProgressBarState(e.ProgressPercentage, TaskDialogProgressState.Normal);
        }
        catch (Exception ex)
        {
            App.CurrentLogger?.Error(ex);
            td.SetProgressBarState(50, TaskDialogProgressState.Error | TaskDialogProgressState.Suspended);
        }
    }

    /// <summary>
    /// 根据<paramref name="fileSize"/>的大小自动返回对应的文件大小值。
    /// <br/>
    /// 如：若<paramref name="fileSize"/>32743879328,则返回30.50GB；
    /// 返回值的数值范围为1~1000。
    /// </summary>
    /// <param name="fileSize">文件大小，单位为Bytes</param>
    /// <returns>处理后的文件大小值。</returns>
    private string ProcessFileSize(double fileSize)
    {
        string[] sizeUnits = ["B/s", "KB/s", "MB/s", "GB/s", "TB/s"];
        var size = fileSize;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < sizeUnits.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{Math.Round(size, 2)}{sizeUnits[unitIndex]}";
    }

    private void DownloaderOnDownloadFileCompleted(object? sender, AsyncCompletedEventArgs e)
    {
        if (e.Error != null)
        {
            App.CurrentLogger.Error(e.Error, module: EnumLogModule.Net);
            try
            {
                App.MainWindow.PlatformFeatures.SetTaskBarProgressBarState(TaskBarProgressBarState.Error);
            }
            catch
            {
            }

            App.CurrentLogger.Log("下载失败");
        }
        else
        {
            App.CurrentLogger.Log("下载完成");
            try
            {
                App.MainWindow.PlatformFeatures.SetTaskBarProgressBarState(TaskBarProgressBarState.None);
            }
            catch
            {
            }
        }
    }

    private string ProcessUri(string BASE, bool isWindows)
    {
        return isWindows ? BASE + "mefrpc-windows.exe" : BASE + "mefrpc-linux.tar";
    }

    private string ProcessSecurityUri(string BASE, bool isWindows)
    {
        return isWindows ? BASE + "mefrpc-windows.json" : BASE + "mefrpc-linux.json";
    }

    private void DownloaderOnDownloadStarted(object? sender, DownloadStartedEventArgs e)
    {
        App.CurrentLogger.Log($"开始下载: {e.FileName}\n文件大小: {e.TotalBytesToReceive}");
        try
        {
            App.MainWindow.PlatformFeatures.SetTaskBarProgressBarState(TaskBarProgressBarState.Indeterminate);
        }
        catch
        {
        }
    }

    public async Task<bool> DownloadMEFrpClient(OperatingSystem platform, CancellationToken cancellationToken = default)
    {
        td.XamlRoot = VisualRoot;

        try
        {
            jokeTimer.Start();
            if (OperatingSystem.IsMacOS())
            {
                platform = new OperatingSystem(PlatformID.MacOSX, new Version(13, 0, 0, 0));
            }

            switch (platform.Platform)
            {
                case PlatformID.Win32NT:
                {
                    // 检查是否已取消
                    cancellationToken.ThrowIfCancellationRequested();

                    td.ShowAsync();
                    Dispatcher.UIThread.Post(() =>
                    {
                        td.Content = string.Format(content, "--", "北京").Replace("JOKE_PLACEHOLDER",
                            _jokes[Random.Shared.Next(0, _jokes.Length - 1)]);
                    });

                    content = string.Format(content, "{0}", "北京");
                    if (downloader.IsCancelled || isCancelled)
                    {
                        throw new OperationCanceledException();
                    }

                    // 检查是否已取消
                    cancellationToken.ThrowIfCancellationRequested();
                    using var killProcess = new Process();
                    killProcess.StartInfo.FileName = "taskkill";
                    killProcess.StartInfo.Arguments = "/im mefrpc.exe /T /F";
                    killProcess.StartInfo.UseShellExecute = false;
                    killProcess.StartInfo.CreateNoWindow = true;
                    killProcess.Start();
                    await downloader.DownloadFileTaskAsync(
                        GetDownloadUrl(platform.Platform, RuntimeInformation.OSArchitecture == Architecture.Arm64),
                        ConfigManager.CurrentConfig.DownloadSource.ToUpper() != "TPCA"
                            ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", "mefrpc.zip.tmp")
                            : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", "mefrpc.exe.tmp"),
                        cancellationToken);

                    // 检查是否已取消
                    if (downloader.IsCancelled || isCancelled)
                    {
                        throw new OperationCanceledException();
                    }

                    if (Path.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", "mefrpc.zip.tmp")))
                    {
                        ZipFile.ExtractToDirectory(
                            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", "mefrpc.zip.tmp"),
                            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin"), true);
                        File.Delete(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", "mefrpc.zip.tmp"));
                        File.Move(
                            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", "mefrpc_windows_amd64_0.61.1",
                                "mefrpc.exe"),
                            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", "mefrpc.exe.tmp"), true);
                        Directory.Delete(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin",
                            "mefrpc_windows_amd64_0.61.1"));
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    /*
                    td.Content = "验证文件...";
                    td.SetProgressBarState(100, TaskDialogProgressState.Indeterminate);
                    using var httpClient = new HttpClient();
                    httpClient.BaseAddress = new Uri(ProcessSecurityUri(downloadUrls[res], true));
                    httpClient.Timeout = TimeSpan.FromSeconds(2);
                    var response = await httpClient.GetAsync(ProcessSecurityUri(downloadUrls[res], true),
                        cancellationToken);
                    if (!response.IsSuccessStatusCode)
                    {
                        Growl.Error("获取安全信息失败, 取消下载");
                        Dispatcher.UIThread.Post(() =>
                        {
                            td.Hide(TaskDialogStandardResult.Cancel);
                        });
                        return false;
                    }

                    var securityInfo = JsonConvert.DeserializeAnonymousType(await response.Content.ReadAsStringAsync(),
                        new
                        {
                            md5 = string.Empty,
                            sha1 = string.Empty,
                            sha256 = string.Empty,
                        });
                    if (!ValidateFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", "mefrpc.exe.tmp"),
                            securityInfo?.md5, securityInfo?.sha256, securityInfo?.sha1))
                    {
                        return true;
                    }
*/
                    // All done, auto close the dialog here
                    new FileInfo(Path.Combine(AppContext.BaseDirectory, "bin",
                        OperatingSystem.IsWindows() ? "mefrpc.exe.tmp" : "mefrpc.tar.tmp")).MoveTo(
                        Path.Combine(AppContext.BaseDirectory, "bin",
                            OperatingSystem.IsWindows() ? "mefrpc.exe" : "mefrpc.tar"), true);
                    File.Delete(Path.Combine(AppContext.BaseDirectory, "bin",
                        OperatingSystem.IsWindows() ? "mefrpc.exe.tmp" : "mefrpc.tar.tmp"));
                    Dispatcher.UIThread.Post(() =>
                    {
                        td.Hide(TaskDialogStandardResult.OK);
                    });

                    return true;
                }
                case PlatformID.Unix:
                {
                    // Linux 版本的类似修改...
                    cancellationToken.ThrowIfCancellationRequested();
                    td.ShowAsync();

                    cancellationToken.ThrowIfCancellationRequested();

                    Dispatcher.UIThread.Post(() =>
                    {
                        td.Content = string.Format(content, "--", "北京").Replace("JOKE_PLACEHOLDER",
                            _jokes[Random.Shared.Next(0, _jokes.Length - 1)]);
                    });
                    Process.Start("killall", "mefrpc");

                    content = string.Format(content, "{0}", "北京");

                    cancellationToken.ThrowIfCancellationRequested();

                    await downloader.DownloadFileTaskAsync(
                        GetDownloadUrl(platform.Platform, RuntimeInformation.OSArchitecture == Architecture.Arm64),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", "mefrpc.tar.tmp"),
                        cancellationToken);

                    cancellationToken.ThrowIfCancellationRequested();
                    /*
                    td.Content = "验证文件...";
                    td.SetProgressBarState(100, TaskDialogProgressState.Indeterminate);
                    using var httpClient = new HttpClient();
                    httpClient.BaseAddress = new Uri(ProcessSecurityUri(downloadUrls[res], false));
                    httpClient.Timeout = TimeSpan.FromSeconds(2);
                    var response = await httpClient.GetAsync(ProcessSecurityUri(downloadUrls[res], false));
                    if (!response.IsSuccessStatusCode)
                    {
                        Growl.Error("获取安全信息失败, 取消下载");
                        Dispatcher.UIThread.Post(() =>
                        {
                            td.Hide(TaskDialogStandardResult.Cancel);
                        });
                    }

                    var securityInfo = JsonConvert.DeserializeAnonymousType(await response.Content.ReadAsStringAsync(),
                        new
                        {
                            md5 = string.Empty,
                            sha1 = string.Empty,
                            sha256 = string.Empty,
                        });
                    if (ValidateFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", "mefrpc.tar.tmp"),
                            securityInfo?.md5, securityInfo?.sha256, securityInfo?.sha1))
                        // All done, auto close the dialog here
                    {
                    */
                    new FileInfo(Path.Combine(AppContext.BaseDirectory, "bin",
                        OperatingSystem.IsWindows() ? "mefrpc.exe.tmp" : "mefrpc.tar.tmp")).MoveTo(
                        Path.Combine(AppContext.BaseDirectory, "bin",
                            OperatingSystem.IsWindows() ? "mefrpc.exe" : "mefrpc.tar"), true);
                    File.Delete(Path.Combine(AppContext.BaseDirectory, "bin",
                        OperatingSystem.IsWindows() ? "mefrpc.exe.tmp" : "mefrpc.tar.tmp"));
                    Dispatcher.UIThread.Post(() =>
                    {
                        td.Hide(TaskDialogStandardResult.OK);
                    });
                    //}

                    return true;
                }
                case PlatformID.MacOSX:
                {
                    // MacOS 版本的类似修改...
                    cancellationToken.ThrowIfCancellationRequested();

                    td.ShowAsync();

                    cancellationToken.ThrowIfCancellationRequested();

                    Dispatcher.UIThread.Post(() =>
                    {
                        td.Content = string.Format(content, "--", "北京").Replace("JOKE_PLACEHOLDER",
                            _jokes[Random.Shared.Next(0, _jokes.Length - 1)]);
                    });
                    try
                    {
                        Process.Start("killall", "mefrpc");
                    }
                    catch (Exception e)
                    {
                        App.CurrentLogger?.Error(e);
                        Console.WriteLine($"\e[33m结束进程失败, 错误原因: {e.Message}\e[0m");
                    }

                    content = string.Format(content, "{0}", "北京");

                    cancellationToken.ThrowIfCancellationRequested();

                    await downloader.DownloadFileTaskAsync(
                        GetDownloadUrl(PlatformID.MacOSX, RuntimeInformation.OSArchitecture == Architecture.Arm64),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", "mefrpc.tar.tmp"),
                        cancellationToken);

                    cancellationToken.ThrowIfCancellationRequested();
                    /*
                    td.Content = "验证文件...";
                    td.SetProgressBarState(100, TaskDialogProgressState.Indeterminate);
                    using var httpClient = new HttpClient();
                    httpClient.BaseAddress = new Uri(ProcessSecurityUri(downloadUrls[res], false));
                    httpClient.Timeout = TimeSpan.FromSeconds(2);
                    var response = await httpClient.GetAsync(ProcessSecurityUri(downloadUrls[res], false));
                    if (!response.IsSuccessStatusCode)
                    {
                        Growl.Error("获取安全信息失败, 取消下载");
                        Dispatcher.UIThread.Post(() =>
                        {
                            td.Hide(TaskDialogStandardResult.Cancel);
                        });
                    }

                    var securityInfo = JsonConvert.DeserializeAnonymousType(await response.Content.ReadAsStringAsync(),
                        new
                        {
                            md5 = string.Empty,
                            sha1 = string.Empty,
                            sha256 = string.Empty,
                        });
                    if (ValidateFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", "mefrpc.tar.tmp"),
                            securityInfo?.md5, securityInfo?.sha256, securityInfo?.sha1))
                        // All done, auto close the dialog here
                    {
                    */
                    new FileInfo(Path.Combine(AppContext.BaseDirectory, "bin",
                        OperatingSystem.IsWindows() ? "mefrpc.exe.tmp" : "mefrpc.tar.tmp")).MoveTo(
                        Path.Combine(AppContext.BaseDirectory, "bin",
                            OperatingSystem.IsWindows() ? "mefrpc.exe" : "mefrpc.tar"), true);
                    File.Delete(Path.Combine(AppContext.BaseDirectory, "bin",
                        OperatingSystem.IsWindows() ? "mefrpc.exe.tmp" : "mefrpc.tar.tmp"));
                    Dispatcher.UIThread.Post(() =>
                    {
                        td.Hide(TaskDialogStandardResult.OK);
                    });
                    //}

                    return true;
                }
                case PlatformID.Win32S:
                case PlatformID.Win32Windows:
                case PlatformID.WinCE:
                case PlatformID.Xbox:
                case PlatformID.Other:
                default:
                    return false;
            }
        }
        catch (OperationCanceledException)
        {
            // 用户取消了操作
            Dispatcher.UIThread.Post(() =>
            {
                td.Hide(TaskDialogStandardResult.Cancel);
            });
            jokeTimer.Stop();
            return false;
        }
        catch (Exception ex)
        {
            App.CurrentLogger.Error(ex);
            Dispatcher.UIThread.Post(() =>
            {
                td.Hide(TaskDialogStandardResult.Cancel);
            });
            jokeTimer.Stop();
            return false;
        }
        finally
        {
            jokeTimer.Stop();
            jokeTimer.Dispose();
        }
    }

    public static bool ValidateFile(string filePath, string? md5, string? sha256, string? sha1)
    {
        var currentMd5 = GetMd5HashFromFile(filePath);
        var currentSha256 = GetSha256(filePath);
        var currentSha1 = GetSha1(filePath);
        return currentMd5 == md5 && currentSha256 == sha256 && currentSha1 == sha1;
    }

    public static bool ValidateFileSimple(string filePath, string md5)
    {
        var currentMd5 = GetMd5HashFromFile(filePath);
        var avaliableMd5s = md5.Split('|');
        return avaliableMd5s.Contains(currentMd5);
    }

    private static string GetMd5HashFromFile(string fileName)
    {
        try
        {
            // 使用 FileStream 的构造函数，并指定 FileShare.ReadWrite
            // 这允许我们读取文件，即使其他进程也在使用它
            using (var file = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (var md5 = MD5.Create())
                {
                    var retVal = md5.ComputeHash(file);
                    // 文件流会在 using 块结束时自动关闭，无需手动调用 file.Close()

                    var sb = new StringBuilder();
                    for (var i = 0; i < retVal.Length; i++)
                    {
                        sb.Append(retVal[i].ToString("x2"));
                    }

                    return sb.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            // 使用更现代的异常处理方式，保留原始异常作为内部异常
            App.CurrentLogger?.Error(new Exception("GetMD5HashFromFile() failed.", ex));
        }

        return string.Empty; // 发生异常时返回空字符串
    }

    private static string GetSha256(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }

    private static string GetSha1(string filePath)
    {
        using var sha1 = SHA1.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha1.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }
}