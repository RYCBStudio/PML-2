using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.ViewModels;

namespace MEFrpLauncherX.Views;

public partial class ALPSettings : Window
{
    public ALPSettings()
    {
        InitializeComponent();
        DataContext = new ALPSettingsViewModel();
        SearchBox.ItemsSource = new List<string> { "/pid:", "/nid:", "/n:" }.OrderBy(x => x);
        SearchBox1.ItemsSource = new List<string> { "/pid:", "/nid:", "/n:" }.OrderBy(x => x);
    }

    private void SaveSettings(object? sender, RoutedEventArgs e)
    {
        ConfigManager.UpdateConfig(cfg =>
            cfg.AutoLaunchProxies.Clear());
        ConfigManager.UpdateConfig(cfg =>
            cfg.AutoLaunchProxies.AddRange(AutoLaunchList.Items.Cast<UserProxyViewModel>().Select(proxy =>
                    new ALPConfig
                    {
                        Name = proxy.proxyName, Id = proxy.proxyId, UseConfig = proxy.UseConfig, Config = proxy.Config
                    })
                .ToList()));
        Close();
        Growl.Success(Languages.Text_ALPSettings_SaveSuccess);
    }

    private void Close(object? sender, RoutedEventArgs e) => Close();
}