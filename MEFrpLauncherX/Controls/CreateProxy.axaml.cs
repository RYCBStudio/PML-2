using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.MEFIntergrated;
using MEFrpLauncherX.Views;
using ReactiveUI;

namespace MEFrpLauncherX.Controls;

public partial class CreateProxy : UserControl
{
    private readonly CreateProxyViewModel _createProxyViewModel;
    private readonly TunnelNodeViewModel _node;

    public CreateProxy()
    {
        InitializeComponent();
        _node = new TunnelNodeViewModel
        {
            NodeId = 114,
            Name = "日本/下北泽①①④⑤①④",
            Description =
                "いずれ花と散る わたしの生命\n帰らぬ時 指おり数えても\n涙と笑い 過去と未来\n引き裂かれしわたしは 冬の花\nあなたは太陽 わたしは月\n光と闇が交じり合わぬように\n涙にけむる ふたりの未来\n美しすぎる過去は蜃気楼\n旅みたいだね\n生きるってどんな時でも\n木枯らしの中\nぬくもり求め 彷徨う\n泣かないで わたしの恋心\n涙はお前にはにあわない\nゆけ ただゆけ\nいっそわたしがゆくよ",
            AllowTypes =
            [
                "tcp", "udp", "http", "https"
            ],
            AllowPorts = "1-65535",
            Bandwidth = "140Gbps",
            LoadPercent = 43,
            IsOnline = true,
            CanBuildSite = true,
            AllowHighTraffic = true,
            Region = "oversea",
            IsSelected = false,
            AllowGroup =
            [
                "admin", "default", "vip", "sponsor"
            ]
        };
        AttachedToVisualTree += CreateProxy_Loaded;
        if (!Design.IsDesignMode)
        {
            CreateProxyPage.Instance.OnCreateProxy += CreateProxy_OnCreateProxy;
        }

        _createProxyViewModel = new CreateProxyViewModel();
        OperationPanel.DataContext = _createProxyViewModel;
    }

    public CreateProxy(TunnelNodeViewModel node)
    {
        InitializeComponent();
        _node = node;
        AttachedToVisualTree += CreateProxy_Loaded;
        CreateProxyPage.Instance.OnCreateProxy += CreateProxy_OnCreateProxy;
        _createProxyViewModel = new CreateProxyViewModel();
        OperationPanel.DataContext = _createProxyViewModel;
    }

    private async Task<bool> CreateProxy_OnCreateProxy()
    {
        var requestData = new
        {
            nodeId = _node.NodeId,
            proxyName = _createProxyViewModel.ProxyName,
            localIp = _createProxyViewModel.LocalAddress,
            localPort = _createProxyViewModel.LocalPort,
            remotePort = (int?)(ProtocolCbBox.SelectedIndex is 0 or 1 ? _createProxyViewModel.RemotePort : null),
            domain = "[\"" + string.Join("\", \"", _createProxyViewModel.RemoteAddress) + "\"]",
            requestHeaders = _createProxyViewModel.RequestHeaders,
            responseHeaders = _createProxyViewModel.ResponseHeaders,
            proxyType = ProtocolCbBox.SelectedItem?.ToString()?.ToLower() ?? "",
            accessKey = SecurityOptionsSelect.SelectedIndex == 2 ? AccessKeyBox.Text : string.Empty,
            httpPlugin = GetHttpPlugin(),
            httpUser = SecurityOptionsSelect.SelectedIndex == 1 ? HTTPBasicAuthNameBox.Text : string.Empty,
            httpPassword = SecurityOptionsSelect.SelectedIndex == 1 ? HTTPBasicAuthPwdBox.Text : string.Empty,
            hostHeaderRewrite = _createProxyViewModel.HostHeaderRewrite,
            crtPath = ProtocolCbBox.SelectedIndex == 3 ? SslPathBox.Text : string.Empty,
            keyPath = ProtocolCbBox.SelectedIndex == 3 ? SslKeyBox.Text : string.Empty,
            proxyProtocolVersion = ProxyProtocolCbBox.SelectionBoxItem.ToString().Contains("不启用")
                ? ""
                : ProxyProtocolCbBox.SelectionBoxItem.ToString() ?? "",
            useEncryption = EnableCryptoCBox.IsChecked ?? false,
            useCompression = EnableCompressCBox.IsChecked ?? false,
            transportProtocol = TpTcpRb.IsChecked == true ? "tcp" : "quic",
            locations = "[\"" + string.Join("\", \"", _createProxyViewModel.Locations) + "\"]"
        };
        if (requestData.proxyName.IsNullOrEmpty())
        {
            await MessageBox.ShowAsync(message: "请输入隧道名");
            return false;
        }

        if (requestData.localIp.IsNullOrEmpty())
        {
            await MessageBox.ShowAsync(message: "请输入本地地址");
            return false;
        }

        var allowRange = _node.AllowPorts.Split('-');
        if (requestData.proxyType is "tcp" or "udp")
        {
            if (!(requestData.remotePort >= Convert.ToInt32(allowRange[0]) &&
                  requestData.remotePort <= Convert.ToInt32(allowRange[1])))
            {
                await MessageBox.ShowAsync(message: "请输入合法的端口号");
                return false;
            }
        }

        var _body = JsonSerializer.Serialize(requestData, new JsonSerializerOptions { WriteIndented = true });
        var success = (await MEpiConverter.PostNewTunnelAsync(_body)).code == 200;
        if (success)
        {
            MainContainer.IsEnabled = false;
        }

        return success;
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

    private void CreateProxy_Loaded(object sender, VisualTreeAttachmentEventArgs e)
    {
        CurrentProxyInfo.DataContext = _node;
        ProtocolCbBox.ItemsSource = _node.AllowTypes;
    }

    private async void GetRemotePort_Click(object sender, RoutedEventArgs e)
    {
        Loading.IsVisible = true;
        var data = -1;
        var res = (await MEpiConverter.GetFreePortAsync(_node.NodeId, ProtocolCbBox.SelectedItem.ToString())).data;
        data = res;
        Loading.IsVisible = false;
        RemotePortNudBox.Value = data;
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
            SecurityOptionsSettingsExpander.Show();
            switch (ProtocolCbBox.SelectedIndex)
            {
                case 2:
                    SourceProtocolSettingsExpanderItemForHttp.Show();
                    CustomRequestHeaderSettings.Show();
                    CustomResponseHeaderSettings.Show();
                    LocationSettings.Show();
                    SourceProtocolSettingsExpanderItemForHttps.Hide();
                    CertificateSettingsForPath.Hide();
                    CertificateSettingsForPrivateKey.Hide();
                    break;
                case 3:
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

    private async void EditRequestHeaders(object? sender, RoutedEventArgs e)
    {
        var he = new HeadersEdit();
        if (_createProxyViewModel.RequestHeaders is not null && _createProxyViewModel.RequestHeaders.Count != 0)
        {
            he.Headers.AddRange(_createProxyViewModel.RequestHeaders.Select(kv =>
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
                        Name = key!,
                        Value = val!
                    };
                })
                .ToList());
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
            he.Headers.ToList().ForEach(h =>
            {
                if (!_createProxyViewModel.RequestHeaders.ContainsKey(h.Name))
                {
                    _createProxyViewModel.RequestHeaders?.Add(h.Name, h.Value);
                }
            });
        }
    }

    private async void EditDomains(object? sender, RoutedEventArgs e)
    {
        var de = new DomainsEdit();
        if (_createProxyViewModel.RemoteAddress is not null && _createProxyViewModel.RemoteAddress.Count != 0)
        {
            de.Domains.AddRange(_createProxyViewModel.RemoteAddress);
        }

        var cd = new ContentDialog
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
            _createProxyViewModel.RemoteAddress?.Clear();
            _createProxyViewModel.RemoteAddress?.AddRange(de.Domains);
        }
    }

