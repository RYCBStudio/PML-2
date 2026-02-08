using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using MEFrpLauncherX.Controls;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.MEFIntergrated;
using MEFrpLauncherX.ViewModels;
using Newtonsoft.Json;

namespace MEFrpLauncherX.Views;

public partial class EditProxyWindow : Window
{
    private readonly UserProxyViewModel _proxy;
    private readonly CreateProxyViewModel _createProxyViewModel = new();

    public EditProxyWindow(UserProxyViewModel pr)
    {
        InitializeComponent();
        _proxy = pr;
        ProxyNameBox.Text = pr.proxyName;
        LocalAddressBox.Text = pr.localIp;
        LocalPortNudBox.Value = pr.localPort;
        RemotePortNudBox.Value = pr.remotePort;
        ProxyProtocolCbBox.SelectedIndex = pr.proxyProtocolVersion switch
        {
            "v1" => 1,
            "v2" => 2,
            _ => 0
        };
        HostHeaderRewriteBox.Text = pr.hostHeaderRewrite;
        EnableCompressCBox.IsChecked = pr.useCompression;
        EnableCryptoCBox.IsChecked = pr.useEncryption;
        ProtocolCbBox.ItemsSource = pr.allowedProtocols;
        ProtocolCbBox.SelectedIndex = pr.proxyType.ToLower() switch
        {
            "tcp" => 0,
            "udp" => 1,
            "http" => 2,
            "https" => 3,
        };
        SecurityOptionsSelect.SelectedIndex = GetSecurityOptions();
        _createProxyViewModel.RemoteAddress.AddRange(_proxy.Domains);
    }

    private int GetSecurityOptions()
    {
        if (_proxy.accessKey.IsNullOrEmpty())
        {
            //TODO: 等官网API更新
            return 0;
        }
        else
        {
            return 1;
        }
    }

    private async void EditHeaders(object? sender, RoutedEventArgs e)
    {
        var he = new HeadersEdit();
        if (_createProxyViewModel.RequestHeaders is not null && _createProxyViewModel.RequestHeaders.Count != 0)
            he.Headers.AddRange(_createProxyViewModel.RequestHeaders.Select(kv =>
                {
                    var key = kv.Keys.FirstOrDefault();
                    var val = kv.Values.FirstOrDefault();
                    if (key is null || val is null)
                    {
                        return new RequestHeader
                        {
                            Name = "NOTFOUND",
                            Value = "NOTFOUND"
                        };
                    }

                    return new RequestHeader
                    {
                        Name = key!,
                        Value = val!
                    };
                })
                .ToList());
        // foreach (var header in he.Headers.Where(header => header.Name == "NOTFOUND"))
        // {
        //     he.Headers.Remove(header);
        // }
        for (var i = 0; i < he.Headers.Count; i++)
        {
            if (he.Headers[i].Name != "NOTFOUND")
            {
                continue;
            }

            he.Headers.RemoveAt(i);
            i--;
        }
        var cd = new ContentDialog()
        {
            Title = "编辑请求头",
            Content = he,
            PrimaryButtonText = "确定",
            DefaultButton = ContentDialogButton.Primary,
            IsSecondaryButtonEnabled = false,
            CloseButtonText = "取消"
        };
        var res = await cd.ShowAsync();
        if (res == ContentDialogResult.Primary)
        {
            // _createProxyViewModel.RequestHeaders.AddRange(he.Headers
            //     .ToDictionary(header => header.Name, header => header.Value));
        }
    }
    
    private async void EditDomains(object? sender, RoutedEventArgs e)
    {
        var de = new DomainsEdit();
        if (_proxy.Domains is not null && _proxy.Domains.Count != 0)
            de.Domains.AddRange(_proxy.Domains);
        var cd = new ContentDialog()
        {
            Title = "编辑绑定域名",
            Content = de,
            PrimaryButtonText = "确定",
            DefaultButton = ContentDialogButton.Primary,
            IsSecondaryButtonEnabled = false,
            CloseButtonText = "取消"
        };
        var res = await cd.ShowAsync();
        if (res == ContentDialogResult.Primary)
        {
            _createProxyViewModel.RemoteAddress.Clear();
            _createProxyViewModel.RemoteAddress.AddRange(de.Domains);
        }
    }
    
