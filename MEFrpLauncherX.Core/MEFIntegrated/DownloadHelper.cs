using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Timers;
using Avalonia;
using Avalonia.Threading;
using Downloader;
using Downloader.Exceptions;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Windowing;
using MEFrpLauncherX.Core.Controls;
using DownloadProgressChangedEventArgs = Downloader.DownloadProgressChangedEventArgs;
using RelayCommand = MEFrpLauncherX.Core.Controls.RelayCommand;
using Timer = System.Timers.Timer;

#pragma warning disable CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法

// ReSharper disable InconsistentNaming

namespace MEFrpLauncherX.Core.MEFIntegrated;

public partial class DownloadHelper
{
    /// <summary>
    ///     MEFrp 客户端下载的最大尝试次数（首次 + 2 次重试）。
    ///     达到该次数后不再重试，只给出最终失败提示并结束流程。
    /// </summary>
    private const int MaxDownloadAttempts = 3;

    /// <summary>
    ///     「文件大小异常」的判定下限。mefrpc 客户端实际体积为数十 MB，
    ///     小于该值的产物几乎必然是错误页或空文件。
    /// </summary>
    private const long MinimumArtifactBytes = 16 * 1024;

    private readonly Timer jokeTimer; // 添加计时器

    private volatile FATaskDialog td;
    private readonly Visual? VisualRoot;

    private string content;

    private string currentJoke; // 当前显示的笑话

    internal DownloadService downloader;

    /// <summary>最近一次由 Downloader 报告的异常（DownloadFileTaskAsync 默认不向外抛出）。</summary>
    private Exception? lastDownloadError;

    private bool isCancelled;

    public DownloadHelper(Visual? VisualRoot)
    {
        this.VisualRoot = VisualRoot;
        jokeTimer = new Timer(1000);
        jokeTimer.Elapsed += OnJokeTimerElapsed;
        jokeTimer.AutoReset = true;
        downloader = CreateDownloadService();
        content = CreateInitialContent();
        td = CreateTaskDialog();
        currentJoke = GetRandomJoke();
    }

    /// <summary>
    ///     构造下载服务。<b>每次下载尝试都重新创建一个实例</b>，
    ///     避免上一次尝试遗留的取消状态（<see cref="DownloadService.IsCancelled" />）污染重试。
    /// </summary>
    private DownloadService CreateDownloadService()
    {
        var DownloadOpt = new DownloadConfiguration
        {
            BufferBlockSize = 1024 * 32, // 通常，主机最大支持8000字节，默认值为8000。
            ChunkCount = 8, // 要下载的文件分片数量，默认值为1
            MaxTryAgainOnFailure = 5, // 失败的最大次数
            ParallelDownload = ConfigManager.CurrentConfig.ParallelDownload, // 下载文件是否为并行的。默认值为false
            ParallelCount = ConfigManager.CurrentConfig.ParallelCount,
            HttpClientTimeout = 5000, // 每个 stream reader  的超时（毫秒），默认值是1000

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
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36 Edg/151.0.0.0"
            }
        };

