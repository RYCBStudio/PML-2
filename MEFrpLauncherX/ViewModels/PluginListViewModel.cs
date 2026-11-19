using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Analysis;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Plugin.Core;
using MEFrpLauncherX.Plugin.Services;
using MEFrpLauncherX.Views;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using ReactiveUI;
// 注意：不能 using ReactiveUI.Primitives —— 其 LinqExtensions 复刻了全套 Rx LINQ 扩展，
// 会与 System.Reactive.Linq 的 Select/Throttle 等产生 CS0121 二义性。
// 这里只需要 RxVoid 类型，故用别名导入。
using RxVoid = ReactiveUI.Primitives.RxVoid;

namespace MEFrpLauncherX.ViewModels;

public class PluginListViewModel : ViewModelBase
{
    private readonly PluginService _pluginService = PluginService.Instance;

    private const string AlistBase = "https://alist.yealqp.cn";
    private const string PluginsApiPath = "/ME-Frp PML2/mefrp/market/plugins";

    public ObservableCollection<PluginInfo> Plugins
    {
        get;
    } = [];

    public PluginInfo? SelectedPlugin
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsLoaded => _pluginService.IsLoaded;

    // ---- Commands ----

    public ReactiveCommand<RxVoid, RxVoid> ReloadPluginsCommand
    {
        get;
    }

    public ReactiveCommand<RxVoid, RxVoid> InstallPluginCommand
    {
        get;
    }

    public ReactiveCommand<RxVoid, RxVoid> UninstallPluginCommand
    {
        get;
    }

    public ReactiveCommand<RxVoid, RxVoid> TogglePluginCommand
    {
        get;
    }

    public ReactiveCommand<RxVoid, RxVoid> ViewYamlCommand
    {
        get;
    }

    public ReactiveCommand<string, RxVoid> OpenPluginFolderCommand
    {
        get;
    }

    // ---- 表单编辑器入口（26.3.1 S6）----

    public ReactiveCommand<RxVoid, RxVoid> NewPluginCommand
    {
        get;
    }

    public ReactiveCommand<RxVoid, RxVoid> EditPluginCommand
    {
        get;
    }

    public ReactiveCommand<RxVoid, RxVoid> DragEnterCommand
    {
        get;
    }

    public ReactiveCommand<RxVoid, RxVoid> DragLeaveCommand
    {
        get;
    }

    public bool IsEmpty => !Plugins.Any();

    // ---- 执行日志（26.3.1 S4）----

    public ObservableCollection<PluginExecutionLogEntry> ExecutionLogs
    {
        get;
    } = [];

    public bool IsLogEmpty => !ExecutionLogs.Any();

    public ReactiveCommand<RxVoid, RxVoid> ClearExecutionLogsCommand
    {
        get;
    }

    public bool IsDragOver
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ReactiveCommand<DragEventArgs?, RxVoid> DropCommand
    {
        get;
    }

    public string? OnlineSearchText
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ObservableCollection<PluginInfo?>? SelectedOnlinePlugins
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    public List<PluginInfo> OnlinePlugins
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public AvaloniaList<PluginInfo> FilteredOnlinePlugins
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ReactiveCommand<RxVoid, RxVoid> RefreshOnlinePluginsCommand
    {
        get;
    }

    public ReactiveCommand<RxVoid, RxVoid> DownloadSelectedPluginsCommand
    {
        get;
    }

    /// <summary>手动检查已安装插件是否有新版本</summary>
    public ReactiveCommand<RxVoid, RxVoid> CheckUpdatesCommand
    {
        get;
    }

    /// <summary>一次性更新所有可更新插件</summary>
    public ReactiveCommand<RxVoid, RxVoid> UpdateAllPluginsCommand
    {
        get;
    }

    /// <summary>「有可用更新」提示条是否展开</summary>
    public bool IsUpdateBarOpen
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>是否存在可更新的已安装插件</summary>
    public bool HasUpdates => Plugins.Any(p => p.UpdateAvailable);

    /// <summary>提示条文案：N 个插件有新版本</summary>
    public string UpdateBarMessage =>
        string.Format(Languages.Text_PluginList_UpdateAvailableFormat, Plugins.Count(p => p.UpdateAvailable));

