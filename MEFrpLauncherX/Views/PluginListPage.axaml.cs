using System.Collections.Generic;
using Avalonia.Controls;
using MEFrpLauncherX.Services;
using MEFrpLauncherX.ViewModels;

namespace MEFrpLauncherX.Views;

public partial class PluginListPage : UserControl
{
    public PluginListPage()
    {
        InitializeComponent();
        DataContext = new PluginListViewModel();
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