        var service = new DownloadService(DownloadOpt);
        service.DownloadStarted += DownloaderOnDownloadStarted;
        service.DownloadProgressChanged += DownloaderOnDownloadProgressChanged;
        service.DownloadFileCompleted += DownloaderOnDownloadFileCompleted;
        return service;
    }

    /// <summary>
    ///     进度对话框的初始内容模板（文件名 + 线路/速度占位符 + 笑话占位符）。
    ///     <para>每次下载尝试都重新生成，避免重复 <see cref="string.Format" /> 把占位符提前吞掉。</para>
    /// </summary>
    private static string CreateInitialContent() =>
        string.Format(Languages.Languages.Text_Download_ContentHeaderFormat, Environment.OSVersion.Platform switch {
            PlatformID.Win32NT => "mefrpc-windows.exe",
            PlatformID.Unix => OperatingSystem.IsMacOS() ? "mefrpc-darwin.tar" : "mefrpc-unix.tar",
            PlatformID.MacOSX => "mefrpc-darwin.tar",
            _ => Languages.Languages.Text_Download_UnknownPlatform
        }) +
        Languages.Languages.Text_Download_ContentFooter;

    /// <summary>创建进度对话框。结构、图标与取消按钮语义与既有实现保持一致。</summary>
    private FATaskDialog CreateTaskDialog()
    {
        var dialog = new FATaskDialog
        {
            Title = Languages.Languages.Text_Download_Title,
            ShowProgressBar = true,
            IconSource = new FASymbolIconSource { Symbol = FASymbol.Download },
            SubHeader = Languages.Languages.Text_Download_SubHeader,
            Content = content,
            XamlRoot = VisualRoot,
            Buttons =
            {
                new FATaskDialogButton
                {
                    DialogResult = FATaskDialogStandardResult.Cancel,
                    Text = Languages.Languages.Text_Global_Cancel,
                    Command = new RelayCommand(async _ =>
                    {
                        isCancelled = true;
                        downloader.CancelAsync();
                        await downloader.CancelTaskAsync();
                    })
                }
            }
        };
        dialog.SetProgressBarState(0, FATaskDialogProgressState.Indeterminate);
        return dialog;
    }

    // 获取随机笑话的方法
    private string GetRandomJoke() => _jokes[Random.Shared.Next(0, _jokes.Length)];

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
                    ? Path.Combine(AppPaths.RunDirectory, "mefrpc.exe.tmp")
                    : Path.Combine(AppPaths.RunDirectory, "mefrpc.tar.tmp"));
                throw new OperationCanceledException();
            }

            // 移除原来的笑话更新逻辑，现在由计时器处理

            Dispatcher.UIThread.Invoke(() =>
                td.Content = string.Format(content, ProcessFileSize(e.BytesPerSecondSpeed)));

            try
            {
                App.MainWindow?.PlatformFeatures.SetTaskBarProgressBarState(FATaskBarProgressBarState.Normal);
                App.MainWindow?.PlatformFeatures.SetTaskBarProgressBarValue((ulong)e.ReceivedBytesSize,
                    (ulong)e.TotalBytesToReceive);
            }
            catch
            {
            }

            td.SetProgressBarState(e.ProgressPercentage, FATaskDialogProgressState.Normal);
        }
        catch (Exception ex)
        {
            App.CurrentLogger?.Error(ex);
            td.SetProgressBarState(50, FATaskDialogProgressState.Error | FATaskDialogProgressState.Suspended);
        }
    }

    /// <summary>
    ///     根据<paramref name="fileSize" />的大小自动返回对应的文件大小值。
    ///     <br />
    ///     如：若<paramref name="fileSize" />32743879328,则返回30.50GB；
    ///     返回值的数值范围为1~1000。
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
        // Downloader 不会把异常抛给调用方，这里必须自行留存，否则失败会被静默
        lastDownloadError = e.Error;

        if (e.Error != null)
        {
            App.CurrentLogger?.Error(e.Error, module: EnumLogModule.Net);
            try
            {
                App.MainWindow?.PlatformFeatures.SetTaskBarProgressBarState(FATaskBarProgressBarState.Error);
            }
            catch
            {
            }

            App.CurrentLogger?.Log("下载失败");
        }
        else
        {
            App.CurrentLogger?.Log("下载完成");
            try
            {
                App.MainWindow?.PlatformFeatures.SetTaskBarProgressBarState(FATaskBarProgressBarState.None);
            }
            catch
            {
            }
        }
    }

    private void DownloaderOnDownloadStarted(object? sender, DownloadStartedEventArgs e)
    {
        App.CurrentLogger?.Log($"开始下载: {e.FileName}\n文件大小: {e.TotalBytesToReceive}");
        try
        {
            App.MainWindow?.PlatformFeatures.SetTaskBarProgressBarState(FATaskBarProgressBarState.Indeterminate);
        }
        catch
        {
        }
    }

    /// <summary>
    ///     下载并安装 ME Frp 客户端。
    ///     <para>
    ///         失败（网络/HTTP 状态码/文件不完整/校验失败/解压失败/写入失败）时不会静默返回，
    ///         而是展示可读的失败提示，由用户决定「重试」（重新执行同一平台的完整流程）或「取消」。
    ///     </para>
    /// </summary>
    /// <param name="platform">目标平台。</param>
    /// <param name="cancellationToken">取消令牌，重试期间语义不变，用户仍可再次取消。</param>
    /// <param name="expectedMd5">可选的期望 MD5（支持 <c>|</c> 分隔的多个值）。为空时不做哈希校验，与既有行为一致。</param>
    /// <returns>成功安装返回 <see langword="true" />；用户取消或最终失败返回 <see langword="false" />。</returns>
    public async Task<bool> DownloadMEFrpClient(OperatingSystem platform, CancellationToken cancellationToken = default,
        string? expectedMd5 = null)
    {
        try
        {
            jokeTimer.Start();
            if (OperatingSystem.IsMacOS())
            {
                platform = new OperatingSystem(PlatformID.MacOSX, new Version(13, 0, 0, 0));
            }

            App.CurrentLogger?.Log("开始下载MEFrpClient");
            App.CurrentLogger?.Log($"当前平台: {platform.Platform}");
            App.CurrentLogger?.Log(
                $"当前操作系统: {(OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsMacOS() ? "MacOS" : "Linux")}");

            switch (platform.Platform)
            {
                case PlatformID.Win32NT:
                case PlatformID.Unix:
                case PlatformID.MacOSX:
                    // 三个平台的失败提示与重试行为完全一致，差异只在 DownloadOnceAsync 内部
                    return await DownloadWithRetryAsync(platform.Platform, expectedMd5, cancellationToken);
                default:
                    return false;
            }
        }
        catch (OperationCanceledException)
        {
            // 用户取消了操作
            CloseProgressDialog(FATaskDialogStandardResult.Cancel);
            return false;
        }
        catch (Exception ex)
        {
            // 兜底：不允许异常冒泡成未处理异常，也不允许静默失败
            App.CurrentLogger?.Error(ex, "下载 ME Frp 客户端时发生未预料的异常", module: EnumLogModule.Net);
            CloseProgressDialog(FATaskDialogStandardResult.Cancel);
            await ShowFailureAsync(platform.Platform, MefrpcDownloadFailureKind.Unknown, null, [],
                exhausted: true);
            return false;
        }
        finally
        {
            ResetDownloadState();
        }
    }

    /// <summary>
    ///     带用户确认的重试循环：每次尝试都是一次完整的「下载 → 校验 → 解压 → 安装」。
    ///     失败后暂停并询问用户是否重试，最多 <see cref="MaxDownloadAttempts" /> 次，不做无限自动重试。
    /// </summary>
    private async Task<bool> DownloadWithRetryAsync(PlatformID platformId, string? expectedMd5,
        CancellationToken cancellationToken)
    {
        var attemptedSources = new List<string>();
        var isArm = RuntimeInformation.OSArchitecture == Architecture.Arm64;

        for (var attempt = 1; attempt <= MaxDownloadAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (attempt > 1)
            {
                // 重试前清掉上一次尝试遗留的状态，并重建下载服务，避免沿用已取消的会话
                isCancelled = false;
                lastDownloadError = null;
                downloader = CreateDownloadService();
            }

            // 首次使用主下载地址；重试时若有备用地址则切换（官方源没有备用地址，沿用主地址）
            var url = attempt == 1
                ? GetDownloadUrl(platformId, isArm)
                : GetFallbackDownloadUrl(platformId, isArm) ?? GetDownloadUrl(platformId, isArm);
            RememberAttemptedSource(attemptedSources, url);

            ShowProgressDialog(attempt);

            MefrpcDownloadFailureKind failureKind;
            string? httpStatus = null;
            Exception failureException;

            try
            {
                await DownloadOnceAsync(platformId, url, expectedMd5, cancellationToken);
                return true;
            }
            catch (OperationCanceledException)
            {
                // 用户取消：不算下载失败，不弹失败提示
                throw;
            }
            catch (MefrpcDownloadException ex)
            {
                failureKind = ex.Kind;
                httpStatus = ex.HttpStatus;
                failureException = ex.InnerException ?? ex;
            }
            catch (Exception ex)
            {
                failureKind = MefrpcDownloadFailureKind.Unknown;
                failureException = ex;
            }

            App.CurrentLogger?.Log(
                $"[下载失败] 平台={platformId} 文件={GetArtifactFileName(platformId)} 尝试={attempt}/{MaxDownloadAttempts} " +
                $"类型={failureKind} 异常={failureException.GetType().Name}: {failureException.Message}",
                EnumLogType.Error, module: EnumLogModule.Net);

            // 失败收尾：先停掉笑话计时器并关闭进度框，保证同一时刻只有一个对话框
            CloseProgressDialog(FATaskDialogStandardResult.Cancel);

            // 达到上限时只给最终提示；否则由用户决定是否重试
            var retry = await ShowFailureAsync(platformId, failureKind, httpStatus, attemptedSources,
                attempt >= MaxDownloadAttempts);
            if (!retry)
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    ///     执行一次「下载 → 校验 → 解压 → 安装」。
    ///     失败抛 <see cref="MefrpcDownloadException" />，用户取消抛 <see cref="OperationCanceledException" />。
    /// </summary>
    private async Task DownloadOnceAsync(PlatformID platformId, string url, string? expectedMd5,
        CancellationToken cancellationToken)
    {
        try
        {
            switch (platformId)
            {
                case PlatformID.Win32NT:
                {
                    // 检查是否已取消
                    cancellationToken.ThrowIfCancellationRequested();

                    if (downloader.IsCancelled || isCancelled)
                    {
                        throw new OperationCanceledException();
                    }

                    // 检查是否已取消
                    cancellationToken.ThrowIfCancellationRequested();
                    await StopMefrpcProcessAsync(cancellationToken);
                    App.CurrentLogger?.Log("正在删除旧版MEFrpClient");
                    try
                    {
                        File.Delete(AppPaths.MefrpcFile);
                    }
                    catch
                    {
                        // 忽略
                    }

                    App.CurrentLogger?.Log("已删除旧版MEFrpClient");
                    App.CurrentLogger?.Log("正在下载新版MEFrpClient");

                    // 官方源提供 zip 包，TPCA 源直接提供 exe —— 落盘命名与既有实现完全一致
                    var downloadTarget = IsTpcaSource()
                        ? Path.Combine(AppPaths.RunDirectory, "mefrpc.exe.tmp")
                        : Path.Combine(AppPaths.RunDirectory, "mefrpc.zip.tmp");

                    await RunDownloadAsync(url, downloadTarget, cancellationToken);
                    App.CurrentLogger?.Log("已下载新版MEFrpClient");
                    EnsureDownloadedArtifact(downloadTarget);
                    App.CurrentLogger?.Log("正在解压新版MEFrpClient");
                    // 检查是否已取消
                    if (downloader.IsCancelled || isCancelled)
                    {
                        throw new OperationCanceledException();
                    }

                    if (Path.Exists(Path.Combine(AppPaths.RunDirectory, "mefrpc.zip.tmp")))
                    {
                        try
                        {
                            await ZipFile.ExtractToDirectoryAsync(
                                Path.Combine(AppPaths.RunDirectory, "mefrpc.zip.tmp"),
                                AppPaths.RunDirectory, true, cancellationToken);
                            File.Delete(Path.Combine(AppPaths.RunDirectory, "mefrpc.zip.tmp"));
                            File.Move(
                                Path.Combine(AppPaths.RunDirectory,
                                    $"mefrpc_windows_amd64_{App.MEFrpVersion}",
                                    "mefrpc.exe"),
                                Path.Combine(AppPaths.RunDirectory, "mefrpc.exe.tmp"), true);
                            Directory.Delete(Path.Combine(AppPaths.RunDirectory,
                                $"mefrpc_windows_amd64_{App.MEFrpVersion}"), true);
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            // zip 损坏、目录不可写或没有权限
                            throw new MefrpcDownloadException(MefrpcDownloadFailureKind.Extract, inner: ex);
                        }
                    }

                    App.CurrentLogger?.Log("已解压新版MEFrpClient");
                    cancellationToken.ThrowIfCancellationRequested();

                    // All done, auto close the dialog here
                    td.Content = Languages.Languages.Text_Download_InstallingClient;
                    // 26.5.0 修复：原先误写成 MoveTo(x, x) 自我移动后立刻 Delete，等于把刚下好的客户端删掉。
                    ValidateStagedClient(expectedMd5);
                    InstallStagedClient();
                    App.CurrentLogger?.Log("已安装新版MEFrpClient");
                    Dispatcher.UIThread.Post(() =>
                    {
                        td.Hide(FATaskDialogStandardResult.OK);
                    });

                    return;
                }
                case PlatformID.Unix:
                {
                    // Linux 版本的类似修改...
                    cancellationToken.ThrowIfCancellationRequested();
                    StopMefrpcProcessBestEffort();

                    cancellationToken.ThrowIfCancellationRequested();

                    var downloadTarget = Path.Combine(AppPaths.RunDirectory, "mefrpc.tar.tmp");
                    await RunDownloadAsync(url, downloadTarget, cancellationToken);
                    EnsureDownloadedArtifact(downloadTarget);

                    cancellationToken.ThrowIfCancellationRequested();
                    ValidateStagedClient(expectedMd5);
                    InstallStagedClient();
                    Dispatcher.UIThread.Post(() =>
                    {
                        td.Hide(FATaskDialogStandardResult.OK);
                    });

                    return;
                }
                case PlatformID.MacOSX:
                {
                    // MacOS 版本的类似修改...
                    cancellationToken.ThrowIfCancellationRequested();
                    StopMefrpcProcessBestEffort();

                    cancellationToken.ThrowIfCancellationRequested();

                    var downloadTarget = Path.Combine(AppPaths.RunDirectory, "mefrpc.tar.tmp");
                    await RunDownloadAsync(url, downloadTarget, cancellationToken);
                    EnsureDownloadedArtifact(downloadTarget);

                    cancellationToken.ThrowIfCancellationRequested();
                    ValidateStagedClient(expectedMd5);
                    InstallStagedClient();
                    Dispatcher.UIThread.Post(() =>
                    {
                        td.Hide(FATaskDialogStandardResult.OK);
                    });

                    return;
                }
                default:
                    throw new MefrpcDownloadException(MefrpcDownloadFailureKind.Unknown);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (MefrpcDownloadException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // 未预料到的异常统一归类，保证一定以「可读提示」结束，而不是冒泡
            throw CreateFailure(ex);
        }
    }

    /// <summary>
    ///     展示一次下载尝试的进度对话框。
    ///     <para>每次尝试都使用全新的对话框实例，上一次的关闭动画/宿主不会与本次叠加。</para>
    /// </summary>
    private void ShowProgressDialog(int attempt)
    {
        content = CreateInitialContent();

        if (attempt > 1)
        {
            td = CreateTaskDialog();
        }

        td.XamlRoot = VisualRoot;
        td.SubHeader = attempt <= 1
            ? Languages.Languages.Text_Download_SubHeader
            : string.Format(Languages.Languages.Text_Download_RetrySubHeaderFormat, attempt, MaxDownloadAttempts);
        td.SetProgressBarState(0, FATaskDialogProgressState.Indeterminate);

        // 模板与「已格式化内容」分开使用：模板里的 {0} 是下载速度占位符
        var template = content;
        content = string.Format(template, "{0}", "北京");

        var dialog = td;
        dialog.ShowAsync();
        Dispatcher.UIThread.Post(() =>
        {
            dialog.Content = string.Format(template, "--", "北京").Replace("JOKE_PLACEHOLDER", GetRandomJoke());
        });

        jokeTimer.Start();
    }

    /// <summary>停止笑话计时器并关闭进度对话框（不包含任何用户提示）。</summary>
    private void CloseProgressDialog(object result)
    {
        jokeTimer.Stop();

        // 捕获当前实例：避免延迟执行的隐藏操作误伤下一轮新建的对话框
        var dialog = td;
        Dispatcher.UIThread.Post(() => dialog.Hide(result));
    }

    /// <summary>结束本次下载涉及的 UI 状态与标志位，不残留后台任务。</summary>
    private void ResetDownloadState()
    {
        jokeTimer.Stop();
        jokeTimer.Dispose();
        isCancelled = false;
        lastDownloadError = null;

        try
        {
            App.MainWindow?.PlatformFeatures.SetTaskBarProgressBarState(FATaskBarProgressBarState.None);
        }
        catch
        {
            // 平台特性不可用时忽略
        }
    }

    /// <summary>
    ///     展示失败提示（复用项目统一的 MessageBox 样式）。
    ///     <paramref name="exhausted" /> 为 <see langword="true" /> 时展示最终失败提示（只有「确定」按钮）。
    /// </summary>
    /// <returns>用户选择「重试」返回 <see langword="true" />；其余情况返回 <see langword="false" />。</returns>
    private async Task<bool> ShowFailureAsync(PlatformID platformId, MefrpcDownloadFailureKind kind,
        string? httpStatus, IReadOnlyList<string> attemptedSources, bool exhausted)
    {
        var body = BuildFailureContent(platformId, kind, httpStatus, attemptedSources, exhausted);

        try
        {
            if (exhausted)
            {
                await MessageBox.ShowAsync(body, Languages.Languages.Text_Download_FailedTitle, "",
                    MessageBoxIcon.Error,
                    [
                        new FATaskDialogButton(Languages.Languages.Text_Download_FailedCloseButton,
                            FATaskDialogStandardResult.OK)
                    ]);
                return false;
            }

            var result = await MessageBox.ShowAsync(body, Languages.Languages.Text_Download_FailedTitle, "",
                MessageBoxIcon.Error,
                [
                    new FATaskDialogButton(Languages.Languages.Text_Download_RetryButton,
                        FATaskDialogStandardResult.Yes),
                    new FATaskDialogButton(Languages.Languages.Text_Download_CancelButton,
                        FATaskDialogStandardResult.Cancel)
                ]);
            return result == MessageBoxResult.Yes;
        }
        catch (Exception ex)
        {
            // 连提示框都无法展示时也不允许二次抛出
            App.CurrentLogger?.Error(ex, "展示下载失败提示时出错");
            return false;
        }
    }

    /// <summary>拼装失败提示正文：可读描述 + 文件名/平台 + 下一步建议 + 已尝试的下载源。</summary>
    private static string BuildFailureContent(PlatformID platformId, MefrpcDownloadFailureKind kind,
        string? httpStatus, IReadOnlyList<string> attemptedSources, bool exhausted)
    {
        var reason = kind switch
        {
            MefrpcDownloadFailureKind.Network => Languages.Languages.Text_Download_FailedNetwork,
            MefrpcDownloadFailureKind.HttpStatus => string.Format(
                Languages.Languages.Text_Download_FailedHttpStatusFormat,
                string.IsNullOrEmpty(httpStatus) ? "?" : httpStatus),
            MefrpcDownloadFailureKind.Incomplete => Languages.Languages.Text_Download_FailedIncomplete,
            MefrpcDownloadFailureKind.HashMismatch => Languages.Languages.Text_Download_FailedHashMismatch,
            MefrpcDownloadFailureKind.Extract => Languages.Languages.Text_Download_FailedExtract,
            MefrpcDownloadFailureKind.WriteFile => Languages.Languages.Text_Download_FailedWrite,
            _ => Languages.Languages.Text_Download_FailedUnknown
        };

        if (exhausted)
        {
            reason += "\n" +
                      string.Format(Languages.Languages.Text_Download_FailedExhaustedFormat, MaxDownloadAttempts);
        }

        var advice = kind switch
        {
            MefrpcDownloadFailureKind.Network => Languages.Languages.Text_Download_AdviceNetwork,
            MefrpcDownloadFailureKind.HttpStatus => Languages.Languages.Text_Download_AdviceHttpStatus,
            MefrpcDownloadFailureKind.Incomplete => Languages.Languages.Text_Download_AdviceIncomplete,
            MefrpcDownloadFailureKind.HashMismatch => Languages.Languages.Text_Download_AdviceHashMismatch,
            MefrpcDownloadFailureKind.Extract => Languages.Languages.Text_Download_AdviceExtract,
            MefrpcDownloadFailureKind.WriteFile => Languages.Languages.Text_Download_AdviceWrite,
            _ => Languages.Languages.Text_Download_AdviceUnknown
        };

        // 无备用源时不虚构：只列出真实尝试过的下载源主机
        var sources = attemptedSources.Count > 0
            ? string.Format(Languages.Languages.Text_Download_FailedSourcesFormat,
                string.Join(", ", attemptedSources))
            : "";

        return string.Format(Languages.Languages.Text_Download_FailedContentFormat,
            reason, GetArtifactFileName(platformId), GetPlatformDisplayName(platformId), advice, sources);
    }

    /// <summary>与进度对话框头部一致的产物文件名（Windows 侧官方源实际下载的是 zip）。</summary>
    private static string GetArtifactFileName(PlatformID platformId) => platformId switch
    {
        PlatformID.Win32NT => "mefrpc-windows.exe",
        PlatformID.Unix => "mefrpc-unix.tar",
        PlatformID.MacOSX => "mefrpc-darwin.tar",
        _ => Languages.Languages.Text_Download_UnknownPlatform
    };

    /// <summary>面向用户的平台名。</summary>
    private static string GetPlatformDisplayName(PlatformID platformId) => platformId switch
    {
        PlatformID.Win32NT => "Windows",
        PlatformID.MacOSX => "macOS",
        PlatformID.Unix => "Linux",
        _ => Languages.Languages.Text_Download_UnknownPlatform
    };

    /// <summary>记录一次实际发起过的下载源（只保留主机名，便于提示中说明已尝试的源）。</summary>
    private static void RememberAttemptedSource(List<string> sources, string url)
    {
        var host = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : url;
        if (!sources.Contains(host))
        {
            sources.Add(host);
        }
    }

    // ===== 下载失败判定、产物校验与进程结束：以下方法只服务于 DownloadMEFrpClient =====

    /// <summary>MEFrp 客户端的临时落盘路径（下载完成后由 AppPaths.InstallMefrpc 落位）。</summary>
    private static string StagedClientPath => Path.Combine(AppPaths.RunDirectory,
        OperatingSystem.IsWindows() ? "mefrpc.exe.tmp" : "mefrpc.tar.tmp");

    /// <summary>
    ///     执行一次下载，并把 Downloader 「静默吞掉」的失败转换成异常。
    ///     判据：DownloadFileCompleted 报告的异常、取消标志与落盘产物。
    /// </summary>
    private async Task RunDownloadAsync(string url, string targetPath, CancellationToken cancellationToken)
    {
        lastDownloadError = null;

        try
        {
            await downloader.DownloadFileTaskAsync(url, targetPath, cancellationToken);
        }
        catch (Exception ex)
        {
            // 某些版本/场景下仍会抛出，统一并入失败判据
            lastDownloadError ??= ex;
        }

        if (isCancelled || downloader.IsCancelled || cancellationToken.IsCancellationRequested)
        {
            TryDeleteFile(targetPath);
            throw new OperationCanceledException(cancellationToken);
        }

        if (lastDownloadError is not null)
        {
            TryDeleteFile(targetPath);
            throw CreateFailure(lastDownloadError);
        }
    }

    /// <summary>校验下载产物是否真实落盘：存在、非空、体积不像错误页。</summary>
    private static void EnsureDownloadedArtifact(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length < MinimumArtifactBytes)
            {
                throw new MefrpcDownloadException(MefrpcDownloadFailureKind.Incomplete);
            }
        }
        catch (MefrpcDownloadException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // 连文件长度都读不到：通常是写入 data/Run 失败（权限/占用/磁盘空间）
            throw new MefrpcDownloadException(MefrpcDownloadFailureKind.WriteFile, inner: ex);
        }
    }

    /// <summary>尽力删除临时文件，失败只记录日志（不影响失败类型的判定）。</summary>
    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            App.CurrentLogger?.Log($"删除临时文件失败: {path} ({ex.Message})", EnumLogType.Warn);
        }
    }

    /// <summary>
    ///     校验落盘后的客户端：存在性 + （可选）MD5。
    ///     <paramref name="expectedMd5" /> 为空时不做哈希校验，与既有行为完全一致；
    ///     校验实现直接复用 <see cref="ValidateFileSimple" />，不改其比较语义。
    /// </summary>
    private static void ValidateStagedClient(string? expectedMd5)
    {
        var staged = StagedClientPath;

        if (!File.Exists(staged))
        {
            throw new MefrpcDownloadException(MefrpcDownloadFailureKind.Incomplete);
        }

        if (string.IsNullOrWhiteSpace(expectedMd5))
        {
            return;
        }

        bool matches;
        try
        {
            matches = ValidateFileSimple(staged, expectedMd5);
        }
        catch (Exception ex)
        {
            throw new MefrpcDownloadException(MefrpcDownloadFailureKind.HashMismatch, inner: ex);
        }

        if (!matches)
        {
            throw new MefrpcDownloadException(MefrpcDownloadFailureKind.HashMismatch);
        }
    }

    /// <summary>
    ///     把 *.tmp 落位为正式客户端并校验落位结果。
    ///     <see cref="AppPaths.InstallMefrpc" />会吞掉异常，因此以「文件确实就位且长度一致」作为成功判据。
    /// </summary>
    private static void InstallStagedClient()
    {
        var staged = StagedClientPath;

        long stagedLength;
        try
        {
            stagedLength = new FileInfo(staged).Length;
        }
        catch (Exception ex)
        {
            throw new MefrpcDownloadException(MefrpcDownloadFailureKind.WriteFile, inner: ex);
        }

        AppPaths.InstallMefrpc(App.CurrentLogger);

        try
        {
            var installed = new FileInfo(AppPaths.MefrpcFile);
            if (!installed.Exists || installed.Length != stagedLength)
            {
                throw new MefrpcDownloadException(MefrpcDownloadFailureKind.WriteFile);
            }
        }
        catch (MefrpcDownloadException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new MefrpcDownloadException(MefrpcDownloadFailureKind.WriteFile, inner: ex);
        }
    }

    /// <summary>结束此前遗留的 mefrpc 进程（Windows）。失败只记录日志，不构成下载失败。</summary>
    private static async Task StopMefrpcProcessAsync(CancellationToken cancellationToken)
    {
        App.CurrentLogger?.Log("正在关闭MEFrpClient");

        try
        {
            using var killProcess = new Process();
            killProcess.StartInfo.FileName = "taskkill";
            killProcess.StartInfo.Arguments = "/im mefrpc.exe /T /F";
            killProcess.StartInfo.UseShellExecute = false;
            killProcess.StartInfo.CreateNoWindow = true;
            killProcess.Start();
            await killProcess.WaitForExitAsync(cancellationToken);
            App.CurrentLogger?.Log("已关闭MEFrpClient");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // 旧客户端本就没有运行，或系统上没有 taskkill
            App.CurrentLogger?.Log($"关闭旧版 MEFrpClient 失败: {ex.Message}", EnumLogType.Warn);
        }
    }

    /// <summary>尽力结束此前遗留的 mefrpc 进程（Linux / macOS）。失败只记录日志，不构成下载失败。</summary>
    private static void StopMefrpcProcessBestEffort()
    {
        try
        {
            using var killProcess = Process.Start("killall", "mefrpc");
            App.CurrentLogger?.Log("已请求结束已运行的 mefrpc 进程");
        }
        catch (Exception ex)
        {
            App.CurrentLogger?.Log($"结束进程失败: {ex.Message}", EnumLogType.Warn);
        }
    }

    /// <summary>把底层异常归类为可读的失败类型（分类只影响文案，不影响任何校验逻辑）。</summary>
    private static MefrpcDownloadException CreateFailure(Exception ex)
    {
        foreach (var current in EnumerateExceptions(ex))
        {
            switch (current)
            {
                case IncompleteDownloadException:
                    return new MefrpcDownloadException(MefrpcDownloadFailureKind.Incomplete, inner: ex);
                case FileExistException:
                    return new MefrpcDownloadException(MefrpcDownloadFailureKind.WriteFile, inner: ex);
            }
        }

        if (TryGetHttpStatus(ex, out var status))
        {
            return new MefrpcDownloadException(MefrpcDownloadFailureKind.HttpStatus, status, ex);
        }

        if (IsWriteFailure(ex))
        {
            return new MefrpcDownloadException(MefrpcDownloadFailureKind.WriteFile, inner: ex);
        }

        if (IsNetworkFailure(ex))
        {
            return new MefrpcDownloadException(MefrpcDownloadFailureKind.Network, inner: ex);
        }

        if (ex is IOException)
        {
            // 传输中断在 Downloader 中通常以 IO 异常呈现
            return new MefrpcDownloadException(MefrpcDownloadFailureKind.Incomplete, inner: ex);
        }

        return new MefrpcDownloadException(MefrpcDownloadFailureKind.Unknown, inner: ex);
    }

    /// <summary>不可重试的本地写入失败（权限不足、目录不存在等）。</summary>
    private static bool IsWriteFailure(Exception ex) =>
        EnumerateExceptions(ex).Any(x => x is UnauthorizedAccessException or DirectoryNotFoundException);

    /// <summary>网络不可用、DNS 解析失败、连接失败或超时。</summary>
    private static bool IsNetworkFailure(Exception ex) =>
        EnumerateExceptions(ex).Any(x => x is HttpRequestException or WebException or TimeoutException
            or System.Net.Sockets.SocketException
            || (x is IOException io && io.InnerException is System.Net.Sockets.SocketException));

    /// <summary>从异常链中提取 HTTP 状态码（非 2xx）。</summary>
    private static bool TryGetHttpStatus(Exception ex, out string? status)
    {
        status = null;

        foreach (var current in EnumerateExceptions(ex))
        {
            switch (current)
            {
                case HttpRequestException { StatusCode: { } code }:
                    status = $"{(int)code} {code}";
                    return true;
                case WebException { Response: HttpWebResponse response }:
                    status = $"{(int)response.StatusCode} {response.StatusCode}";
                    return true;
            }
        }

        // Downloader 在非 2xx 时可能只抛出带状态码文本的普通异常，这里做一次保守兜底
        foreach (var current in EnumerateExceptions(ex))
        {
            var match = HttpStatusPattern.Match(current.Message);
            if (match.Success)
            {
                status = match.Groups[1].Value;
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<Exception> EnumerateExceptions(Exception? ex)
    {
        var depth = 0;
        while (ex is not null && depth < 10)
        {
            yield return ex;
            ex = ex.InnerException;
            depth++;
        }
    }

    /// <summary>仅用于从错误文本中提取状态码，不参与任何校验判定。</summary>
    private static readonly Regex HttpStatusPattern = new(@"(?<!\d)([45][0-9]{2})(?!\d)", RegexOptions.CultureInvariant);

    /// <summary>MEFrp 客户端下载失败的类型，仅用于选择面向用户的文案。</summary>
    private enum MefrpcDownloadFailureKind
    {
        Unknown,
        Network,
        HttpStatus,
        Incomplete,
        HashMismatch,
        Extract,
        WriteFile
    }

    /// <summary>
    ///     内部使用的下载失败信号：携带失败类型与（可选的）HTTP 状态码。
    ///     该异常不会冒泡到调用方，一律在 <see cref="DownloadMEFrpClient" /> 内转换成用户提示。
    /// </summary>
    private sealed class MefrpcDownloadException : Exception
    {
        public MefrpcDownloadException(MefrpcDownloadFailureKind kind, string? httpStatus = null,
            Exception? inner = null)
            : base($"MEFrp client download failed: {kind}", inner)
        {
            Kind = kind;
            HttpStatus = httpStatus;
        }

        public MefrpcDownloadFailureKind Kind { get; }

        public string? HttpStatus { get; }
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
            App.CurrentLogger?.Error(ex);
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
