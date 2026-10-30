using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Downloader;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Windowing;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Core.Models;
using MEFrpLauncherX.Core.Services;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using ReactiveUI.Primitives;
using DownloadProgressChangedEventArgs = Downloader.DownloadProgressChangedEventArgs;
// ReSharper disable InconsistentNaming

// ReSharper disable EmptyGeneralCatchClause

namespace MEFrpLauncherX.ViewModels;

public class UpdatePageViewModel : ViewModelBase
{
    internal DownloadService downloader;

    /// <summary>
    ///     本次检查更新期间下载失败的 GitHub 资产名（26.4）。
    ///     用于「重试下载」时优先挑选同一 Release 中的其他安装包。
    /// </summary>
    private readonly HashSet<string> _failedAssetNames = new(StringComparer.OrdinalIgnoreCase);

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
        DownloadSource =
            GitHubUpdateSources.Normalize(ConfigManager.CurrentConfig.UpdateSettings.DownloadSource) switch
            {
                GitHubUpdateSources.GitHub => 1,
                GitHubUpdateSources.GitHubGhProxy => 2,
                GitHubUpdateSources.GitHubMoeyy => 3,
                _ => 0
            };

        CheckUpdateCommand = ReactiveCommand.Create(CheckUpdate);
        RetryDownloadCommand = ReactiveCommand.Create(RetryDownload);
        DownloadUpdateCommand = ReactiveCommand.Create(DownloadUpdate);

