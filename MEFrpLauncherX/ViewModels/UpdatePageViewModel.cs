using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reactive;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Animation;
using Avalonia.Collections;
using Downloader;
using FluentAvalonia.UI.Windowing;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.Languages;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using DownloadProgressChangedEventArgs = Downloader.DownloadProgressChangedEventArgs;

// ReSharper disable EmptyGeneralCatchClause

namespace MEFrpLauncherX.ViewModels;

public class UpdatePageViewModel : ViewModelBase
{
    internal DownloadService downloader;

    public UpdatePageViewModel()
    {
        UpdateChannel = ConfigManager.CurrentConfig.UpdateSettings.Channel switch
        {
            "Stable" => 0,
            "Preview" => 1,
            _ => 0
        };
        UpdateMethod = ConfigManager.CurrentConfig.UpdateSettings.Method switch
        {
            "ds" => 0,
            "dd" => 1,
            "md" => 2,
            _ => 0
        };
        TargetCompileType = ConfigManager.CurrentConfig.UpdateSettings.CompileType switch
        {
            "AOT" => 0,
            "Common" => 1,
            _ => Core.App.ReleaseFlag == "AOT" ? 0 : 1
        };

        CheckUpdateCommand = ReactiveCommand.Create(CheckUpdate);
        RetryDownloadCommand = ReactiveCommand.Create(RetryDownload);
        DownloadUpdateCommand = ReactiveCommand.Create(DownloadUpdate);
    }

    public bool HasNewVersion
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = false;

    public string Icon
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = ICONS.UPDATE;

    public string Status
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = Languages.Text_Update_NeverChecked;

    public DateTime LatestCheckTime
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = DateTime.Now;

    public IterationCount IterationCount
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = new(0, IterationType.Many);

    public string LatestVersion
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = Core.App.Version;

    public AvaloniaList<string> Changelog
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    public bool IsIdle
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public double ProgressValue
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsLoading
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public double MaxValue
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string? CurrentSpeed
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string Codename
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = App.Codename;

    /// <summary>
    ///     0 - 稳定通道
    ///     1 - 预览通道
    /// </summary>
    public int UpdateChannel
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>
    ///     0 - 自动下载并安装
    ///     1 - 自动下载
    ///     2 - 手动下载
    /// </summary>
    public int UpdateMethod
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>
    ///     0 - AOT（预编译）
    ///     1 - Common（常规）
    /// </summary>
    public int TargetCompileType
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ReactiveCommand<Unit, Unit> CheckUpdateCommand { get; }

    /// <summary>
    ///     下载失败（网络/校验），显示「重试下载」按钮
    /// </summary>
    public bool HasDownloadFailed
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>
    ///     失败提示（状态文本 ToolTip，含切源指引）
    /// </summary>
    public string? FailureTip
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>
    ///     重试下载
    /// </summary>
    public ReactiveCommand<Unit, Unit> RetryDownloadCommand { get; }

    /// <summary>
    ///     下载并安装更新（绑定入口，禁止直接绑定方法）
    /// </summary>
    public ReactiveCommand<Unit, Unit> DownloadUpdateCommand { get; }

    /// <summary>
    ///     检查更新
    /// </summary>
    /// <returns>(是否最新, 最新版本)</returns>
    public static async Task<(bool, string)> GetNewVersionAsync()
    {
        var updateInfo = await RYCBApiConverter.GetLatestVersionInfoAsync();
        var preiewUpdateInfo = await RYCBApiConverter.GetLatestPreviewVersionInfoAsync();
        var isPreview = ConfigManager.CurrentConfig.UpdateSettings.Channel != "Stable";
        var latestVersion = isPreview ? GetLatestVersion(updateInfo, preiewUpdateInfo) : updateInfo.version;

        return (VersionComparer.IsGreaterThan(latestVersion, Core.App.Version), latestVersion);
    }