    private async void CheckPort(object? sender, RoutedEventArgs e)
    {
        var psv = new PortScannerView();
        var cd = new ContentDialog
        {
            Title = "查找 Minecraft 端口",
            Content = psv,
            PrimaryButtonText = "确定",
            DefaultButton = ContentDialogButton.Primary,
            IsSecondaryButtonEnabled = false,
            CloseButtonText = "取消"
        };
        if (await cd.ShowAsync() == ContentDialogResult.Primary)
        {
            _createProxyViewModel.LocalPort = psv.DataContext.SelectedResult?.Port ?? 0;
        }
    }

    private async void EditResponseHeaders(object? sender, RoutedEventArgs e)
    {
        var he = new HeadersEdit();
        if (_createProxyViewModel.ResponseHeaders is not null && _createProxyViewModel.ResponseHeaders.Count != 0)
        {
            he.Headers.AddRange(_createProxyViewModel.ResponseHeaders.Select(kv =>
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
                        Name = key!,
                        Value = val!
                    };
                })
                .ToList());
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
            Title = "编辑响应头",
            Content = he,
            PrimaryButtonText = "确定",
            DefaultButton = ContentDialogButton.Primary,
            IsSecondaryButtonEnabled = false,
            CloseButtonText = "取消"
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
            Title = "编辑路径",
            Content = de,
            PrimaryButtonText = "确定",
            DefaultButton = ContentDialogButton.Primary,
            IsSecondaryButtonEnabled = false,
            CloseButtonText = "取消"
        };
        var res = await cd.ShowAsync();
        if (res == ContentDialogResult.Primary)
        {
            _createProxyViewModel.Locations?.Clear();
            _createProxyViewModel.Locations?.AddRange(de.Domains);
        }
    }
}

public class SelectIndexToVisibleConverter : IValueConverter
{
    public static SelectIndexToVisibleConverter Instance
    {
        get;
    } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is 1;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class SelectIndexToVisibleConverterReverse : IValueConverter
{
    public static SelectIndexToVisibleConverterReverse Instance
    {
        get;
    } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is 2;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class LegalProxyNameValidator : ValidationAttribute
{
    public override bool IsValid(object? value) => value is string name && !name.Contains('.');
}

public class CreateProxyViewModel : ViewModelBase
{
    public TunnelNodeViewModel TunnelNode
    {
        get;
        set;
    }

    [LegalProxyNameValidator(ErrorMessage = "隧道名不能包含: .")]
    [Required(ErrorMessage = "必填项")]
    public string ProxyName
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "";

    [Required(ErrorMessage = "必填项")]
    public string LocalAddress
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "";

    [Required(ErrorMessage = "必填项")]
    public int LocalPort
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public List<string> RemoteAddress
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    public List<string> Locations
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    [Required(ErrorMessage = "必填项")]
    public int RemotePort
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string XFromWhere
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "";

    public Dictionary<string, string> RequestHeaders
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    public Dictionary<string, string> ResponseHeaders
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    public string HostHeaderRewrite
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "";
}