        // 26.5.0：旧版本目录策略与版本目录占用巡检
        KeepOldVersionChoice = ConfigManager.CurrentConfig.UpdateSettings.KeepOldVersion switch
        {
            UpdateSettings.UpdateOldVersionValues.Keep => UpdateSettings.UpdateOldVersionValues.Keep,
            UpdateSettings.UpdateOldVersionValues.Discard => UpdateSettings.UpdateOldVersionValues.Discard,
            _ => UpdateSettings.UpdateOldVersionValues.Ask
        };
        RefreshVersionStorage();
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
    } = Icons.UPDATE;

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

    public bool IsCheckUpdate
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = false;

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
    ///     「旧版本目录」下拉的当前选中项（对应 <see cref="UpdateSettings.KeepOldVersion" />）。
    ///     供设置页与升级前的询问弹窗共用，避免两处逻辑漂移。
    /// </summary>
    public string KeepOldVersionChoice { get; private set; }

    /// <summary>
    ///     是否有可清理的旧版本目录（不含当前版本，也不含回滚点）。
    ///     仅版本化布局下可能为 true；开发态与旧版单层布局恒为 false。
    /// </summary>
    public bool HasRemovableVersions
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>版本目录占用情况的可读摘要（供设置页展示）。</summary>
    public string VersionStorageSummary
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    /// <summary>设置「旧版本目录」策略。</summary>
    public void SetKeepOldVersion(string value)
    {
        KeepOldVersionChoice = value;
        ConfigManager.UpdateConfig(cfg => cfg.UpdateSettings.KeepOldVersion = value);
    }

    /// <summary>
    ///     重新巡检版本目录占用，刷新「可清理」相关属性。
    ///     <para>构造时调用一次即可；删除动作后需再次调用以刷新摘要。</para>
    /// </summary>
    public void RefreshVersionStorage()
    {
        try
        {
            var inspection = VersionCleanupService.Inspect();

            HasRemovableVersions = inspection.Removable.Count > 0;
            VersionStorageSummary = HasRemovableVersions
                ? string.Format(
                    Languages.Text_Update_VersionStorageSummary,
                    inspection.Entries.Count,
                    ToolboxService.FormatFileSize(inspection.TotalSizeBytes),
                    inspection.Removable.Count,
                    ToolboxService.FormatFileSize(inspection.RemovableSizeBytes))
                : string.Empty;
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger?.Error(ex, "统计版本目录占用失败");
        }
    }

    /// <summary>
    ///     删除全部可清理的旧版本目录（不含当前版本与回滚点）。
    /// </summary>
    /// <returns>(删除数量, 失败数量, 回收字节数)</returns>
    public (int Removed, int Failed, long Freed) CleanupOldVersions()
    {
        var result = VersionCleanupService.RemoveAllRemovable();
        RefreshVersionStorage();
        return result;
    }

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
    ///     更新下载源（26.4）：
    ///     <para>0 - TPCA（自建 Alist CDN）</para>
    ///     <para>1 - GitHub（官方直连）</para>
    ///     <para>2 - GitHub（gh-proxy 镜像）</para>
    ///     <para>3 - GitHub（moeyy 镜像）</para>
    /// </summary>
    public int DownloadSource
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ReactiveCommand<RxVoid, RxVoid> CheckUpdateCommand { get; }

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
    public ReactiveCommand<RxVoid, RxVoid> RetryDownloadCommand { get; }

    /// <summary>
    ///     下载并安装更新（绑定入口，禁止直接绑定方法）
    /// </summary>
    public ReactiveCommand<RxVoid, RxVoid> DownloadUpdateCommand { get; }

    /// <summary>
    ///     最近一次「检查更新」的结果（供精简主页推荐使用，避免重复网络请求）。
    ///     null 表示尚未检查过。
    /// </summary>
    public static bool? HasKnownUpdate { get; private set; }

    /// <summary>最近一次检查到的最新版本号（无更新或未检查时为空）</summary>
    public static string? LatestKnownVersion { get; private set; }

    /// <summary>记录一次检查更新结果（由 <see cref="CheckUpdate" /> 与自动检查复用）</summary>
    internal static void ReportUpdateCheck(bool hasUpdate, string? latestVersion)
    {
        HasKnownUpdate = hasUpdate;
        LatestKnownVersion = hasUpdate ? latestVersion : null;
    }

    /// <summary>
    ///     检查更新
    /// </summary>
    /// <param name="forceRefresh">
    ///     true 表示用户显式检查更新，跳过 5 分钟缓存强制请求；
    ///     false 表示启动时的自动检查等场景，可复用有效期内的缓存结果。
    /// </param>
    /// <returns>(是否最新, 最新版本)</returns>
    public static async Task<(bool, string)> GetNewVersionAsync(bool forceRefresh = false)
    {
        var updateInfo = await RYCBApiConverter.GetLatestVersionInfoAsync(forceRefresh);
        var preiewUpdateInfo = await RYCBApiConverter.GetLatestPreviewVersionInfoAsync(forceRefresh);
        var isPreview = ConfigManager.CurrentConfig.UpdateSettings.Channel != "Stable";
        var latestVersion = isPreview ? GetLatestVersion(updateInfo, preiewUpdateInfo) : updateInfo.Version;

        return (VersionComparer.IsGreaterThan(latestVersion, Core.App.Version), latestVersion);
    }

    /// <summary>
    ///     用户显式「检查更新」（26.4：跳过 5 分钟缓存，始终拉取最新）。
    /// </summary>
    public async void CheckUpdate() => await CheckUpdateCoreAsync(true);

    /// <summary>
    ///     检查更新的实际实现。
    /// </summary>
    /// <param name="forceRefresh">true 表示跳过 5 分钟缓存强制请求</param>
    private async Task CheckUpdateCoreAsync(bool forceRefresh)
    {
        Icon = Icons.UPDATE;
        IsLoading = true;
        IsIdle = true;
        LatestCheckTime = DateTime.Now;
        try
        {
            Core.App.MainWindow?.PlatformFeatures.SetTaskBarProgressBarState(FATaskBarProgressBarState.Indeterminate);
        }
        catch
        {
            // Platform not support
        }

        IsCheckUpdate = true;
        Core.App.CurrentLogger?.Log("正在检查更新", module: EnumLogModule.Update);
        Status = Languages.Text_Update_Checking;
        var isPreview = ConfigManager.CurrentConfig.UpdateSettings.Channel != "Stable";
        SingleVersionInfo updateInfo = new()
            {
                Data = new SingleVersionInfo.VersionInfo
                {
                    Changes = [Languages.Text_Update_FetchFailed],
                    Codename = App.Codename,
                    Date = DateTime.Now.ToString("yyyy-MM-dd"),
                    Description = Languages.Text_Update_FetchFailed
                },
                Success = false,
                Version = Core.App.Version
            },
            preiewUpdateInfo = new()
            {
                Data = new SingleVersionInfo.VersionInfo
                {
                    Changes = [Languages.Text_Update_FetchFailed],
                    Codename = App.Codename,
                    Date = DateTime.Now.ToString("yyyy-MM-dd"),
                    Description = Languages.Text_Update_FetchFailed
                },
                Success = false,
                Version = Core.App.Version
            };
        try
        {
            updateInfo = await RYCBApiConverter.GetLatestVersionInfoAsync(forceRefresh);
            preiewUpdateInfo = await RYCBApiConverter.GetLatestPreviewVersionInfoAsync(forceRefresh);
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger?.Log("获取更新信息失败", EnumLogType.Error, module: EnumLogModule.Update);
            Core.App.CurrentLogger?.Error(ex);
            Status = Languages.Text_Update_FetchFailed;
            FailureTip = Languages.Text_Update_FetchFailedTip;
            Icon = Icons.ERROR;
            IsIdle = false;
            return;
        }

        string latestVersion;
        latestVersion = isPreview ? GetLatestVersion(updateInfo, preiewUpdateInfo) : updateInfo.Version;

// #if !DEBUG
//         Core.App.CurrentLogger.LogDebug("[DEBUG] 模拟更新", module: EnumLogModule.Update);
//         updateInfo.Version = "999.999.999.99";
// #endif
        // var VersionRegex = new Regex(@"^\d+(?:\.\d+){2,4}");
        // var Version = VersionRegex.Match(App.Version);
        if (VersionComparer.IsLessThan(updateInfo.Version, preiewUpdateInfo.Version) ||
            VersionComparer.IsLessThan(latestVersion, preiewUpdateInfo.Version) ||
            latestVersion == preiewUpdateInfo.Version)
        {
            updateInfo = preiewUpdateInfo;
        }

        if (VersionComparer.IsGreaterThan(latestVersion, Core.App.Version))
        {
            Core.App.CurrentLogger?.Log("检测到新版本: " + updateInfo.Version, module: EnumLogModule.Update);
            IsCheckUpdate = false;
            Icon = Icons.DOWNLOAD;
            if (latestVersion == preiewUpdateInfo.Version)
            {
                updateInfo = preiewUpdateInfo;
            }

            Status = Languages.Text_Update_NewVersionDetected + latestVersion;
            LatestVersion = updateInfo.Version;
            IsLoading = false;
            IsIdle = false;
            HasNewVersion = true;
            FailureTip = null;
            Codename = updateInfo.Data.Codename;
            Changelog.Clear();
            Changelog.AddRange(updateInfo.Data.Changes);
            // 供精简主页推荐使用：记录本次检查结果
            ReportUpdateCheck(true, latestVersion);
        }
        else
        {
            Core.App.CurrentLogger?.Log("当前版本已经是最新版本", module: EnumLogModule.Update);
            Status = Languages.Text_Update_AlreadyLatest;
            Icon = Icons.LATEST;
            IsLoading = false;
            IsIdle = true;
            FailureTip = null;
            ReportUpdateCheck(false, null);
        }

        if (updateInfo is { Success: true, Data.Changes.Length: > 0 })
        {
            LatestVersion = latestVersion;
            Changelog.Clear();
            Changelog.AddRange(updateInfo.Data.Changes);
        }
        else
        {
            Core.App.CurrentLogger?.Log("获取更新信息失败", EnumLogType.Error, module: EnumLogModule.Update);
            Status = Languages.Text_Update_FetchFailed;
            FailureTip = Languages.Text_Update_FetchFailedTip;
            Icon = Icons.ERROR;
            IsIdle = false;
            return;
        }

        try
        {
            Core.App.MainWindow?.PlatformFeatures.SetTaskBarProgressBarState(FATaskBarProgressBarState.None);
        }
        catch
        {
        }

        Core.App.CurrentLogger?.Log("检查更新完成", module: EnumLogModule.Update);
        IsCheckUpdate = false;
    }

    private static string GetLatestVersion(SingleVersionInfo updateInfo, SingleVersionInfo preiewUpdateInfo)
    {
        var res = VersionComparer.CompareVersions(updateInfo.Version, preiewUpdateInfo.Version);
        var cd = res switch
        {
            -1 => preiewUpdateInfo.Version,
            1 => updateInfo.Version,
            _ => preiewUpdateInfo.Version
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
        Icon = Icons.DOWNLOAD;
        IsLoading = true;
        IsIdle = false;
        HasDownloadFailed = false;
        FailureTip = null;
        ProgressValue = 0;

        // ===== 1. 前置判定：应用内更新只支持 zip + 分离式布局 =====
        // 26.5.1 起的语义区分：
        //   安装包（exe / dmg / deb / rpm / tar.gz）→ 仅面向用户**手动**下载安装；
        //   zip                                   → 应用内自动更新专用（解压进新的 vXXX\）。
        // 这套流程依赖安装根下的启动器与 launcher.json；开发态 / ≤26.4 的扁平布局
        // 没有启动器来加载新版本目录，只能到发布页手动下载安装包。
        if (!Core.AppPaths.IsVersionedLayout)
        {
            Core.App.CurrentLogger?.Warning("当前不是版本化安装布局，应用内自动更新不可用",
                module: EnumLogModule.Update);
            IsLoading = false;
            IsIdle = true;
            Icon = Icons.ERROR;
            Status = Languages.Text_Update_VersionLayoutRequired;
            FailureTip = Languages.Text_Update_VersionLayoutRequired;
            return;
        }

        // ===== 2. 解析下载源（两条线路都返回 zip 更新包） =====
        var resolved = await ResolveDownloadAsync();

        // 解析失败（GitHub 线路下无匹配 zip / 网络不可用）时进入失败态，保留重试入口
        if (resolved is null)
        {
            EnterDownloadFailedState(Languages.Text_Update_DownloadFailed, null);
            return;
        }

        var downloadUrl = resolved.Url;

        // 临时文件名固定为 .zip：更新包只有一种格式，不再按 deb / exe / dmg 推断安装方式
        var tempFileName = $"update_tmp_{LatestVersion}{UpdatePackageService.PackageExtension}";

        // ===== 3. 下载配置（复用原有逻辑） =====
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

        // ===== 4. 初始化下载器并开始下载 =====
        downloader = new DownloadService(DownloadOpt);
        downloader.DownloadStarted += DownloaderOnDownloadStarted;
        downloader.DownloadProgressChanged += DownloaderOnDownloadProgressChanged;
        downloader.DownloadFileCompleted += DownloaderOnDownloadFileCompleted;

        var savePath = Path.Combine(Core.AppPaths.CacheDirectory, tempFileName);

        // ===== 5. 下载（失败不再静默：明确文案 + 重试入口） =====
        try
        {
            await downloader.DownloadFileTaskAsync(downloadUrl, savePath);
        }
        catch (Exception ex)
        {
            // 记录失败的资产，供「重试下载」换用同一 Release 中的其他安装包
            if (resolved.AssetName is not null)
            {
                _failedAssetNames.Add(resolved.AssetName);
            }

            EnterDownloadFailedState(Languages.Text_Update_DownloadFailed, ex);
            return;
        }

        // 校验下载产物：文件必须存在且非空，否则视为下载/校验失败（禁止静默失败）
        if (!File.Exists(savePath) || new FileInfo(savePath).Length <= 0)
        {
            EnterDownloadFailedState(Languages.Text_Update_VerifyFailed, null, isVerify: true);
            return;
        }

        // ===== 6. 下载完成后处理 =====
        Core.App.CurrentLogger?.Log("下载更新包完成", module: EnumLogModule.Update);
        IsLoading = false;
        IsIdle = true;
        Icon = Icons.LATEST;
        Status = Languages.Text_Update_DownloadCompleted;

        try
        {
            Core.App.MainWindow?.PlatformFeatures.SetTaskBarProgressBarState(FATaskBarProgressBarState.None);
        }
        catch
        {
            // 忽略：任务栏进度条只是辅助反馈
        }

        // 「仅下载 / 手动下载」模式：只把 zip 放进 data/Cache，询问是否打开所在目录
        if (UpdateMethod != 0)
        {
            if (await MessageBox.ShowAsync(Languages.Text_Update_DownloadCompletedOpenDir, Languages.Caption_Info,
                    ButtonEnum.YesNo) == MessageBoxResult.Yes)
            {
                OpenFileInExplorer(savePath);
            }

            return;
        }

        // ===== 7. 解压并切换版本（不再调用任何系统安装器） =====
        Status = Languages.Text_Update_Extracting;
        Icon = Icons.DOWNLOAD;
        IsLoading = true;
        // 解压阶段没有可用的字节进度：切到不确定进度，避免进度条停在 100% 造成误解
        IsIdle = true;

        // 旧版本目录的去留：Ask 先问（没有可清理的旧目录时直接保留），Keep / Discard 沿用设置
        var removeOldVersions = await ResolveRemoveOldVersionsAsync();

        // 解压属于磁盘密集型操作，放到线程池执行，避免卡住 UI 线程
        var applyResult = await Task.Run(() => UpdatePackageService.Apply(
            savePath, LatestVersion, removeOldVersions, Core.App.CurrentLogger));

        IsLoading = false;
        IsIdle = true;

        if (!applyResult.Success)
        {
            // 解压 / 槽位写入失败：保留「重试下载」入口（重新下载并重试落地）。
            // 这种失败与网络无关，提示不要引导用户“切换下载源”，而是直接给解压失败文案。
            EnterDownloadFailedState(Languages.Text_Update_ExtractFailed, null);
            FailureTip = Languages.Text_Update_ExtractFailed;
            return;
        }

        // 更新包已落地进版本目录，zip 本体不再需要（继续保留只会占用 data/Cache）
        try
        {
            File.Delete(savePath);
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger?.Warning($"删除更新包缓存失败: {ex.Message}", module: EnumLogModule.Update);
        }

        // 刷新「可清理版本」统计（设置页与更新页共用）
        RefreshVersionStorage();

        Icon = Icons.LATEST;
        Status = Languages.Text_Update_DownloadCompleted;
        Core.App.CurrentLogger?.Log(
            $"更新包已安装到 {applyResult.VersionFolder}（清理旧版本: {(removeOldVersions ? "是" : "否")}），重启后生效",
            module: EnumLogModule.Update);

        await MessageBox.ShowAsync(Languages.Text_Update_RestartToInstall, Languages.Caption_Info, MessageBoxIcon.Info);

        // 重启交给安装根下的启动器：它读取 launcher.json 后进入新的 vXXX 目录。
        // 不可直接启动 MEFrpLauncherX.exe —— 那会跳过版本管理，还锁死在旧目录上。
        Core.App.CurrentLogger?.Log("正在重启到新版本", module: EnumLogModule.Update);
        Core.AppPaths.RestartViaLauncher();

        // 走统一退出入口（先给所有终端发 Ctrl+C 停掉隧道），避免更新后残留 mefrpc 进程占端口
        if (Views.MainWindow.Instance is { } mainWindow)
        {
            mainWindow.ExitApplication();
        }
        else
        {
            App.Desktop.Shutdown();
        }
    }

    /// <summary>
    ///     依据「旧版本目录」策略决定本次升级是否连回滚点一起清理（26.5.1）。
    ///     <para>
    ///         策略为 <c>Ask</c> 时弹窗让用户在「保留（可回滚）」与「删除（省空间）」之间选择；
    ///         仅当确实存在可清理的旧版本目录时才询问 —— 没有旧目录时问了也是废话。
    ///         选择结果会写回设置，下次升级不再重复询问。
    ///     </para>
    /// </summary>
    /// <returns>true 表示连回滚点一起删除；false 表示保留回滚点</returns>
    private async Task<bool> ResolveRemoveOldVersionsAsync()
    {
        // 非版本化布局（开发态 / ≤26.4）没有版本目录概念，无从清理
        if (!Core.AppPaths.IsVersionedLayout)
        {
            return false;
        }

        if (KeepOldVersionChoice == UpdateSettings.UpdateOldVersionValues.Ask)
        {
            var inspection = VersionCleanupService.Inspect();
            if (inspection.Removable.Count == 0)
            {
                // 本次升级不会产生历史目录（首个版本化安装 / 上次已清理干净）
                return false;
            }

            var keep = await AskKeepOldVersionAsync(inspection);
            if (keep is null)
            {
                // 用户关闭了弹窗：按默认值（保留回滚点）处理
                return false;
            }

            KeepOldVersionChoice = keep.Value ? UpdateSettings.UpdateOldVersionValues.Keep
                : UpdateSettings.UpdateOldVersionValues.Discard;
            ConfigManager.UpdateConfig(cfg => cfg.UpdateSettings.KeepOldVersion = KeepOldVersionChoice);
        }

        return KeepOldVersionChoice == UpdateSettings.UpdateOldVersionValues.Discard;
    }

    /// <summary>
    ///     询问用户升级后如何处理旧版本目录。
    /// </summary>
    /// <returns>true 保留、false 删除、null 用户取消（按默认处理）</returns>
    private static async Task<bool?> AskKeepOldVersionAsync(VersionCleanupService.Inspection inspection)
    {
        // 单个旧版本目录的体积 —— 用它换算「保留」与「删除」各自的代价
        var singleSize = ToolboxService.FormatFileSize(
            inspection.Removable.Count > 0 ? inspection.Removable[0].SizeBytes : 0);

        var content = new StackPanel
        {
            Spacing = 10,
            Width = 380,
            Children =
            {
                new TextBlock
                {
                    Text = string.Format(Languages.Text_Update_AskKeepOldVersion_Message,
                        Core.App.Version,
                        string.Join("、", inspection.Removable.Select(e => e.FolderName))),
                    TextWrapping = TextWrapping.Wrap
                },
                BuildChoiceCard(Languages.Text_Update_AskKeepOldVersion_Keep,
                    string.Format(Languages.Text_Update_AskKeepOldVersionDetail_Keep, singleSize)),
                BuildChoiceCard(Languages.Text_Update_AskKeepOldVersion_Discard,
                    string.Format(Languages.Text_Update_AskKeepOldVersionDetail_Discard, singleSize))
            }
        };

        // 用 Primary / Secondary 两个标准按钮承载「保留 / 删除」，
        // 与仓库内既有对话框（如 DnsAccountsWindowViewModel）保持一致，
        // 避免为两个固定选项自绘按钮。
        var dialog = new FAContentDialog
        {
            Title = Languages.Text_Update_AskKeepOldVersion_Title,
            Content = content,
            PrimaryButtonText = Languages.Text_Update_AskKeepOldVersion_Keep,
            SecondaryButtonText = Languages.Text_Update_AskKeepOldVersion_Discard,
            CloseButtonText = Languages.Text_Global_Cancel,
            DefaultButton = FAContentDialogButton.Primary
        };

        var result = await dialog.ShowAsync();

        return result switch
        {
            FAContentDialogResult.Primary => true,
            FAContentDialogResult.Secondary => false,
            _ => null
        };
    }

    /// <summary>构造「选项卡片」（标题 + 说明），用于询问弹窗。</summary>
    private static Border BuildChoiceCard(string title, string description) => new()
    {
        Padding = new Thickness(12),
        CornerRadius = new CornerRadius(6),
        BorderThickness = new Thickness(1),
        BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#33808080")),
        Child = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock { Text = title, FontWeight = FontWeight.SemiBold },
                new TextBlock
                {
                    Text = description,
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = 0.8,
                    Classes = { "CaptionTextBlock" }
                }
            }
        }
    };

    /// <summary>
    ///     解析出的下载目标：URL 与资产名（TPCA 线路下也提供，由命名规则拼出）。
    /// </summary>
    private sealed record ResolvedDownload(string Url, string? AssetName);

    /// <summary>
    ///     按当前「下载源」解析更新包（zip）下载地址。
    ///     <para>
    ///         两条线路解析到的都是同一份东西 —— <b>应用内更新 zip</b>，与安装包完全无关：
    ///         <list type="bullet">
    ///             <item>TPCA：按「版本 + 平台 + 架构 + 编译类型」直接拼 Alist CDN 地址；</item>
    ///             <item>GitHub 线路：通过 REST API 拉 Release，挑选同名 zip 资产，
    ///                   并按「镜像前缀 + 原始下载地址」拼接（与 install.sh 的规则一致）。</item>
    ///         </list>
    ///     </para>
    /// </summary>
    /// <returns>解析结果；失败返回 null（原因已记日志）</returns>
    private async Task<ResolvedDownload?> ResolveDownloadAsync()
    {
        var source = ConfigManager.CurrentConfig.UpdateSettings.DownloadSource;

        // 文件名与发布工作流产出一致：pml2-{version}-{os}-{arch}-{mode}.zip
        var fileName = GitHubReleaseService.BuildUpdatePackageName(LatestVersion);
        if (fileName is null)
        {
            Core.App.CurrentLogger?.Warning(
                $"无法确定本机平台 / 架构的更新包名（{RuntimeInformation.RuntimeIdentifier}）",
                module: EnumLogModule.Update);
            return null;
        }

        if (!GitHubUpdateSources.IsGitHub(source))
        {
            var platform = GitHubReleaseService.PlatformTag;
            if (platform is null)
            {
                return null;
            }

            return new ResolvedDownload(
                GitHubUpdateSources.BuildTpcaUpdatePackageUrl(fileName, LatestVersion, platform), fileName);
        }

        var asset = await SelectGitHubAssetAsync(_failedAssetNames);
        if (asset is null)
        {
            return null;
        }

        Status = string.Format(Languages.Text_Update_DownloadSourceResolvedFormat,
            GitHubUpdateSources.GetDisplayName(source), asset.Name);
        return new ResolvedDownload(
            GitHubUpdateSources.BuildDownloadUrl(source, asset.BrowserDownloadUrl!), asset.Name);
    }

    /// <summary>
    ///     从 GitHub Release 中挑选匹配当前环境的安装包。
    /// </summary>
    private async Task<GitHubAsset?> SelectGitHubAssetAsync(
        IReadOnlyCollection<string> excludedNames)
    {
        var cfg = ConfigManager.CurrentConfig.UpdateSettings;
        // 优先使用缓存；仅在缓存缺失时才真正发起 API 请求
        var release = await GitHubReleaseService.GetReleaseAsync(LatestVersion, cfg.DownloadSource);
        if (release is null)
        {
            // 缓存 / 请求均失败时强制刷新一次，避免复用了不完整的结果
            release = await GitHubReleaseService.GetReleaseAsync(LatestVersion, cfg.DownloadSource, true);
        }

        if (release is null)
        {
            return null;
        }

        // 26.5.0：编译类型不再是用户可选项 —— 更新包始终与当前运行时一致，
        // 否则可能下载到另一种编译产物（AOT ↔ Common 无法原地替换运行）。
        var asset = GitHubReleaseService.SelectAsset(release, LatestVersion, Core.App.ReleaseFlag,
            excludedNames);
        if (asset is null && excludedNames.Count > 0)
        {
            // 排除列表把候选资产筛空时放宽限制再选一次（保证「重试」仍有机会成功）
            asset = GitHubReleaseService.SelectAsset(release, LatestVersion, Core.App.ReleaseFlag);
        }

        return asset;
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
        Icon = Icons.ERROR;
        IsLoading = false;
        IsIdle = true;
        HasDownloadFailed = true;
        FailureTip = isVerify ? Languages.Text_Update_VerifyFailed : Languages.Text_Update_DownloadFailed;
        try
        {
            Core.App.MainWindow?.PlatformFeatures.SetTaskBarProgressBarState(FATaskBarProgressBarState.None);
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
        Icon = Icons.DOWNLOAD;
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
            Path.Combine(Core.AppPaths.CacheDirectory, $"update_tmp_{LatestVersion}.exe"));
        Core.App.CurrentLogger?.Log("下载更新完成", module: EnumLogModule.Update);
        IsLoading = false;
        IsIdle = true;
        Icon = Icons.LATEST;
        Status = Languages.Text_Update_DownloadCompleted;
        if (UpdateMethod != 0)
        {
            if (await MessageBox.ShowAsync(Languages.Text_Update_DownloadCompletedOpenDir, Languages.Caption_Info, ButtonEnum.YesNo) == MessageBoxResult.Yes)
            {
                OpenFileInExplorer(Path.Combine(Core.AppPaths.CacheDirectory, $"update_tmp_{LatestVersion}.exe"));
                return;
            }
        }

        Core.App.CurrentLogger?.Log("正在安装更新", module: EnumLogModule.Update);
        await MessageBox.ShowAsync(Languages.Text_Update_RestartToInstall, Languages.Caption_Info, MessageBoxIcon.Info);
        Process.Start(
                new ProcessStartInfo(Path.Combine(Core.AppPaths.CacheDirectory, $"update_tmp_{LatestVersion}.exe"))
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

    private static class Icons
    {
        public const string UPDATE = "\xe68a";
        public const string DOWNLOAD = "\xe72b";
        public const string LATEST = "\xe653";
        public const string ERROR = "\xed27";
    }
}