    public async void CheckUpdate()
    {
        Icon = ICONS.UPDATE;
        IsLoading = true;
        IsIdle = true;
        LatestCheckTime = DateTime.Now;
        try
        {
            Core.App.MainWindow?.PlatformFeatures.SetTaskBarProgressBarState(TaskBarProgressBarState.Indeterminate);
        }
        catch
        {
            // Platform not support
        }

        IterationCount = new IterationCount(100000, IterationType.Many);
        Core.App.CurrentLogger?.Log("正在检查更新", module: EnumLogModule.Update);
        Status = Languages.Text_Update_Checking;
        var isPreview = ConfigManager.CurrentConfig.UpdateSettings.Channel != "Stable";
        SingleVersionInfo updateInfo = new()
            {
                data = new SingleVersionInfo.VersionInfo
                {
                    changes = [Languages.Text_Update_FetchFailed],
                    codename = App.Codename,
                    date = DateTime.Now.ToString("yyyy-MM-dd"),
                    description = Languages.Text_Update_FetchFailed
                },
                success = false,
                version = Core.App.Version
            },
            preiewUpdateInfo = new()
            {
                data = new SingleVersionInfo.VersionInfo
                {
                    changes = [Languages.Text_Update_FetchFailed],
                    codename = App.Codename,
                    date = DateTime.Now.ToString("yyyy-MM-dd"),
                    description = Languages.Text_Update_FetchFailed
                },
                success = false,
                version = Core.App.Version
            };
        try
        {
            updateInfo = await RYCBApiConverter.GetLatestVersionInfoAsync();
            preiewUpdateInfo = await RYCBApiConverter.GetLatestPreviewVersionInfoAsync();
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger?.Log("获取更新信息失败", EnumLogType.Error, module: EnumLogModule.Update);
            Core.App.CurrentLogger?.Error(ex);
            Status = Languages.Text_Update_FetchFailed;
            FailureTip = Languages.Text_Update_FetchFailedTip;
            Icon = ICONS.ERROR;
            IsIdle = false;
            return;
        }

        string latestVersion;
        latestVersion = isPreview ? GetLatestVersion(updateInfo, preiewUpdateInfo) : updateInfo.version;

// #if !DEBUG
//         Core.App.CurrentLogger.LogDebug("[DEBUG] 模拟更新", module: EnumLogModule.Update);
//         updateInfo.version = "999.999.999.99";
// #endif
        // var versionRegex = new Regex(@"^\d+(?:\.\d+){2,4}");
        // var version = versionRegex.Match(App.Version);
        if (VersionComparer.IsLessThan(updateInfo.version, preiewUpdateInfo.version) ||
            VersionComparer.IsLessThan(latestVersion, preiewUpdateInfo.version) ||
            latestVersion == preiewUpdateInfo.version)
        {
            updateInfo = preiewUpdateInfo;
        }

        if (VersionComparer.IsGreaterThan(latestVersion, Core.App.Version))
        {
            Core.App.CurrentLogger?.Log("检测到新版本: " + updateInfo.version, module: EnumLogModule.Update);
            IterationCount = new IterationCount(0);
            Icon = ICONS.DOWNLOAD;
            if (latestVersion == preiewUpdateInfo.version)
            {
                updateInfo = preiewUpdateInfo;
            }

            Status = Languages.Text_Update_NewVersionDetected + latestVersion;
            LatestVersion = updateInfo.version;
            IsLoading = false;
            IsIdle = false;
            HasNewVersion = true;
            FailureTip = null;
            Codename = updateInfo.data.codename;
            Changelog.Clear();
            Changelog.AddRange(updateInfo.data.changes);
        }
        else
        {
            Core.App.CurrentLogger?.Log("当前版本已经是最新版本", module: EnumLogModule.Update);
            Status = Languages.Text_Update_AlreadyLatest;
            Icon = ICONS.LATEST;
            IsLoading = false;
            IsIdle = true;
            FailureTip = null;
        }

        if (updateInfo is { success: true, data.changes.Length: > 0 })
        {
            LatestVersion = latestVersion;
            Changelog.Clear();
            Changelog.AddRange(updateInfo.data.changes);
        }
        else
        {
            Core.App.CurrentLogger?.Log("获取更新信息失败", EnumLogType.Error, module: EnumLogModule.Update);
            Status = Languages.Text_Update_FetchFailed;
            FailureTip = Languages.Text_Update_FetchFailedTip;
            Icon = ICONS.ERROR;
            IsIdle = false;
            return;
        }

        try
        {
            Core.App.MainWindow?.PlatformFeatures.SetTaskBarProgressBarState(TaskBarProgressBarState.None);
        }
        catch
        {
        }

        Core.App.CurrentLogger?.Log("检查更新完成", module: EnumLogModule.Update);
        IterationCount = new IterationCount(0);
    }

    private static string GetLatestVersion(SingleVersionInfo updateInfo, SingleVersionInfo preiewUpdateInfo)
    {
        var res = VersionComparer.CompareVersions(updateInfo.version, preiewUpdateInfo.version);
        var cd = res switch
        {
            -1 => preiewUpdateInfo.version,
            1 => updateInfo.version,
            _ => preiewUpdateInfo.version
        };
        var res1 = VersionComparer.CompareVersions(Core.App.Version, cd);
        return res1 switch
        {
            -1 => cd,
            1 => Core.App.Version,
            _ => cd
        };
    }

