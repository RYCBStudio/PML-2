using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.ViewModels;

namespace MEFrpLauncherX.Controls;

public partial class ProxySSLSettings : UserControl
{
    private UserProxyViewModel _proxy;

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

    public ProxySSLSettings(UserProxyViewModel vm)
    {
        InitializeComponent();
        _proxy = vm;
    }

    private async void OpenFile(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn)
        {
            return;
        }

        IReadOnlyList<FilePickerFileType> filterForCfg =
        [
            new("TOML文件") { Patterns = ["*.toml"] }, new("JSON文件") { Patterns = ["*.json"] },
            new("INI文件") { Patterns = ["*.ini"] }, new("YAML文件") { Patterns = ["*.yaml", "*.yml"] },
            FilePickerFileTypes.All
        ];
        IReadOnlyList<FilePickerFileType> filterForCert =
        [
            new("证书文件") { Patterns = ["*.crt", "*.pem"] },
            FilePickerFileTypes.All
        ];
        IReadOnlyList<FilePickerFileType> filterForKey =
        [
            new("私钥文件") { Patterns = ["*.key", "*.pem"] },
            FilePickerFileTypes.All
        ];
        var cfg = await Core.App.MainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "请选择文件",
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