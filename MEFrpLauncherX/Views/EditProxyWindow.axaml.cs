using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using MEFrpLauncherX.Controls;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Core.MEFIntegrated;
using MEFrpLauncherX.ViewModels;

namespace MEFrpLauncherX.Views;

public partial class EditProxyWindow : Window
{
    private readonly CreateProxyViewModel _createProxyViewModel;
    private readonly UserProxyViewModel _proxy;
    private readonly string _type;

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
        EnableCompressCBox.IsChecked = pr.useCompression;
        EnableCryptoCBox.IsChecked = pr.useEncryption;
        SecurityOptionsSelect.SelectedIndex = GetSecurityOptions();
        _type = pr.proxyType;
        TpQuicRb.IsChecked = pr.transportProtocol switch
        {
            "quic" => true,
            _ => false
        };
        TpTcpRb.IsChecked = pr.transportProtocol switch
        {
            "tcp" => true,
            _ => false
        };
        SameAsHttp.IsChecked = GetTypeFromHttpPlugin(pr.httpPlugin).ToLower() == "http";
        Http2Https.IsChecked = pr.httpPlugin.ToLower() switch
        {
            "http2https" => true,
            _ => false
        };
        SameAsHttps.IsChecked = GetTypeFromHttpPlugin(pr.httpPlugin).ToLower() == "https";
        Https2Http.IsChecked = pr.httpPlugin.ToLower() switch
        {
            "https2http" => true,
            _ => false
        };
        _createProxyViewModel = new CreateProxyViewModel
        {
            ProxyName = pr.proxyName,
            LocalAddress = pr.localIp,
            LocalPort = pr.localPort,
            RemoteAddress = [.. pr.Domains.Distinct()],
            RemotePort = pr.remotePort,
            XFromWhere = pr.headerXFromWhere,
            HostHeaderRewrite = pr.hostHeaderRewrite,
            RequestHeaders = pr.RequestHeaders,
            ResponseHeaders = pr.ResponseHeaders,
            Locations = pr.Locations
        };
        _createProxyViewModel.RemoteAddress.AddRange(_proxy.Domains ?? []);
        DataContext = _createProxyViewModel;
        SecurityOptionsSelect.SelectedIndex = GetSecurityOptions();
        HTTPBasicAuthNameBox.Text = pr.httpUser;
        HTTPBasicAuthPwdBox.Text = pr.httpPassword;
        AccessKeyBox.Text = pr.accessKey;
        InitPanels();
    }

    private async void EditResponseHeaders(object? sender, RoutedEventArgs e)
    {
        var he = new HeadersEdit();
        if (_createProxyViewModel.ResponseHeaders is not null && _createProxyViewModel.ResponseHeaders.Count != 0)
        {
            he.Headers.AddRange([
                .. _createProxyViewModel.ResponseHeaders.Select(kv =>
                {
                    var key = kv.Key;
                    var val = kv.Value;
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
                        Name = key,
                        Value = val
                    };
                })
            ]);
        }

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

        var cd = new ContentDialog
        {
            Title = Languages.Text_CreateProxy_EditResponseHeaders,
            Content = he,
            PrimaryButtonText = Languages.Text_Global_Confirm,
            DefaultButton = ContentDialogButton.Primary,
            IsSecondaryButtonEnabled = false,
            CloseButtonText = Languages.Text_Global_Cancel
        };
        var res = await cd.ShowAsync();
        if (res == ContentDialogResult.Primary)
        {
            he.Headers.ToList().ForEach(h =>
            {
                if (!_createProxyViewModel.ResponseHeaders.ContainsKey(h.Name))
                {
                    _createProxyViewModel.ResponseHeaders?.Add(h.Name, h.Value);
                }
            });
        }
    }

    private async void EditLocations(object? sender, RoutedEventArgs e)
    {
        var de = new DomainsEdit();
        if (_createProxyViewModel.Locations is not null && _createProxyViewModel.Locations.Count != 0)
        {
            de.Domains.AddRange(_createProxyViewModel.Locations);
        }

        var cd = new ContentDialog
        {
            Title = Languages.Text_CreateProxy_EditLocations,
            Content = de,
            PrimaryButtonText = Languages.Text_Global_Confirm,
            DefaultButton = ContentDialogButton.Primary,
            IsSecondaryButtonEnabled = false,
            CloseButtonText = Languages.Text_Global_Cancel
        };
        var res = await cd.ShowAsync();
        if (res == ContentDialogResult.Primary)
        {
            _createProxyViewModel.Locations?.Clear();
            _createProxyViewModel.Locations?.AddRange(de.Domains);
        }
    }

    /// <summary>
    ///     获取隧道的安全选项
    ///     0 - 禁用
    ///     1 - HTTP Basic Auth
    ///     2 - 访问密钥
    /// </summary>
    /// <returns></returns>
    private int GetSecurityOptions()
    {
        if (_proxy.accessKey.IsNullOrEmpty() && _proxy.httpUser.IsNullOrEmpty() && _proxy.httpPassword.IsNullOrEmpty())
        {
            return 0;
        }

        return _proxy.accessKey.IsNullOrEmpty() ? 1 : 2;
    }

    private async void EditRequestHeaders(object? sender, RoutedEventArgs e)
    {
        var he = new HeadersEdit();
        if (_createProxyViewModel.RequestHeaders is not null && _createProxyViewModel.RequestHeaders.Count != 0)
        {
            he.Headers.AddRange([
                .. _createProxyViewModel.RequestHeaders.Select(kv =>
                {
                    var key = kv.Key;
                    var val = kv.Value;
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
                        Name = key,
                        Value = val
                    };
                })
            ]);
        }

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

        var cd = new ContentDialog
        {
            Title = Languages.Text_CreateProxy_EditRequestHeaders,
            Content = he,
            PrimaryButtonText = Languages.Text_Global_Confirm,
            DefaultButton = ContentDialogButton.Primary,
            IsSecondaryButtonEnabled = false,
            CloseButtonText = Languages.Text_Global_Cancel
        };
        var res = await cd.ShowAsync();
        if (res == ContentDialogResult.Primary)
        {
            he.Headers.ToList().ForEach(h =>
            {
                if (!_createProxyViewModel.ResponseHeaders.ContainsKey(h.Name))
                {
                    _createProxyViewModel.ResponseHeaders?.Add(h.Name, h.Value);
                }
            });
        }
    }

    private async void EditDomains(object? sender, RoutedEventArgs e)
    {
        var de = new DomainsEdit();
        de.Domains.Clear();
        var filteredDomains = _proxy.Domains.Distinct();
        var enumerable = filteredDomains.ToList();
        if (filteredDomains is not null && enumerable.Count != 0)
        {
            de.Domains.AddRange(enumerable);
        }

        var cd = new ContentDialog
        {
            Title = Languages.Text_CreateProxy_EditDomains,
            Content = de,
            PrimaryButtonText = Languages.Text_Global_Confirm,
            DefaultButton = ContentDialogButton.Primary,
            IsSecondaryButtonEnabled = false,
            CloseButtonText = Languages.Text_Global_Cancel
        };
        var res = await cd.ShowAsync();
        if (res == ContentDialogResult.Primary)
        {
            _createProxyViewModel.RemoteAddress.Clear();
            _createProxyViewModel.RemoteAddress.AddRange([.. de.Domains]);
        }
    }

    private string GetHttpPlugin()
    {
        return _type.ToLower() switch
        {
            "http" when SameAsHttp.IsChecked == true => string.Empty,
            "http" when Http2Https.IsChecked == true => "http2https",
            "http" => string.Empty,
            "https" when SameAsHttps.IsChecked == true => "https2https",
            "https" when Https2Http.IsChecked == true => "https2http",
            _ => string.Empty
        };
    }

    private string GetTypeFromHttpPlugin(string plugin)
    {
        return plugin?.ToLower() switch
        {
            "http2https" => "http",
            "https2https" => "https",
            "https2http" => "https",
            "" or null => DetermineTypeFromOtherFields(),
            _ => "unknown"
        };
    }

    private string DetermineTypeFromOtherFields()
    {
        // 根据其他字段状态来推断类型
        if (SameAsHttp?.IsChecked == true)
        {
            return "http";
        }

        if (Http2Https?.IsChecked == true)
        {
            return "http";
        }

        if (SameAsHttps?.IsChecked == true)
        {
            return "https";
        }

        if (Https2Http?.IsChecked == true)
        {
            return "https";
        }

        // 默认返回 http
        return "http";
    }


    private async void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        var requestData = new InfoClasses.CreateProxyRequestData
        {
            proxyId = _proxy.proxyId,
            proxyName = _createProxyViewModel.ProxyName,
            localIp = _createProxyViewModel.LocalAddress,
            localPort = _createProxyViewModel.LocalPort,
            remotePort = _type.ToLower() is "tcp" or "udp" ? _createProxyViewModel.RemotePort : 0,
            domain = "[\"" + string.Join("\", \"", _createProxyViewModel.RemoteAddress) + "\"]",
            requestHeaders = _createProxyViewModel.RequestHeaders,
            responseHeaders = _createProxyViewModel.ResponseHeaders,
            accessKey = SecurityOptionsSelect.SelectedIndex == 2 ? AccessKeyBox.Text : string.Empty,
            httpPlugin = GetHttpPlugin(),
            httpUser = SecurityOptionsSelect.SelectedIndex == 1 ? HTTPBasicAuthNameBox.Text : string.Empty,
            httpPassword = SecurityOptionsSelect.SelectedIndex == 1 ? HTTPBasicAuthPwdBox.Text : string.Empty,
            hostHeaderRewrite = _createProxyViewModel.HostHeaderRewrite,
            crtPath = _type.ToLower() == "https" ? SslPathBox.Text : string.Empty,
            keyPath = _type.ToLower() == "https" ? SslKeyBox.Text : string.Empty,
            proxyProtocolVersion = ProxyProtocolCbBox.SelectionBoxItem.ToString().Contains(Languages.Text_CreateProxy_NotEnabled)
                ? ""
                : ProxyProtocolCbBox.SelectionBoxItem.ToString() ?? "",
            useEncryption = EnableCryptoCBox.IsChecked ?? false,
            useCompression = EnableCompressCBox.IsChecked ?? false,
            transportProtocol = TpTcpRb.IsChecked == true ? "tcp" : "quic",
            locations = "[\"" + string.Join("\", \"", _createProxyViewModel.Locations ?? []) + "\"]"
        };
        var _body = JsonSerializer.Serialize(requestData, App.AppJsonSerializerContext.CreateProxyRequestData);
        Core.App.CurrentLogger.LogDebug($"EditProxyWindow: {_body}");
        var success = (await MEFrpApiConverter.UpdateTunnelAsync(_body)).code == 200;
        if (success)
        {
            Close();
        }
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close(false);

    /// <summary>
    ///     从证书助手已签发的证书中选择一个，回填 HTTPS 隧道的证书/密钥路径（26.4 阶段 B）。
    ///     与创建页保持同一交互；无本地证书时给出提示。
    /// </summary>
    private async void PickCertificateFromAssistant(object? sender, RoutedEventArgs e)
    {
        try
        {
            var items = Core.Services.CertStore.List();
            if (items.Count == 0)
            {
                var goCreate = new ContentDialog
                {
                    Title = Languages.Text_Certificate_Title,
                    Content = Languages.Text_Certificate_Empty,
                    PrimaryButtonText = Languages.Text_Certificate_OpenDirectory,
                    CloseButtonText = Languages.Text_Global_Cancel,
                    DefaultButton = ContentDialogButton.Close
                };
                if (await goCreate.ShowAsync() == ContentDialogResult.Primary)
                {
                    OpenCertificateRoot();
                }

                return;
            }

            var list = new ListBox
            {
                ItemsSource = items.Select(i => i.DisplayName).ToList(),
                SelectedIndex = 0,
                MinWidth = 320
            };
            var cd = new ContentDialog
            {
                Title = Languages.Text_Certificate_SelectTitle,
                Content = list,
                PrimaryButtonText = Languages.Text_Global_Confirm,
                CloseButtonText = Languages.Text_Global_Cancel,
                DefaultButton = ContentDialogButton.Primary
            };
            if (await cd.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            var idx = list.SelectedIndex;
            if (idx < 0 || idx >= items.Count)
            {
                return;
            }

            var picked = items[idx];
            SslPathBox.Text = picked.FullChainPath;
            SslKeyBox.Text = picked.PrivateKeyPath;

            // 临期 / Staging 提醒，避免误用
            if (picked.Staging)
            {
                Growl.Warning(Languages.Text_Certificate_Env_Staging);
            }
            else if (picked.IsExpiringSoon)
            {
                Growl.Warning($"{Languages.Text_Certificate_ExpiringSoon}（{picked.DaysToExpiry} d）");
            }
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger?.Error(ex, "选择证书失败");
        }
    }

    /// <summary>用系统文件管理器打开证书根目录。</summary>
    private static void OpenCertificateRoot()
    {
        try
        {
            var dir = Core.Services.CertStore.RootPath;
            Directory.CreateDirectory(dir);
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", dir);
            }
            else
            {
                Process.Start("xdg-open", dir);
            }
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger?.Error(ex, "打开证书目录失败");
        }
    }

    private async void GetRemotePort_Click(object sender, RoutedEventArgs e) => RemotePortNudBox.Value =
        (await MEFrpApiConverter.GetFreePortAsync(_proxy.Node.nodeId, _type.ToLower())).data;

    private void InitPanels()
    {
        if (_type.ToLower() is "http" or "https")
        {
            RemotePortGrid.Collapse();
            RemoteAddressStackPanel.Show();
            CustomRequestHeaderSettings.Show();
            SecurityOptionsSettingsExpander.Show();
            switch (_type.ToLower())
            {
                case "http":
                    SourceProtocolSettingsExpanderItemForHttp.Show();
                    CustomRequestHeaderSettings.Show();
                    CustomResponseHeaderSettings.Show();
                    LocationSettings.Show();
                    SourceProtocolSettingsExpanderItemForHttps.Hide();
                    CertificateSettingsForPath.Hide();
                    CertificateSettingsForPrivateKey.Hide();
                    break;
                case "https":
                    SourceProtocolSettingsExpanderItemForHttp.Hide();
                    LocationSettings.Collapse();
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
            LocationSettings.Collapse();
            CustomRequestHeaderSettings.Collapse();
            CustomResponseHeaderSettings.Collapse();
            SecurityOptionsSettingsExpander.Hide();
            SourceProtocolSettingsExpanderItemForHttp.Hide();
            SourceProtocolSettingsExpanderItemForHttps.Hide();
            CertificateSettingsForPath.Hide();
            CertificateSettingsForPrivateKey.Hide();
        }
    }
}