    /// <summary>在线市场缓存的存活时间，避免每次进入插件页都重新拉取目录</summary>
    private static readonly TimeSpan OnlineCacheTtl = TimeSpan.FromMinutes(10);

    private DateTimeOffset _lastOnlineRefresh = DateTimeOffset.MinValue;
    private bool _autoCheckInFlight;

    public bool IsBusy
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public PluginListViewModel()
    {
        OnlinePlugins = [];
        FilteredOnlinePlugins = [];
        ClearExecutionLogsCommand = ReactiveCommand.Create(() =>
        {
            _pluginService.ClearExecutionLogs();
            ExecutionLogs.Clear();
            this.RaisePropertyChanged(nameof(IsLogEmpty));
        });
        // 加载已有执行日志并订阅新增（UI 线程刷新）
        foreach (var entry in _pluginService.ExecutionLogs)
        {
            ExecutionLogs.Add(entry);
        }
        _pluginService.ExecutionLogAdded += OnExecutionLogAdded;
        ReloadPluginsCommand = ReactiveCommand.Create(ReloadPlugins);
        InstallPluginCommand = ReactiveCommand.CreateFromTask(InstallPluginAsync);
        UninstallPluginCommand = ReactiveCommand.CreateFromTask(UninstallPluginAsync,
            this.WhenAnyValue(x => x.SelectedPlugin, (PluginInfo? p) => p != null));
        TogglePluginCommand = ReactiveCommand.CreateFromTask(TogglePluginAsync,
            this.WhenAnyValue(x => x.SelectedPlugin, (PluginInfo? p) => p != null));
        ViewYamlCommand = ReactiveCommand.CreateFromTask(ViewYamlAsync,
            this.WhenAnyValue(x => x.SelectedPlugin, (PluginInfo? p) => p != null));
        OpenPluginFolderCommand = ReactiveCommand.Create<string>(OpenPluginFolder);
        NewPluginCommand = ReactiveCommand.CreateFromTask(OpenNewPluginEditorAsync);
        EditPluginCommand = ReactiveCommand.CreateFromTask(OpenEditPluginEditorAsync,
            this.WhenAnyValue(x => x.SelectedPlugin, (PluginInfo? p) => p != null));
        DragEnterCommand = ReactiveCommand.Create(() =>
        {
            IsDragOver = true;
        });
        DragLeaveCommand = ReactiveCommand.Create(() =>
        {
            IsDragOver = false;
        });
        DropCommand = ReactiveCommand.Create<DragEventArgs?>(async e =>
        {
            IsDragOver = false;

            // Avalonia 12：拖放数据由 DataTransfer 承载，文件用 TryGetFiles 扩展方法获取。
            var fileList = e?.DataTransfer.TryGetFiles();
            if (fileList == null) return;

            var files = fileList.ToList();
            if (files.Count == 0) return;

            var installed = 0;
            foreach (var path in files.Select(file => file.TryGetLocalPath()))
            {
                if (path == null ||
                    !(Path.GetExtension(path).Equals(".yaml") || Path.GetExtension(path).Equals(".yml") ||
                      Path.GetExtension(path).Equals(".pmls")))
                {
                    return;
                }

                if (_pluginService.InstallPlugin(path))
                {
                    installed++;
                }
            }

            LoadPlugins();
            AppAnalytics.TrackAction("plugin.install.drop", new Dictionary<string, string>
            {
                ["installed"] = installed.ToString()
            });
            Growl.Success(string.Format(Languages.Text_PluginList_InstalledCountFormat, installed));
        });
        RefreshOnlinePluginsCommand = ReactiveCommand.CreateFromTask(RefreshOnlinePluginsAsync);
        DownloadSelectedPluginsCommand = ReactiveCommand.CreateFromTask(
            DownloadSelectedPluginsAsync,
            Observable.CombineLatest(
                    // 选中变化时你在 code-behind 里改集合；再补一个可观察信号更稳
                    this.WhenAnyValue(x => x.SelectedOnlinePlugins)
                        .Select(coll => coll != null), this.WhenAnyValue(x => x.IsBusy),
                    (hasColl, busy) => hasColl && !busy)
        );
        CheckUpdatesCommand = ReactiveCommand.CreateFromTask(CheckForUpdatesAsync,
            this.WhenAnyValue(x => x.IsBusy, busy => !busy));
        UpdateAllPluginsCommand = ReactiveCommand.CreateFromTask(UpdateAllPluginsAsync,
            this.WhenAnyValue(x => x.IsBusy, busy => !busy));
        ObservableExtensions.Subscribe(this.WhenAnyValue(x => x.OnlineSearchText).Throttle(TimeSpan.FromMilliseconds(300)), text =>
        {
            if (string.IsNullOrWhiteSpace(OnlineSearchText))
            {
                if (OnlinePlugins.Count == FilteredOnlinePlugins.Count)
                {
                    return;
                }

                foreach (var pluginInfo in OnlinePlugins.FindAll(p => !FilteredOnlinePlugins.Contains(p)))
                {
                    FilteredOnlinePlugins.Add(pluginInfo);
                }
            }
            else
            {
                var search = text?.ToLowerInvariant();
                FilteredOnlinePlugins.Clear();
                if (search?.StartsWith("/id:") == true)
                {
                    FilteredOnlinePlugins.AddRange(OnlinePlugins.FindAll(x =>
                        x.Id == search[4..] || x.Id.Contains(search[4..]) || search[4..].Contains(x.Id)));
                    return;
                }

                if (search?.StartsWith("/name:") == true)
                {
                    FilteredOnlinePlugins.AddRange(OnlinePlugins.FindAll(x =>
                        x.Name == search[6..] || x.Name.Contains(search[6..]) || search[6..].Contains(x.Name)));
                    return;
                }

                if (search?.StartsWith("/desc:") == true)
                {
                    FilteredOnlinePlugins.AddRange(OnlinePlugins.FindAll(x =>
                        x.Description == search[6..] || x.Description.Contains(search[6..]) ||
                        search[6..].Contains(x.Description)));
                    return;
                }

                if (search?.StartsWith("/author:") == true)
                {
                    FilteredOnlinePlugins.AddRange(OnlinePlugins.FindAll(x =>
                        x.Author == search[8..] || x.Author.Contains(search[8..]) || search[8..].Contains(x.Author)));
                    return;
                }

                foreach (var plugin in OnlinePlugins.FindAll(x => x.Id.Contains(search) || search.Contains(x.Id)
                                 || x.Name.Contains(search) || search.Contains(x.Name) ||
                                 x.Description.Contains(search))
                             .Where(plugin => !FilteredOnlinePlugins.Contains(plugin)))
                {
                    FilteredOnlinePlugins.Add(plugin);
                }
            }
        });

        LoadPlugins();
    }