    private string GetHttpPlugin()
    {
        return ProtocolCbBox.SelectedIndex switch
        {
            2 when SameAsHttp.IsChecked == true => string.Empty,
            2 when Http2Https.IsChecked == true => "http2https",
            2 => string.Empty,
            3 when SameAsHttps.IsChecked == true => "https2https",
            3 when Https2Http.IsChecked == true => "https2http",
            _ => string.Empty
        };
    }

    private async void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        var requestData = new
        {
            _proxy.proxyId,
            proxyName = ProxyNameBox.Text,
            localIp = LocalAddressBox.Text,
            localPort = Convert.ToInt32(LocalPortNudBox.Value),
            remotePort = Convert.ToInt32(RemotePortNudBox.Value),
            proxyType = ProtocolCbBox.SelectedItem?.ToString()?.ToLower() ?? string.Empty,
            hostHeaderRewrite = HostHeaderRewriteBox.Text,
            domain = "[\"" + string.Join("\", \"", _createProxyViewModel.RemoteAddress) + "\"]",
            accessKey = SecurityOptionsSelect.SelectedIndex == 2 ? AccessKeyBox.Text : string.Empty,
            httpPlugin = GetHttpPlugin(),
            httpUser = SecurityOptionsSelect.SelectedIndex == 1 ? HTTPBasicAuthNameBox.Text : string.Empty,
            httpPassword = SecurityOptionsSelect.SelectedIndex == 1 ? HTTPBasicAuthPwdBox.Text : string.Empty,
            proxyProtocolVersion = ProxyProtocolCbBox.SelectionBoxItem.ToString().Contains("不启用")
                ? string.Empty
                : ProxyProtocolCbBox.SelectionBoxItem.ToString() ?? string.Empty,
            crtPath = ProtocolCbBox.SelectedIndex == 3 ? SslPathBox.Text : string.Empty,
            keyPath = ProtocolCbBox.SelectedIndex == 3 ? SslKeyBox.Text : string.Empty,
            useEncryption = EnableCryptoCBox.IsChecked ?? false,
            useCompression = EnableCompressCBox.IsChecked ?? false
        };
        var _body = JsonConvert.SerializeObject(requestData, Formatting.Indented);
        Core.App.CurrentLogger.LogDebug($"EditProxyWindow: {_body}");
        var success = (await MEFApiConverter.UpdateTunnelAsync(_body)).code == 200;
        if (success)
        {
            Close();
        }
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void GetRemotePort_Click(object sender, RoutedEventArgs e)
    {
        RemotePortNudBox.Value = MEFApiConverter
            .GetFreePort(
                MEFApiConverter.CurrentNodesListInfo!.NodesList.FirstOrDefault(x => x.name == _proxy.node)!.nodeId,
                ProtocolCbBox.SelectedItem.ToString()).data;
    }

    private void ProtocolCbBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProtocolCbBox is null)
        {
            return;
        }

        if (ProtocolCbBox.SelectedIndex is 2 or 3)
        {
            RemotePortGrid.Collapse();
            RemoteAddressStackPanel.Show();
            CustomRequestHeaderSettings.Show();
            SecurityOptionsSettingsExpander.Show();
            switch (ProtocolCbBox.SelectedIndex)
            {
                case 2:
                    SourceProtocolSettingsExpanderItemForHttp.Show();
                    SourceProtocolSettingsExpanderItemForHttps.Hide();
                    CertificateSettingsForPath.Hide();
                    CertificateSettingsForPrivateKey.Hide();
                    break;
                case 3:
                    SourceProtocolSettingsExpanderItemForHttp.Hide();
                    SourceProtocolSettingsExpanderItemForHttps.Show();
                    CertificateSettingsForPath.Show();
                    CertificateSettingsForPrivateKey.Show();
                    break;
            }
        }
        else
        {
            RemoteAddressStackPanel.Collapse();
            RemotePortGrid.Show();
            CustomRequestHeaderSettings.Collapse();
            SecurityOptionsSettingsExpander.Hide();
            SourceProtocolSettingsExpanderItemForHttp.Hide();
            SourceProtocolSettingsExpanderItemForHttps.Hide();
            CertificateSettingsForPath.Hide();
            CertificateSettingsForPrivateKey.Hide();
        }
    }
}