    public async void DownloadUpdate()
    {
        Core.App.CurrentLogger?.Log("正在下载更新", module: EnumLogModule.Update);
        Status = Languages.Text_Update_Downloading;
        Icon = ICONS.DOWNLOAD;
        IsLoading = true;
        IsIdle = false;
        HasDownloadFailed = false;
        FailureTip = null;
        ProgressValue = 0;

        // ===== 1. 按系统拼接下载链接和临时文件名 =====
        string downloadUrl;
        string tempFileName;
        string systemTip; // 下载完成后的安装提示

        if (OperatingSystem.IsWindows())
        {
            downloadUrl = BuildUpdateDownloadUrl(PlatformID.Win32NT, LatestVersion);
            tempFileName = $"update_tmp_{LatestVersion}.exe";
            systemTip = Languages.Text_Update_InstallTipWindows;
        }
        else if (OperatingSystem.IsMacOS())
        {
            downloadUrl = BuildUpdateDownloadUrl(PlatformID.MacOSX, LatestVersion);
            tempFileName = $"update_tmp_{LatestVersion}.dmg";
            systemTip = Languages.Text_Update_InstallTipMacOS;
        }
        else if (OperatingSystem.IsLinux())
        {
            downloadUrl = BuildUpdateDownloadUrl(PlatformID.Unix, LatestVersion);
            tempFileName = $"update_tmp_{LatestVersion}.deb";
            systemTip = Languages.Text_Update_InstallTipLinux;
        }
        else
        {
            await MessageBox.ShowAsync(Languages.Text_Update_AutoUpdateNotSupported, Languages.Text_Update_UnsupportedSystem,
                MessageBoxIcon.Error);
            IsLoading = false;
            IsIdle = true;
            return;
        }

        // ===== 2. 下载配置（复用原有逻辑） =====
        var DownloadOpt = new DownloadConfiguration
        {
            BufferBlockSize = 1024 * 32,
            ChunkCount = 8,
            MaxTryAgainOnFailure = 5,
            ParallelDownload = ConfigManager.CurrentConfig.ParallelDownload,
            ParallelCount = ConfigManager.CurrentConfig.ParallelCount,
            BlockTimeout = 5000,
            RequestConfiguration =
            {
                Accept = "*/*",
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                CookieContainer = new CookieContainer(),
                Headers = [],
                KeepAlive = true,
                ProtocolVersion = HttpVersion.Version11,
                UseDefaultCredentials = false,
                UserAgent =
                    $"RYCB/PML {Core.App.Version} Desktop"
            }
        };

        // ===== 3. 初始化下载器并开始下载 =====
        downloader = new DownloadService(DownloadOpt);
        downloader.DownloadStarted += DownloaderOnDownloadStarted;
        downloader.DownloadProgressChanged += DownloaderOnDownloadProgressChanged;
        downloader.DownloadFileCompleted += DownloaderOnDownloadFileCompleted;

        var savePath = Path.Combine(Core.App.StartupPath, "Cache", tempFileName);

        // ===== 3.5 下载（失败不再静默：明确文案 + 重试入口） =====
        try
        {
            await downloader.DownloadFileTaskAsync(downloadUrl, savePath);
        }
        catch (Exception ex)
        {
            EnterDownloadFailedState(Languages.Text_Update_DownloadFailed, ex);
            return;
        }

        // 校验下载产物：文件必须存在且非空，否则视为下载/校验失败（禁止静默失败）
        if (!File.Exists(savePath) || new FileInfo(savePath).Length <= 0)
        {
            EnterDownloadFailedState(Languages.Text_Update_VerifyFailed, null, isVerify: true);
            return;
        }

        // ===== 4. 下载完成后处理 =====
        Core.App.CurrentLogger?.Log("下载更新完成", module: EnumLogModule.Update);
        IsLoading = false;
        IsIdle = true;
        Icon = ICONS.LATEST;
        Status = Languages.Text_Update_DownloadCompleted;

        try
        {
            Core.App.MainWindow?.PlatformFeatures.SetTaskBarProgressBarState(TaskBarProgressBarState.None);
        }
        catch
        {
        } // Windows 自动安装逻辑保留，Linux/macOS 仅打开目录+提示

        if (UpdateMethod != 0 || !OperatingSystem.IsWindows())
        {
            if (await MessageBox.ShowAsync($"{systemTip}", Languages.Text_Update_DownloadCompleted, ButtonEnum.YesNo) == MessageBoxResult.Yes)
            {
                OpenFileInExplorer(savePath);
                return;
            }
        }

        try
        {
            if (ConfigManager.CurrentConfig.UpdateSettings.KeepProfile)
            {
                File.Copy(ConfigManager.ConfigPath, ConfigManager.BackupConfigPath, true);
                await File.WriteAllTextAsync(Path.Combine(Core.App.StartupPath, "Cache", "preference.update"),
                    $"{ConfigManager.CurrentConfig.Skin}");
            }
        }
        catch
        {
            Icon = ICONS.ERROR;
            Status = Languages.Text_Update_BackupConfigFailed;
        }

        // 仅 Windows 执行自动安装
        if (OperatingSystem.IsWindows())
        {
            Core.App.CurrentLogger?.Log("正在安装更新", module: EnumLogModule.Update);
            await MessageBox.ShowAsync(Languages.Text_Update_RestartToInstall, Languages.Caption_Info, MessageBoxIcon.Info);

            // 当当前运行时编译类型与目标编译类型一致时，传入 /nocleanup 参数
            var currentType = Core.App.ReleaseFlag;
            var targetType = ConfigManager.CurrentConfig.UpdateSettings.CompileType;
            var sameType = string.Equals(currentType, targetType, StringComparison.OrdinalIgnoreCase);
            var installArgs = sameType ? "/silent /sp- /nocancel /nocleanup" : "/silent /sp- /nocancel";
            installArgs += " /nodownload";

            Core.App.CurrentLogger?.Log($"更新安装参数: 当前类型={currentType}, 目标类型={targetType}, 参数={installArgs}",
                module: EnumLogModule.Update);

            Process.Start(
                new ProcessStartInfo(savePath)
                    { UseShellExecute = true, Arguments = installArgs });
            App.Desktop.Shutdown();
        }
    }

