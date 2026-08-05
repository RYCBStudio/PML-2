using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using FluentAvalonia.UI.Controls;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Services;
using MEFrpLauncherX.Views;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using ReactiveUI;

namespace MEFrpLauncherX.ViewModels;

public class PluginListViewModel : ViewModelBase
{
    private readonly PluginService _pluginService = PluginService.Instance;

    public ObservableCollection<PluginInfo> Plugins
    {
        get;
    } = [];

    public PluginInfo? SelectedPlugin
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsLoaded
    {
        get => _pluginService.IsLoaded;
    }

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

    public ReactiveCommand<Unit, Unit> DragEnterCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> DragLeaveCommand
    {
        get;
    }

    public bool IsEmpty => !Plugins.Any();

    public bool IsDragOver
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ReactiveCommand<DragEventArgs?, Unit> DropCommand
    {
        get;
    }

    public PluginListViewModel()
    {
        ReloadPluginsCommand = ReactiveCommand.Create(ReloadPlugins);
        InstallPluginCommand = ReactiveCommand.CreateFromTask(InstallPluginAsync);
        UninstallPluginCommand = ReactiveCommand.CreateFromTask(UninstallPluginAsync,
            this.WhenAnyValue(x => x.SelectedPlugin, (PluginInfo? p) => p != null));
        TogglePluginCommand = ReactiveCommand.CreateFromTask(TogglePluginAsync,
            this.WhenAnyValue(x => x.SelectedPlugin, (PluginInfo? p) => p != null));
        ViewYamlCommand = ReactiveCommand.CreateFromTask(ViewYamlAsync,
            this.WhenAnyValue(x => x.SelectedPlugin, (PluginInfo? p) => p != null));
        OpenPluginFolderCommand = ReactiveCommand.Create<string>(OpenPluginFolder);
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
            if (e?.Data == null) return;

            // Avalonia 拖放文件通过 DataFormats.Files 获取
            var fileList = e.Data.GetFiles();
            if (fileList == null) return;

            var files = fileList.ToList();
            if (files.Count == 0) return;

            var installed = 0;
            foreach (var file in files)
            {
                var path = file.TryGetLocalPath();
                if (path == null) continue;
                if (_pluginService.InstallPlugin(path))
                    installed++;
            }

            LoadPlugins();
            Growl.Success($"成功安装 {installed} 个插件");
        });

        LoadPlugins();
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

            try
            {
                SelectedPlugin = tmpSelected;
            }catch{}
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
        Growl.Success("插件列表已刷新");
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
                    new FilePickerFileType("YAML 插件文件")
                    {
                        Patterns = ["*.yaml", "*.yml"]
                    }
                ],
                Title = "安装插件",
                SuggestedStartLocation =
                    await MainWindow.Instance.StorageProvider.TryGetFolderFromPathAsync(
                        Environment.GetFolderPath(Environment.SpecialFolder.Desktop))
            });

            if (files.Count == 0) return;

            var installed = files.Count(file => _pluginService.InstallPlugin(file.TryGetLocalPath()));

            LoadPlugins();
            Growl.Success($"成功安装 {installed} 个插件");
        }
        catch (Exception ex)
        {
            await MessageBoxManager
                .GetMessageBoxStandard("错误", $"安装插件失败: {ex.Message}", ButtonEnum.Ok, Icon.Error)
                .ShowAsync();
        }
    }

    private async Task UninstallPluginAsync()
    {
        if (SelectedPlugin == null) return;

        var result = await MessageBoxManager
            .GetMessageBoxStandard("确认卸载",
                $"确定要卸载插件 '{SelectedPlugin.Name}' 吗？\n\n此操作将删除插件文件，不可恢复。",
                ButtonEnum.YesNo, Icon.Question)
            .ShowAsync();

        if (result != ButtonResult.Yes) return;

        try
        {
            if (_pluginService.UninstallPlugin(SelectedPlugin.Id))
            {
                Plugins.Remove(SelectedPlugin);
                SelectedPlugin = null;
                Growl.Success("插件已卸载");
            }
        }
        catch (Exception ex)
        {
            await MessageBoxManager
                .GetMessageBoxStandard("错误", $"卸载插件失败: {ex.Message}", ButtonEnum.Ok, Icon.Error)
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
                    .GetMessageBoxStandard("提示", "无法读取插件文件内容。", ButtonEnum.Ok, Icon.Warning)
                    .ShowAsync();
                return;
            }

            var viewer = new ContentDialog
            {
                Title = $"插件源码: {SelectedPlugin.Name}",
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
                PrimaryButtonText = "关闭",
                MaxWidth = 800,
                MaxHeight = 600
            };

            await viewer.ShowAsync();
        }
        catch (Exception ex)
        {
            await MessageBoxManager
                .GetMessageBoxStandard("错误", $"读取插件 YAML 失败: {ex.Message}", ButtonEnum.Ok, Icon.Error)
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