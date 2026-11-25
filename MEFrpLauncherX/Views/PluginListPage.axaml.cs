using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using MEFrpLauncherX.Plugin.Services;
using MEFrpLauncherX.ViewModels;

namespace MEFrpLauncherX.Views;

public partial class PluginListPage : UserControl
{
    public PluginListPage()
    {
        InitializeComponent();
        DataContext = new PluginListViewModel();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // 按「插件自动更新」设置静默检查/更新；内部已做缓存与并发保护，失败不影响页面
        if (DataContext is PluginListViewModel vm)
        {
            _ = vm.AutoCheckForUpdatesAsync();
        }
    }

    private void OnlinePluginsList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not PluginListViewModel vm) return;
        if (sender is not ListBox list) return;

        vm.SelectedOnlinePlugins ??= [];
        vm.SelectedOnlinePlugins.Clear();

        foreach (var item in list.SelectedItems ?? new List<object>())
        {
            if (item is PluginInfo p)
                vm.SelectedOnlinePlugins.Add(p);
        }
    }
}