    /// <summary>
    ///     按平台拼接应用更新包下载地址。
    ///     <br />
    ///     当前仅有 alist 主源（无备源），失败时 UI 会提示前往「设置」切换下载源后重试。
    /// </summary>
    private static string BuildUpdateDownloadUrl(PlatformID platform, string latestVersion)
    {
        return platform switch
        {
            PlatformID.Win32NT =>
                $"https://alist.yealqp.cn/download/ME-Frp%20PML2/mefrp/windows-distributions/{latestVersion}/pml2_setup%20{latestVersion}{(ConfigManager.CurrentConfig.UpdateSettings.CompileType == "AOT" ? "%20AOT" : "")}.exe",
            PlatformID.MacOSX =>
                $"https://alist.yealqp.cn/download/ME-Frp%20PML2/mefrp/macos-distributions/pml2-{latestVersion}-macos-x64.dmg",
            PlatformID.Unix =>
                $"https://alist.yealqp.cn/download/ME-Frp%20PML2/mefrp/linux-distributions/pml2-{latestVersion}-linux-x64.deb",
            _ => throw new NotSupportedException($"Unsupported platform: {platform}")
        };
    }

    /// <summary>
    ///     进入下载失败态：展示明确文案与「重试下载」按钮，杜绝静默失败。
    /// </summary>
    private void EnterDownloadFailedState(string status, Exception? ex, bool isVerify = false)
    {
        Core.App.CurrentLogger?.Log($"更新下载失败: {status}", EnumLogType.Error, module: EnumLogModule.Update);
        if (ex != null)
        {
            Core.App.CurrentLogger?.Error(ex);
        }

        Status = status;
        Icon = ICONS.ERROR;
        IsLoading = false;
        IsIdle = true;
        HasDownloadFailed = true;
        FailureTip = isVerify ? Languages.Text_Update_VerifyFailed : Languages.Text_Update_DownloadFailed;
        try
        {
            Core.App.MainWindow?.PlatformFeatures.SetTaskBarProgressBarState(TaskBarProgressBarState.None);
        }
        catch
        {
        }
    }

    private void RetryDownload()
    {
        HasDownloadFailed = false;
        FailureTip = null;
        DownloadUpdate();
    }

