using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.ViewModels;

namespace MEFrpLauncherX.Controls;

public partial class ProxySSLSettings : UserControl
{
    private UserProxyViewModel _proxy;

    public ProxySSLSettings(UserProxyViewModel vm)
    {
        InitializeComponent();
        _proxy = vm;
    }

    public bool Finished
    {
        get;
        private set;
    }

    public string Config
    {
        get;
        private set;
    }

    public string CertFile
    {
        get;
        private set;
    }

    public string KeyFile
    {
        get;
        private set;
    }

    private async void OpenFile(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn)
        {
            return;
        }

        IReadOnlyList<FilePickerFileType> filterForCfg =
        [
            new(Languages.Text_ConfigPreviewer_TomlFile) { Patterns = ["*.toml"] }, new(Languages.Text_ConfigPreviewer_JsonFile) { Patterns = ["*.json"] },
            new(Languages.Text_ConfigPreviewer_IniFile) { Patterns = ["*.ini"] }, new(Languages.Text_ConfigPreviewer_YamlFile) { Patterns = ["*.yaml", "*.yml"] },
            FilePickerFileTypes.All
        ];
        IReadOnlyList<FilePickerFileType> filterForCert =
        [
            new(Languages.Text_ProxySSL_CertFiles) { Patterns = ["*.crt", "*.pem"] },
            FilePickerFileTypes.All
        ];
        IReadOnlyList<FilePickerFileType> filterForKey =
        [
            new(Languages.Text_ProxySSL_KeyFiles) { Patterns = ["*.key", "*.pem"] },
            FilePickerFileTypes.All
        ];
        var cfg = await Core.App.MainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Languages.Text_ProxySSL_SelectFileTitle,
            AllowMultiple = false,
            FileTypeFilter = btn.Name switch
            {
                "ProxyCfg" => filterForCfg,
                "CertFileB" => filterForCert,
                "KeyFileB" => filterForKey,
                _ => [FilePickerFileTypes.All]
            }
        });
        if (cfg is null || cfg?.Count <= 0)
        {
            return;
        }

        var file = cfg?[0].Path.AbsolutePath;
        switch (btn.Name)
        {
            case "ProxyCfg":
                ProxyCfgBox.Text = file;
                break;
            case "CertFileB":
                CertFileBox.Text = file;
                break;
            case "KeyFileB":
                KeyFileBox.Text = file;
                break;
        }
    }

    public bool Check()
    {
        return Config.IsNullOrEmpty() && CertFile.IsNullOrEmpty() && KeyFile.IsNullOrEmpty() &&
               !ProxyNameBox.Text.IsNullOrEmpty() && LocalIpBox.Text.IsNullOrEmpty() && DomainBox.Text.IsNullOrEmpty();
    }

    public Dictionary<string, string> GetSSlConfig()
    {
        return new Dictionary<string, string>
        {
            { "cert", CertFile },
            { "key", KeyFile },
            { "cfg", Config },
            { "localIp", LocalIpBox.Text },
            { "domain", DomainBox.Text },
            { "name", ProxyNameBox.Text }
        };
    }
}