    /// <summary>手动刷新在线市场：成功后顺带比对本地插件版本，发现新版本时展开提示条。</summary>
    private async Task RefreshOnlinePluginsAsync()
    {
        var ok = await RefreshOnlinePluginsCoreAsync(silent: false).ConfigureAwait(true);
        if (ok)
        {
            RecomputeUpdates();
            if (HasUpdates)
            {
                IsUpdateBarOpen = true;
            }
        }
    }

    /// <summary>
    /// 拉取在线插件目录。silent=true 时失败不弹框、成功不提示（供进入页面时的自动检查使用）。
    /// </summary>
    private async Task<bool> RefreshOnlinePluginsCoreAsync(bool silent)
    {
        IsBusy = true;
        try
        {
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(10);
            http.DefaultRequestHeaders.Add("User-Agent", $"RYCB/PML-2 {Core.App.Version}");

            // 1. 列目录
            var listBody = JsonSerializer.Serialize(new AlistListFileRequestBody
            {
                Path = PluginsApiPath,
                Page = 1,
                PageSize = 100
            }, App.AppJsonSerializerContext.AlistListFileRequestBody);
            using var listReq = new HttpRequestMessage(HttpMethod.Post, $"{AlistBase}/api/fs/list")
            {
                Content = new StringContent(listBody, Encoding.UTF8, "application/json")
            };
            var listJson = await http.SendAsync(listReq).ConfigureAwait(true);
            listJson.EnsureSuccessStatusCode();
            using var listDoc = JsonDocument.Parse(await listJson.Content.ReadAsStringAsync());

            if (listDoc.RootElement.GetProperty("code").GetInt32() != 200)
            {
                if (!silent)
                {
                    Growl.Error(Languages.Text_PluginList_FetchOnlineListFailed);
                }

                return false;
            }

            var content = listDoc.RootElement.GetProperty("data").GetProperty("content");
            var result = new AvaloniaList<PluginInfo>();

            foreach (var item in content.EnumerateArray())
            {
                if (item.GetProperty("is_dir").GetBoolean()) continue;

                var name = item.GetProperty("name").GetString() ?? "";
                var ext = Path.GetExtension(name).ToLowerInvariant();
                if (ext is not (".yaml" or ".yml" or ".pmls")) continue;

                try
                {
                    var info = await FetchOnlinePluginInfoAsync(http, name).ConfigureAwait(true);
                    if (info != null) result.Add(info);
                }
                catch (Exception ex)
                {
                    Core.App.CurrentLogger.Error(ex, $"解析在线插件失败: {name}");
                }
            }

            OnlinePlugins = [.. result];
            FilteredOnlinePlugins = [.. result];
            _lastOnlineRefresh = DateTimeOffset.Now;
            AppAnalytics.TrackAction("plugin.market.refresh", new Dictionary<string, string>
            {
                ["count"] = result.Count.ToString()
            });
            if (!silent)
            {
                Growl.Success(string.Format(Languages.Text_PluginList_OnlineLoadedCountFormat, result.Count));
            }

            return true;
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger.Error(ex, "刷新在线插件失败");
            AppAnalytics.CaptureException(ex, "plugin.market.refresh");
            if (!silent)
            {
                await MessageBoxManager
                    .GetMessageBoxStandard(Languages.Caption_Error, string.Format(Languages.Text_PluginList_RefreshOnlineFailedFormat, ex.Message), ButtonEnum.Ok, Icon.Error)
                    .ShowAsync();
            }

            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 拿下载地址，再读 YAML 元数据
    /// </summary>
    private async Task<PluginInfo?> FetchOnlinePluginInfoAsync(HttpClient http, string fileName)
    {
        var path = $"{PluginsApiPath}/{fileName}";

        var yaml = await http.GetStringAsync("https://alist.yealqp.cn/download/" + path)
            .ConfigureAwait(true);

        var pi = PluginService.Instance.ExtractPluginInfoFromContent(yaml);
        return pi;
    }

    // ===================== 插件更新 =====================

    /// <summary>从配置读取自动更新模式（0=仅手动 1=自动检查并提示 2=静默自动更新）</summary>
    private static int ReadAutoUpdateMode()
    {
        try
        {
            return ConfigManager.CurrentConfig.PluginAutoUpdateMode;
        }
        catch
        {
            // 配置尚不可用时按默认（自动检查并提示）处理
            return 1;
        }
    }

    /// <summary>在线版本是否比已安装版本新；版本号无法解析时退化为「字符串不同即视为有更新」。</summary>
    private static bool IsNewerVersion(string? online, string? local)
    {
        if (string.IsNullOrWhiteSpace(online))
        {
            return false;
        }

        var l = string.IsNullOrWhiteSpace(local) ? "1.0" : local;
        try
        {
            return VersionComparer.IsGreaterThan(online, l);
        }
        catch
        {
            return !string.Equals(online, l, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// 把本地已安装插件与在线市场条目按 Id 比对，标记 UpdateAvailable / LatestVersion。
    /// 因为 PluginInfo 不实现 INotifyPropertyChanged，标记后重建列表以刷新绑定。
    /// </summary>
    private void RecomputeUpdates()
    {
        ApplyUpdateFlags();
        ReloadLocalList();
        this.RaisePropertyChanged(nameof(HasUpdates));
        this.RaisePropertyChanged(nameof(UpdateBarMessage));
        if (!HasUpdates)
        {
            IsUpdateBarOpen = false;
        }
    }

    /// <summary>按在线市场比对结果给已安装插件打标记（不重建列表、不触发通知）。</summary>
    private void ApplyUpdateFlags()
    {
        var onlineById = new Dictionary<string, PluginInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var o in OnlinePlugins)
        {
            if (!string.IsNullOrEmpty(o.Id))
            {
                onlineById[o.Id] = o;
            }
        }

        foreach (var p in _pluginService.Plugins)
        {
            p.UpdateAvailable = false;
            p.LatestVersion = null;
            if (onlineById.TryGetValue(p.Id, out var online))
            {
                p.LatestVersion = online.Version;
                p.UpdateAvailable = IsNewerVersion(online.Version, p.Version);
            }
        }
    }

    /// <summary>确保在线插件目录已加载（未过期时复用缓存），供自动检查使用。</summary>
    private async Task<bool> EnsureOnlinePluginsAsync(bool force)
    {
        if (!force && OnlinePlugins.Count > 0 && DateTimeOffset.Now - _lastOnlineRefresh < OnlineCacheTtl)
        {
            return true;
        }

        return await RefreshOnlinePluginsCoreAsync(silent: true).ConfigureAwait(true);
    }

    /// <summary>
    /// 进入插件页时调用：按配置自动检查更新；静默模式（2）下直接更新，其余模式只展开提示条。
    /// 任何失败都安静吞掉，不影响插件页正常使用。
    /// </summary>
    public async Task AutoCheckForUpdatesAsync()
    {
        if (_autoCheckInFlight)
        {
            return;
        }

        var mode = ReadAutoUpdateMode();
        if (mode <= 0)
        {
            return;
        }

        _autoCheckInFlight = true;
        try
        {
            if (!await EnsureOnlinePluginsAsync(force: false).ConfigureAwait(true))
            {
                return;
            }

            RecomputeUpdates();
            if (!HasUpdates)
            {
                return;
            }

            if (mode >= 2)
            {
                var updated = await UpdatePluginsCoreAsync([.. Plugins.Where(p => p.UpdateAvailable)]).ConfigureAwait(true);
                if (updated > 0)
                {
                    Growl.Success(string.Format(Languages.Text_PluginList_AutoUpdatedCountFormat, updated));
                }
            }
            else
            {
                IsUpdateBarOpen = true;
            }
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger.Warning($"自动检查插件更新失败: {ex.Message}");
        }
        finally
        {
            _autoCheckInFlight = false;
        }
    }

    /// <summary>手动检查更新</summary>
    private async Task CheckForUpdatesAsync()
    {
        var ok = await RefreshOnlinePluginsCoreAsync(silent: false).ConfigureAwait(true);
        if (!ok)
        {
            return;
        }

        RecomputeUpdates();
        if (HasUpdates)
        {
            IsUpdateBarOpen = true;
            Growl.Success(string.Format(Languages.Text_PluginList_UpdateAvailableFormat, Plugins.Count(p => p.UpdateAvailable)));
        }
        else
        {
            Growl.Success(Languages.Text_PluginList_UpToDate);
        }
    }

    /// <summary>更新全部可更新插件</summary>
    private async Task UpdateAllPluginsAsync()
    {
        var targets = Plugins.Where(p => p.UpdateAvailable).ToList();
        if (targets.Count == 0)
        {
            return;
        }

        var updated = await UpdatePluginsCoreAsync(targets).ConfigureAwait(true);
        if (updated > 0)
        {
            Growl.Success(string.Format(Languages.Text_PluginList_UpdatedCountFormat, updated));
        }

        if (Plugins.Any(p => p.UpdateAvailable))
        {
            await MessageBoxManager
                .GetMessageBoxStandard(Languages.Caption_Error, Languages.Text_PluginList_UpdateFailed, ButtonEnum.Ok, Icon.Error)
                .ShowAsync();
        }
    }

    /// <summary>下载并就地覆盖指定插件；返回成功更新的数量。结束时统一重载并重新比对。</summary>
    private async Task<int> UpdatePluginsCoreAsync(List<PluginInfo> targets)
    {
        IsBusy = true;
        var updated = 0;
        try
        {
            var onlineById = new Dictionary<string, PluginInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var o in OnlinePlugins)
            {
                if (!string.IsNullOrEmpty(o.Id))
                {
                    onlineById[o.Id] = o;
                }
            }

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            http.DefaultRequestHeaders.Add("User-Agent", $"RYCB/PML-2 {Core.App.Version}");

            // 批量写入期间关闭文件监听，避免热重载在写一半时重载所有插件
            _pluginService.DisableHotReload();
            foreach (var plugin in targets)
            {
                if (!onlineById.TryGetValue(plugin.Id, out var online))
                {
                    continue;
                }

                var fileName = online.Id.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
                               online.Id.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
                    ? online.Id
                    : $"{online.Id}.yaml";

                try
                {
                    var content = await http
                        .GetStringAsync($"https://alist.yealqp.cn/download/{PluginsApiPath}/{fileName}")
                        .ConfigureAwait(true);
                    if (_pluginService.UpdatePluginContent(plugin.Id, content))
                    {
                        updated++;
                    }
                }
                catch (Exception ex)
                {
                    Core.App.CurrentLogger.Error(ex, $"下载/更新插件失败: {plugin.Id}");
                }
            }
        }
        finally
        {
            // ReloadPlugins 会停止并重新启动热重载监听
            _pluginService.ReloadPlugins();
            LoadPlugins();
            RecomputeUpdates();
            IsBusy = false;
        }

        AppAnalytics.TrackAction("plugin.update.batch", new Dictionary<string, string>
        {
            ["targets"] = targets.Count.ToString(),
            ["updated"] = updated.ToString()
        });

        return updated;
    }

    private async Task DownloadSelectedPluginsAsync()
    {
        if (SelectedOnlinePlugins == null || SelectedOnlinePlugins.Count == 0) return;

        try
        {
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(60);
            var installed = 0;
            foreach (var plugin in SelectedOnlinePlugins.Where(p => p != null)!)
            {
                var fileName = plugin!.Id.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)
                    ? plugin.Id
                    : $"{plugin.Id}.yaml";

                var bytes = await http
                    .GetByteArrayAsync($"https://alist.yealqp.cn/download/{PluginsApiPath}/{fileName}")
                    .ConfigureAwait(true);
                // 必须写到系统临时目录：若直接写到插件目录，InstallPlugin 内部 File.Copy 会因
                // 源/目标为同一路径抛 IOException，导致永远安装 0 个插件。
                var tmp = Path.Combine(Path.GetTempPath(), fileName);
                await File.WriteAllBytesAsync(tmp, bytes).ConfigureAwait(true);

                _pluginService.DisableHotReload();
                if (_pluginService.InstallPlugin(tmp, false, true))
                    installed++;

                try
                {
                    File.Delete(tmp);
                }
                catch
                {
                    /* ignore */
                }
            }

            LoadPlugins();
            AppAnalytics.TrackAction("plugin.market.download", new Dictionary<string, string>
            {
                ["selected"] = SelectedOnlinePlugins.Count.ToString(),
                ["installed"] = installed.ToString()
            });
            Growl.Success(string.Format(Languages.Text_PluginList_InstalledOnlineCountFormat, installed));
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger.Error(ex, "下载在线插件失败");
            AppAnalytics.CaptureException(ex, "plugin.market.download");
            await MessageBoxManager
                .GetMessageBoxStandard(Languages.Caption_Error, string.Format(Languages.Text_PluginList_DownloadInstallFailedFormat, ex.Message), ButtonEnum.Ok, Icon.Error)
                .ShowAsync();
        }
    }

    /// <summary>打开表单编辑器：新建插件</summary>
    private async Task OpenNewPluginEditorAsync()
    {
        var vm = new PluginEditor.PluginEditorViewModel();
        await ShowEditorDialogAsync(vm);
    }

    /// <summary>打开表单编辑器：编辑选中插件；无法用表单打开时提示并打开插件目录</summary>
    private async Task OpenEditPluginEditorAsync()
    {
        var selected = SelectedPlugin;
        if (selected == null)
        {
            return;
        }

        var yaml = _pluginService.GetPluginYaml(selected.Id);
        if (yaml == null)
        {
            Growl.Warning(Languages.Text_PluginList_CannotOpenInForm);
            OpenPluginFolder("plugins");
            return;
        }

        // 表单编辑器仅支持事件插件；create-proxy-template 等资源型插件需保留 templates 段，直接编辑 YAML
        if (!string.Equals(selected.Type, "event", StringComparison.OrdinalIgnoreCase))
        {
            await MessageBoxManager
                .GetMessageBoxStandard(Languages.Caption_Hint,
                    string.Format(Languages.Text_PluginEditor_TypeNotSupported, selected.Type),
                    ButtonEnum.Ok, Icon.Warning)
                .ShowAsync();
            OpenPluginFolder("plugins");
            return;
        }

        try
        {
            var vm = new PluginEditor.PluginEditorViewModel(yaml);
            await ShowEditorDialogAsync(vm);
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger.Warning($"无法用表单打开插件 {selected.Id}: {ex.Message}");
            Growl.Warning(Languages.Text_PluginList_CannotOpenInForm);
            OpenPluginFolder("plugins");
        }
    }

    private async Task ShowEditorDialogAsync(PluginEditor.PluginEditorViewModel vm)
    {
        var window = new PluginEditorWindow(vm);
        await window.ShowDialog(Core.App.MainWindow);
        // 保存后刷新列表（热重载由文件监听触发，此处同步一次保证列表即时一致）
        LoadPlugins();
    }

    /// <summary>执行日志新增回调（引擎线程触发，回 UI 线程追加）</summary>
    private void OnExecutionLogAdded(PluginExecutionLogEntry entry)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ExecutionLogs.Insert(0, entry);
            while (ExecutionLogs.Count > 200) ExecutionLogs.RemoveAt(ExecutionLogs.Count - 1);
            this.RaisePropertyChanged(nameof(IsLogEmpty));
        });
    }

    private void LoadPlugins()
    {
        try
        {
            if (!_pluginService.IsLoaded)
                _pluginService.LoadPlugins();

            ApplyUpdateFlags();
            ReloadLocalList();
            this.RaisePropertyChanged(nameof(HasUpdates));
            this.RaisePropertyChanged(nameof(UpdateBarMessage));
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger.Error(ex, "加载插件列表失败");
        }
    }

    /// <summary>用服务层的最新插件集合重建可见列表（保留原先选中的插件）。</summary>
    private void ReloadLocalList()
    {
        var tmpSelected = SelectedPlugin;
        Plugins.Clear();
        foreach (var plugin in _pluginService.Plugins)
        {
            Plugins.Add(plugin);
        }

        this.RaisePropertyChanged(nameof(IsEmpty));

        try
        {
            SelectedPlugin = tmpSelected is not null && Plugins.Contains(tmpSelected) ? tmpSelected : null;
        }
        catch
        {
            // 忽略选择恢复失败
        }
    }

    private void ReloadPlugins()
    {
        _pluginService.ReloadPlugins();
        LoadPlugins();
        AppAnalytics.TrackAction("plugin.list.reload");
        Growl.Success(Languages.Text_PluginList_ListRefreshed);
    }

    private async Task InstallPluginAsync()
    {
        try
        {
            var files = await MainWindow.Instance.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = true,
                FileTypeFilter =
                [
                    new FilePickerFileType(Languages.Text_PluginList_YamlPluginFiles)
                    {
                        Patterns = ["*.yaml", "*.yml"]
                    }
                ],
                Title = Languages.Text_PluginList_InstallPluginTitle,
                SuggestedStartLocation =
                    await MainWindow.Instance.StorageProvider.TryGetFolderFromPathAsync(
                        Environment.GetFolderPath(Environment.SpecialFolder.Desktop))
            });

            if (files.Count == 0) return;

            var installed = files.Count(file => _pluginService.InstallPlugin(file.TryGetLocalPath()));

            LoadPlugins();
            AppAnalytics.TrackAction("plugin.install.local", new Dictionary<string, string>
            {
                ["selected"] = files.Count.ToString(),
                ["installed"] = installed.ToString()
            });
            Growl.Success(string.Format(Languages.Text_PluginList_InstalledCountFormat, installed));
        }
        catch (Exception ex)
        {
            AppAnalytics.CaptureException(ex, "plugin.install.local");
            await MessageBoxManager
                .GetMessageBoxStandard(Languages.Caption_Error, string.Format(Languages.Text_PluginList_InstallFailedFormat, ex.Message), ButtonEnum.Ok, Icon.Error)
                .ShowAsync();
        }
    }

    private async Task UninstallPluginAsync()
    {
        if (SelectedPlugin == null) return;

        var result = await MessageBoxManager
            .GetMessageBoxStandard(Languages.Text_PluginList_ConfirmUninstallCaption,
                string.Format(Languages.Text_PluginList_UninstallConfirmFormat, SelectedPlugin.Name),
                ButtonEnum.YesNo, Icon.Question)
            .ShowAsync();

        if (result != ButtonResult.Yes) return;

        try
        {
            if (_pluginService.UninstallPlugin(SelectedPlugin.Id))
            {
                Plugins.Remove(SelectedPlugin);
                SelectedPlugin = null;
                Growl.Success(Languages.Text_PluginList_Uninstalled);
            }
        }
        catch (Exception ex)
        {
            AppAnalytics.CaptureException(ex, "plugin.uninstall.ui");
            await MessageBoxManager
                .GetMessageBoxStandard(Languages.Caption_Error, string.Format(Languages.Text_PluginList_UninstallFailedFormat, ex.Message), ButtonEnum.Ok, Icon.Error)
                .ShowAsync();
        }
    }

    private async Task TogglePluginAsync()
    {
        if (SelectedPlugin == null) return;

        if (SelectedPlugin.IsEnabled)
            _pluginService.DisablePlugin(SelectedPlugin.Id);
        else
            _pluginService.EnablePlugin(SelectedPlugin.Id);

        // 刷新列表中该项的 IsEnabled 状态
        LoadPlugins();
    }

    private async Task ViewYamlAsync()
    {
        if (SelectedPlugin == null) return;

        try
        {
            var yaml = _pluginService.GetPluginYaml(SelectedPlugin.Id);
            if (yaml == null)
            {
                await MessageBoxManager
                    .GetMessageBoxStandard(Languages.Caption_Hint, Languages.Text_PluginList_CannotReadPluginContent, ButtonEnum.Ok, Icon.Warning)
                    .ShowAsync();
                return;
            }

            var viewer = new FAContentDialog
            {
                Title = string.Format(Languages.Text_PluginList_PluginSourceTitleFormat, SelectedPlugin.Name),
                Content = new ScrollViewer
                {
                    Content = new TextBlock
                    {
                        Text = yaml,
                        FontFamily = "Cascadia Code, Consolas, monospace",
                        FontSize = 13,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    }
                },
                PrimaryButtonText = Languages.Text_Global_Close,
                MaxWidth = 800,
                MaxHeight = 600
            };

            await viewer.ShowAsync();
        }
        catch (Exception ex)
        {
            await MessageBoxManager
                .GetMessageBoxStandard(Languages.Caption_Error, string.Format(Languages.Text_PluginList_ReadYamlFailedFormat, ex.Message), ButtonEnum.Ok, Icon.Error)
                .ShowAsync();
        }
    }

    private void OpenPluginFolder(string? folderType)
    {
        var folder = folderType switch
        {
            "plugins" => _pluginService.PluginsFolder,
            _ => _pluginService.PluginsFolder
        };

        try
        {
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger.Error(ex, "打开插件文件夹失败");
        }
    }
}

public class AlistListFileRequestBody
{
    [JsonPropertyName("path")]
    public string? Path
    {
        get;
        set;
    }

    [JsonPropertyName("page")]
    public int Page
    {
        get;
        set;
    }

    [JsonPropertyName("per_page")]
    public int PageSize
    {
        get;
        set;
    }
}