    [Obsolete("This method is kept for backward compatibility. Use DownloadUpdate() instead.")]
    public async void DownloadUpdateBak()
    {
        if (OperatingSystem.IsLinux())
        {
            await MessageBox.ShowAsync(Languages.Text_Update_LinuxManualUpdate, Languages.Text_Update_HardTimesCaption,
                MessageBoxIcon.Warning);
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            await MessageBox.ShowAsync(Languages.Text_Update_MacOSManualUpdate, Languages.Text_Update_HardTimesCaption,
                MessageBoxIcon.Warning);
            return;
        }

        Core.App.CurrentLogger?.Log("正在下载更新", module: EnumLogModule.Update);
        Status = Languages.Text_Update_Downloading;
        Icon = ICONS.DOWNLOAD;
        IsLoading = true;
        IsIdle = false;
        ProgressValue = 0;
        var DownloadOpt = new DownloadConfiguration
        {
            BufferBlockSize = 1024 * 32, // 通常，主机最大支持8000字节，默认值为8000。
            ChunkCount = 8, // 要下载的文件分片数量，默认值为1
            MaxTryAgainOnFailure = 5, // 失败的最大次数
            ParallelDownload = ConfigManager.CurrentConfig.ParallelDownload, // 下载文件是否为并行的。默认值为false
            ParallelCount = ConfigManager.CurrentConfig.ParallelCount,
            BlockTimeout = 5000, // 每个 stream reader  的超时（毫秒），默认值是1000

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
                    "RYCB/PML Desktop"
            }
        };
        downloader = new DownloadService(DownloadOpt);
        downloader.DownloadStarted += DownloaderOnDownloadStarted;
        downloader.DownloadProgressChanged += DownloaderOnDownloadProgressChanged;
        downloader.DownloadFileCompleted += DownloaderOnDownloadFileCompleted;
        await downloader.DownloadFileTaskAsync(
            $"https://alist.yealqp.cn/download/ME-Frp%20PML2/mefrp/windows-distributions/" +
            $"{LatestVersion}/pml2_setup%20{LatestVersion}.exe",
            Path.Combine(Core.App.StartupPath, "Cache", $"update_tmp_{LatestVersion}.exe"));
        Core.App.CurrentLogger?.Log("下载更新完成", module: EnumLogModule.Update);
        IsLoading = false;
        IsIdle = true;
        Icon = ICONS.LATEST;
        Status = Languages.Text_Update_DownloadCompleted;
        if (UpdateMethod != 0)
        {
            if (await MessageBox.ShowAsync(Languages.Text_Update_DownloadCompletedOpenDir, Languages.Caption_Info, ButtonEnum.YesNo) == MessageBoxResult.Yes)
            {
                OpenFileInExplorer(Path.Combine(Core.App.StartupPath, "Cache", $"update_tmp_{LatestVersion}.exe"));
                return;
            }
        }

        Core.App.CurrentLogger?.Log("正在安装更新", module: EnumLogModule.Update);
        await MessageBox.ShowAsync(Languages.Text_Update_RestartToInstall, Languages.Caption_Info, MessageBoxIcon.Info);
        Process.Start(
            new ProcessStartInfo(Path.Combine(Core.App.StartupPath, "Cache", $"update_tmp_{LatestVersion}.exe"))
                { UseShellExecute = true, Arguments = "/silent /sp- /nocancel" });
        App.Desktop.Shutdown();
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
        else if (OperatingSystem.IsMacOS())
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

    private void DownloaderOnDownloadStarted(object? sender, DownloadStartedEventArgs e)
    {
        Core.App.CurrentLogger?.Log($"开始下载: {e.FileName}\n文件大小: {e.TotalBytesToReceive}");
        MaxValue = e.TotalBytesToReceive;
    }

    private void DownloaderOnDownloadProgressChanged(object? sender, DownloadProgressChangedEventArgs e)
    {
        try
        {
            ProgressValue = e.ReceivedBytesSize;
            CurrentSpeed =
                $"{ProcessFileSize(e.ReceivedBytesSize)}/{ProcessFileSize(MaxValue)} {ProcessSpeed(e.BytesPerSecondSpeed)}";
            try
            {
                Core.App.MainWindow?.PlatformFeatures.SetTaskBarProgressBarValue((ulong)e.ReceivedBytesSize,
                    (ulong)e.TotalBytesToReceive);
            }
            catch
            {
            }
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger?.Error(ex);
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
    private string ProcessSpeed(double fileSize)
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
        string[] sizeUnits = ["B", "KB", "MB", "GB", "TB"];
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
            Core.App.CurrentLogger?.Error(e.Error, module: EnumLogModule.Net);
            Core.App.CurrentLogger?.Log("下载失败");
        }
        else
        {
            CurrentSpeed = "";
            Core.App.CurrentLogger?.Log("下载完成");
        }
    }

    private class ICONS
    {
        public const string UPDATE = "\xe68a";
        public const string DOWNLOAD = "\xe72b";
        public const string LATEST = "\xe653";
        public const string ERROR = "\xed27";
    }
}