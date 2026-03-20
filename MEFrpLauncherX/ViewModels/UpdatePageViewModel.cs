using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Animation;
using Avalonia.Collections;
using Downloader;
using FluentAvalonia.UI.Windowing;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Controls;
using MsBox.Avalonia.Enums;
using Newtonsoft.Json;
using ReactiveUI;
using DownloadProgressChangedEventArgs = Downloader.DownloadProgressChangedEventArgs;
// ReSharper disable EmptyGeneralCatchClause

namespace MEFrpLauncherX.ViewModels;

public class UpdatePageViewModel : ViewModelBase
{
    private class ICONS
    {
        public const string UPDATE = "\xe68a";
        public const string DOWNLOAD = "\xe72b";
        public const string LATEST = "\xe653";
        public const string ERROR = "\xed27";
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
    } = "从未检查过更新";

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
    } = App.Version;

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
    /// 0 - 稳定通道
    /// 1 - 预览通道
    /// </summary>
    public int UpdateChannel
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>
    /// 0 - 自动下载并安装
    /// 1 - 自动下载
    /// 2 - 手动下载
    /// </summary>
    public int UpdateMethod
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

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
    }

    /// <summary>
    /// 检查更新
    /// </summary>
    /// <returns>(是否最新, 最新版本)</returns>
    public static async Task<(bool, string)> GetNewVersionAsync()
    {
        var updateInfo = await RYCBApiConverter.GetLatestVersionInfoAsync();
        var preiewUpdateInfo = await RYCBApiConverter.GetLatestPreviewVersionInfoAsync();
        var isPreview = ConfigManager.CurrentConfig.UpdateSettings.Channel != "Stable";
        string latestVersion;
        if (isPreview)
        {
            latestVersion = GetLatestVersion(updateInfo, preiewUpdateInfo);
        }
        else
        {
            latestVersion = updateInfo.version;
        }

        return (VersionComparer.IsGreaterThan(latestVersion, App.Version), latestVersion);
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
        Status = "正在检查更新";
        var isPreview = ConfigManager.CurrentConfig.UpdateSettings.Channel != "Stable";
        SingleVersionInfo updateInfo = new()
            {
                data = new SingleVersionInfo.VersionInfo
                {
                    changes = ["获取更新失败"],
                    codename = App.Codename,
                    date = DateTime.Now.ToString("yyyy-MM-dd"),
                    description = "获取更新失败"
                },
                success = false,
                version = App.Version
            },
            preiewUpdateInfo = new()
            {
                data = new SingleVersionInfo.VersionInfo
                {
                    changes = ["获取更新失败"],
                    codename = App.Codename,
                    date = DateTime.Now.ToString("yyyy-MM-dd"),
                    description = "获取更新失败"
                },
                success = false,
                version = App.Version
            };
        try
        {
            updateInfo = await RYCBApiConverter.GetLatestVersionInfoAsync();
            preiewUpdateInfo = await RYCBApiConverter.GetLatestPreviewVersionInfoAsync();
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger?.Log("获取更新信息失败", type: EnumLogType.Error, module: EnumLogModule.Update);
            Core.App.CurrentLogger?.Error(ex);
        }

        string latestVersion;
        if (isPreview)
        {
            latestVersion = GetLatestVersion(updateInfo, preiewUpdateInfo);
        }
        else
        {
            latestVersion = updateInfo.version;
        }

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

        if (VersionComparer.IsGreaterThan(latestVersion, App.Version))
        {
            Core.App.CurrentLogger?.Log("检测到新版本: " + updateInfo.version, module: EnumLogModule.Update);
            IterationCount = new IterationCount(0);
            Icon = ICONS.DOWNLOAD;
            if (latestVersion == preiewUpdateInfo.version)
            {
                updateInfo = preiewUpdateInfo;
            }

            Status = "检测到新版本: " + latestVersion;
            LatestVersion = updateInfo.version;
            IsLoading = false;
            IsIdle = false;
            HasNewVersion = true;
            Codename = updateInfo.data.codename;
            Changelog.Clear();
            Changelog.AddRange(updateInfo.data.changes);
        }
        else
        {
            Core.App.CurrentLogger?.Log("当前版本已经是最新版本", module: EnumLogModule.Update);
            Status = "当前版本已经是最新版本";
            IsLoading = false;
            IsIdle = true;
        }

        LatestVersion = latestVersion;
        Changelog.Clear();
        Changelog.AddRange(updateInfo.data.changes);
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
        var res1 = VersionComparer.CompareVersions(App.Version, cd);
        return res1 switch
        {
            -1 => cd,
            1 => App.Version,
            _ => cd
        };
    }

    internal DownloadService downloader;

    public async void DownloadUpdate()
    {
        Core.App.CurrentLogger?.Log("正在下载更新", module: EnumLogModule.Update);
        Status = "正在下载更新";
        Icon = ICONS.DOWNLOAD;
        IsLoading = true;
        IsIdle = false;
        ProgressValue = 0;

        // ===== 1. 按系统拼接下载链接和临时文件名 =====
        string downloadUrl;
        string tempFileName;
        string systemTip; // 下载完成后的安装提示

        if (OperatingSystem.IsWindows())
        {
            downloadUrl = $"https://alist.yealqp.cn/download/ME-Frp%20PML2/mefrp/windows-distributions/" +
                          $"{LatestVersion}/pml2_setup%20{LatestVersion}.exe";
            tempFileName = $"update_tmp_{LatestVersion}.exe";
            systemTip = "点击确认打开安装文件所在目录，双击 exe 文件完成安装";
        }
        else if (OperatingSystem.IsMacOS())
        {
            downloadUrl =
                $"https://alist.yealqp.cn/download/ME-Frp%20PML2/mefrp/macos-distributions/pml2-{LatestVersion}-macos-x64.dmg";
            tempFileName = $"update_tmp_{LatestVersion}.dmg";
            systemTip = "点击确认打开安装文件所在目录，双击 dmg 文件完成安装";
        }
        else if (OperatingSystem.IsLinux())
        {
            downloadUrl =
                $"https://alist.yealqp.cn/download/ME-Frp%20PML2/mefrp/linux-distributions/pml2-{LatestVersion}-linux-x64.deb";
            tempFileName = $"update_tmp_{LatestVersion}.deb";
            systemTip = "点击确认打开安装文件所在目录，使用 dpkg -i 命令安装 deb 包";
        }
        else
        {
            await MessageBox.ShowAsync("暂不支持当前操作系统的自动更新，请手动下载安装包", "不支持的系统",
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
            Timeout = 5000,
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
                    "RYCB/PML Desktop",
            }
        };

        // ===== 3. 初始化下载器并开始下载 =====
        downloader = new DownloadService(DownloadOpt);
        downloader.DownloadStarted += DownloaderOnDownloadStarted;
        downloader.DownloadProgressChanged += DownloaderOnDownloadProgressChanged;
        downloader.DownloadFileCompleted += DownloaderOnDownloadFileCompleted;

        var savePath = Path.Combine(Core.App.StartupPath, "Cache", tempFileName);
        await downloader.DownloadFileTaskAsync(downloadUrl, savePath);

        // ===== 4. 下载完成后处理 =====
        Core.App.CurrentLogger?.Log("下载更新完成", module: EnumLogModule.Update);
        IsLoading = false;
        IsIdle = true;
        Icon = ICONS.LATEST;
        Status = "下载更新完成";

        try
        {
            Core.App.MainWindow?.PlatformFeatures.SetTaskBarProgressBarState(TaskBarProgressBarState.None);
        }
        catch
        {
        } // Windows 自动安装逻辑保留，Linux/macOS 仅打开目录+提示

        if (UpdateMethod != 0 || !OperatingSystem.IsWindows())
        {
            if (await MessageBox.ShowAsync($"{systemTip}", "更新下载完成", ButtonEnum.YesNo) == MessageBoxResult.Yes)
            {
                OpenFileInExplorer(savePath);
                return;
            }
        }

        try
        {
            if (ConfigManager.CurrentConfig.UpdateSettings.KeepProfile)
            {
                File.Copy(ConfigManager.ConfigPath, ConfigManager.ConfigPath + ".bak.update");
            }
        }
        catch
        {
            Icon = ICONS.ERROR;
            Status = "备份配置文件失败, 请手动备份配置文件";
        }
        // 仅 Windows 执行自动安装
        if (OperatingSystem.IsWindows())
        {
            Core.App.CurrentLogger?.Log("正在安装更新", module: EnumLogModule.Update);
            await MessageBox.ShowAsync("即将关闭程序以自动安装更新", "信息", MessageBoxIcon.Info);
            Process.Start(
                new ProcessStartInfo(savePath)
                    { UseShellExecute = true, Arguments = "/silent /sp- /nocancel" });
            App.Desktop.Shutdown();
        }
    }

    public async void DownloadUpdateBak()
    {
        if (OperatingSystem.IsLinux())
        {
            await MessageBox.ShowAsync("由于技术原因, 目前我们只提供Windows的自动更新服务。请您到最新版本页手动下载并覆盖安装, 或使用安装脚本安装。", "我们都有不顺利的时候",
                MessageBoxIcon.Warning);
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            await MessageBox.ShowAsync("由于技术原因, 目前我们只提供Windows的自动更新服务。请您到最新版本页手动下载并覆盖安装。", "我们都有不顺利的时候",
                MessageBoxIcon.Warning);
            return;
        }

        Core.App.CurrentLogger?.Log("正在下载更新", module: EnumLogModule.Update);
        Status = "正在下载更新";
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
                    "RYCB/PML Desktop",
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
        Status = "下载更新完成";
        if (UpdateMethod != 0)
        {
            if (await MessageBox.ShowAsync("更新下载完成！点击确认打开安装文件所在目录", "信息", ButtonEnum.YesNo) == MessageBoxResult.Yes)
            {
                OpenFileInExplorer(Path.Combine(Core.App.StartupPath, "Cache", $"update_tmp_{LatestVersion}.exe"));
                return;
            }
        }

        Core.App.CurrentLogger?.Log("正在安装更新", module: EnumLogModule.Update);
        await MessageBox.ShowAsync("即将关闭程序以自动安装更新", "信息", MessageBoxIcon.Info);
        Process.Start(
            new ProcessStartInfo(Path.Combine(Core.App.StartupPath, "Cache", $"update_tmp_{LatestVersion}.exe"))
                { UseShellExecute = true, Arguments = "/silent /sp- /nocancel" });
        App.Desktop.Shutdown();
    }

    void OpenFileInExplorer(string filePath)
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
    /// 根据<paramref name="fileSize"/>的大小自动返回对应的文件大小值。
    /// <br/>
    /// 如：若<paramref name="fileSize"/>32743879328,则返回30.50GB；
    /// 返回值的数值范围为1~1000。
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
    /// 根据<paramref name="fileSize"/>的大小自动返回对应的文件大小值。
    /// <br/>
    /// 如：若<paramref name="fileSize"/>32743879328,则返回30.50GB；
    /// 返回值的数值范围为1~1000。
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
}

/// <summary>
/// 更新清单模型，用于描述更新包中的文件及其对应的SHA256哈希值
/// </summary>
public class UpdateManifest
{
    /// <summary>
    /// 文件路径与SHA256哈希值的映射集合
    /// </summary>
    [JsonProperty("files")]
    public Dictionary<string, string> Files
    {
        get;
        set;
    } = new();
}