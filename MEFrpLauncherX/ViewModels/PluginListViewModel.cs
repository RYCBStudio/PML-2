using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive;
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

    public ReactiveCommand<Unit, Unit> ReloadPluginsCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> InstallPluginCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> UninstallPluginCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> TogglePluginCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> ViewYamlCommand
    {
        get;
    }

    public ReactiveCommand<string, Unit> OpenPluginFolderCommand
    {
        get;
    }

    // ---- 表单编辑器入口（26.3.1 S6）----

    public ReactiveCommand<Unit, Unit> NewPluginCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> EditPluginCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> DragEnterCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> DragLeaveCommand
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

    public ReactiveCommand<Unit, Unit> ClearExecutionLogsCommand
    {
        get;
    }

    public bool IsDragOver
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ReactiveCommand<DragEventArgs?, Unit> DropCommand
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

    public ReactiveCommand<Unit, Unit> RefreshOnlinePluginsCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> DownloadSelectedPluginsCommand
    {
        get;
    }

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

            // Avalonia 拖放文件通过 DataFormats.Files 获取
            var fileList = e?.Data?.GetFiles();
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
            this.WhenAnyValue(x => x.SelectedOnlinePlugins)
                .Select(coll => coll != null)
                .CombineLatest(
                    // 选中变化时你在 code-behind 里改集合；再补一个可观察信号更稳
                    this.WhenAnyValue(x => x.IsBusy),
                    (hasColl, busy) => hasColl && !busy)
        );
        this.WhenAnyValue(x => x.OnlineSearchText).Throttle(TimeSpan.FromMilliseconds(300)).Subscribe(text =>
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

    private async Task RefreshOnlinePluginsAsync()
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
                Growl.Error(Languages.Text_PluginList_FetchOnlineListFailed);
                return;
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
            AppAnalytics.TrackAction("plugin.market.refresh", new Dictionary<string, string>
            {
                ["count"] = result.Count.ToString()
            });
            Growl.Success(string.Format(Languages.Text_PluginList_OnlineLoadedCountFormat, result.Count));
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger.Error(ex, "刷新在线插件失败");
            AppAnalytics.CaptureException(ex, "plugin.market.refresh");
            await MessageBoxManager
                .GetMessageBoxStandard(Languages.Caption_Error, string.Format(Languages.Text_PluginList_RefreshOnlineFailedFormat, ex.Message), ButtonEnum.Ok, Icon.Error)
                .ShowAsync();
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
            var tmpSelected = SelectedPlugin;
            if (!_pluginService.IsLoaded)
                _pluginService.LoadPlugins();

            Plugins.Clear();
            foreach (var plugin in _pluginService.Plugins)
            {
                Plugins.Add(plugin);
            }
            this.RaisePropertyChanged(nameof(IsEmpty));

            try
            {
                SelectedPlugin = tmpSelected;
            }
            catch
            {
            }
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger.Error(ex, "加载插件列表失败");
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

            var viewer = new ContentDialog
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