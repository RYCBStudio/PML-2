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
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Core.MEFIntegrated;
using MEFrpLauncherX.Core.Models;
using MEFrpLauncherX.ViewModels.Controls;
using MEFrpLauncherX.Views;
using ReactiveUI;

namespace MEFrpLauncherX.Controls;

public partial class CreateProxy : UserControl
{
    private readonly CreateProxyViewModel _createProxyViewModel;

    private readonly TunnelNodeViewModel _node;

    // 当由引导模式创建时，外部可以通过此属性指定首选协议(例如 "tcp", "udp", "http", "https")
    public string? PreferredProtocol
    {
        get;
        set;
    }

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

    // 对外暴露 ViewModel 以便页面可以设置默认值
    public CreateProxyViewModel ViewModel => _createProxyViewModel;

    // 暴露当前节点
    public TunnelNodeViewModel Node => _node;

    private async Task<bool> CreateProxy_OnCreateProxy()
    {
        // 提前提取常用值，避免多次重复计算
        var proxyName = _createProxyViewModel.ProxyName;
        var localIp = _createProxyViewModel.LocalAddress;
        var proxyType = ProtocolCbBox.SelectedItem?.ToString()?.ToLower() ?? "";
        var httpPlugin = GetHttpPlugin(proxyType);

        // 验证前置：先做轻量校验，避免在失败时构造完整 requestData
        if (proxyName.IsNullOrEmpty())
        {
            await MessageBox.ShowAsync(message: Languages.Text_CreateProxy_EnterProxyName);
            return false;
        }

        if (localIp.IsNullOrEmpty())
        {
            await MessageBox.ShowAsync(message: Languages.Text_CreateProxy_EnterLocalAddress);
            return false;
        }

        if (proxyType is "tcp" or "udp")
        {
            var allowRange = _node.AllowPorts.Split('-');
            var remotePort = _createProxyViewModel.RemotePort;
            if (!(remotePort >= Convert.ToInt32(allowRange[0]) &&
                  remotePort <= Convert.ToInt32(allowRange[1])))
            {
                await MessageBox.ShowAsync(message: Languages.Text_CreateProxy_EnterValidPort);
                return false;
            }
        }

        var requestData = new InfoClasses.CreateProxyRequestData
        {
            nodeId = _node.NodeId,
            proxyName = proxyName,
            localIp = localIp,
            localPort = _createProxyViewModel.LocalPort,
            remotePort = proxyType is "tcp" or "udp" ? _createProxyViewModel.RemotePort : null,
            domain = JsonSerializer.Serialize(_createProxyViewModel.RemoteAddress, App.AppJsonSerializerContext.ListString),
            requestHeaders = _createProxyViewModel.RequestHeaders,
            responseHeaders = _createProxyViewModel.ResponseHeaders,
            proxyType = proxyType,
            accessKey = SecurityOptionsSelect.SelectedIndex == 2 ? AccessKeyBox.Text : string.Empty,
            httpPlugin = httpPlugin,
            httpUser = SecurityOptionsSelect.SelectedIndex == 1 ? HTTPBasicAuthNameBox.Text : string.Empty,
            httpPassword = SecurityOptionsSelect.SelectedIndex == 1 ? HTTPBasicAuthPwdBox.Text : string.Empty,
            hostHeaderRewrite = _createProxyViewModel.HostHeaderRewrite,
            crtPath = proxyType == "https" ? SslPathBox.Text : string.Empty,
            keyPath = proxyType == "https" ? SslKeyBox.Text : string.Empty,
            proxyProtocolVersion = ProxyProtocolCbBox.SelectionBoxItem.ToString().Contains(Languages.Text_CreateProxy_NotEnabled)
                ? ""
                : ProxyProtocolCbBox.SelectionBoxItem.ToString() ?? "",
            useEncryption = EnableCryptoCBox.IsChecked ?? false,
            useCompression = EnableCompressCBox.IsChecked ?? false,
            transportProtocol = TpTcpRb.IsChecked == true ? "tcp" : "quic",
            locations = JsonSerializer.Serialize(_createProxyViewModel.Locations, App.AppJsonSerializerContext.ListString)
        };
        CreateProxyPage.Instance._targetRequest = requestData;

        var _body = JsonSerializer.Serialize(requestData, App.AppJsonSerializerContext.CreateProxyRequestData);
        var success = (await MEFrpApiConverter.PostNewTunnelAsync(_body)).code == 200;
        if (success)
        {
            MainContainer.IsEnabled = false;
        }

        return success;
    }

    private string GetHttpPlugin(string? protoType)
    {
        if (protoType == "https")
        {
            if (SameAsHttps.IsChecked == true)
                return string.Empty;
            return Https2Http.IsChecked == true ? "https2http" : string.Empty;
        }

        if (protoType == "http")
        {
            if (SameAsHttp.IsChecked == true)
                return string.Empty;
            return Http2Https.IsChecked == true ? "http2https" : string.Empty;
        }

        return string.Empty;
    }

    private void CreateProxy_Loaded(object sender, VisualTreeAttachmentEventArgs e)
    {
        CurrentProxyInfo.DataContext = _node;
        ProtocolCbBox.ItemsSource = _node.AllowTypes;
        // 如果外部指定了首选协议，则尝试选择该协议
        if (!string.IsNullOrEmpty(PreferredProtocol))
        {
            var items = ProtocolCbBox.Items?.Cast<object?>().ToList() ?? new List<object?>();
            var idx = items.FindIndex(item =>
                string.Equals(item?.ToString(), PreferredProtocol, StringComparison.OrdinalIgnoreCase));
            ProtocolCbBox.SelectedIndex = idx >= 0 ? idx : 0;
        }
        else
        {
            ProtocolCbBox.SelectedIndex = 0;
        }
    }

    /// <summary>常用端口快捷按钮：将 Tag 中的端口号填入本地端口</summary>
    private void QuickPort_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && int.TryParse(tag, out var port))
        {
            _createProxyViewModel.LocalPort = port;
        }
    }

    /// <summary>套用选中模板到当前表单</summary>
    private async void ApplyTemplate_Click(object? sender, RoutedEventArgs e)
    {
        var tpl = _createProxyViewModel.SelectedTemplate;
        if (tpl is null)
        {
            await MessageBox.ShowAsync(message: Languages.Text_CreateProxy_SelectTemplateFirst);
            return;
        }

        _createProxyViewModel.LocalAddress = tpl.LocalAddress.IsNullOrEmpty() ? "127.0.0.1" : tpl.LocalAddress;
        if (tpl.LocalPort > 0)
        {
            _createProxyViewModel.LocalPort = tpl.LocalPort;
        }

        if (!tpl.Protocol.IsNullOrEmpty())
        {
            var items = ProtocolCbBox.Items?.Cast<object?>().ToList() ?? new List<object?>();
            var idx = items.FindIndex(item =>
                string.Equals(item?.ToString(), tpl.Protocol, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                ProtocolCbBox.SelectedIndex = idx;
            }
        }

        if (tpl.RemotePort is > 0)
        {
            _createProxyViewModel.RemotePort = tpl.RemotePort.Value;
        }

        EnableCryptoCBox.IsChecked = tpl.UseEncryption;
        EnableCompressCBox.IsChecked = tpl.UseCompression;
        Growl.Success(Languages.Text_CreateProxy_TemplateApplied);
    }

    /// <summary>将当前表单参数保存为模板</summary>
    private async void SaveTemplate_Click(object? sender, RoutedEventArgs e)
    {
        var name = _createProxyViewModel.TemplateName?.Trim();
        if (name.IsNullOrEmpty())
        {
            await MessageBox.ShowAsync(message: Languages.Text_CreateProxy_TemplateNameRequired);
            return;
        }

        var tpl = new ProxyTemplate
        {
            Name = name,
            LocalAddress = _createProxyViewModel.LocalAddress,
            LocalPort = _createProxyViewModel.LocalPort,
            Protocol = ProtocolCbBox.SelectedItem?.ToString()?.ToLower(),
            RemotePort = ProtocolCbBox.SelectedItem?.ToString()?.ToLower() is "tcp" or "udp"
                ? _createProxyViewModel.RemotePort
                : null,
            UseEncryption = EnableCryptoCBox.IsChecked ?? false,
            UseCompression = EnableCompressCBox.IsChecked ?? false
        };

        var saved = false;
        ConfigManager.UpdateConfig(cfg =>
        {
            cfg.ProxyTemplates ??= [];
            var existing = cfg.ProxyTemplates.FirstOrDefault(t =>
                string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                cfg.ProxyTemplates.Remove(existing);
            }

            cfg.ProxyTemplates.Add(tpl);
            saved = true;
        });

        if (saved)
        {
            _createProxyViewModel.Templates = ConfigManager.CurrentConfig.ProxyTemplates;
            _createProxyViewModel.SelectedTemplate = tpl;
            Growl.Success(Languages.Text_CreateProxy_TemplateSaved);
        }
    }

    /// <summary>删除选中的模板</summary>
    private async void DeleteTemplate_Click(object? sender, RoutedEventArgs e)
    {
        var tpl = _createProxyViewModel.SelectedTemplate;
        if (tpl is null)
        {
            await MessageBox.ShowAsync(message: Languages.Text_CreateProxy_SelectTemplateFirst);
            return;
        }

        ConfigManager.UpdateConfig(cfg =>
        {
            cfg.ProxyTemplates?.RemoveAll(t =>
                string.Equals(t.Name, tpl.Name, StringComparison.OrdinalIgnoreCase));
        });
        _createProxyViewModel.Templates = ConfigManager.CurrentConfig.ProxyTemplates;
        _createProxyViewModel.SelectedTemplate = null;
        Growl.Success(Languages.Text_CreateProxy_TemplateDeleted);
    }

    public async void GetRemotePort_Click(object sender, RoutedEventArgs e)
    {
        Loading.IsVisible = true;
        if (ProtocolCbBox.SelectedItem?.ToString()?.ToLower() is not "tcp" and not "udp" && sender is not string)
        {
            Loading.IsVisible = false;
            return;
        }

        var type = sender is string t ? t : ProtocolCbBox.SelectedItem?.ToString();
        var res = (await MEFrpApiConverter.GetFreePortAsync(_node.NodeId, type)).data;
        Loading.IsVisible = false;
        RemotePortNudBox.Value = res;
    }

    private void ProtocolCbBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProtocolCbBox is null)
        {
            return;
        }

        if (ProtocolCbBox.SelectedItem?.ToString()?.ToLower() is "http" or "https")
        {
            RemotePortGrid.Collapse();
            RemoteAddressStackPanel.Show();
            SecurityOptionsSettingsExpander.Show();
            switch (ProtocolCbBox.SelectedItem?.ToString()?.ToLower())
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

    private async void EditRequestHeaders(object? sender, RoutedEventArgs e)
    {
        var he = new HeadersEdit();
        if (_createProxyViewModel.RequestHeaders is { Count: > 0 })
        {
            he.Headers.AddRange(_createProxyViewModel.RequestHeaders
                .Where(kv => kv.Key is not null && kv.Value is not null)
                .Select(kv => new RequestHeader { Name = kv.Key, Value = kv.Value }));
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
        if (await cd.ShowAsync() == ContentDialogResult.Primary)
        {
            foreach (var h in he.Headers)
            {
                if (!_createProxyViewModel.RequestHeaders.ContainsKey(h.Name))
                    _createProxyViewModel.RequestHeaders.Add(h.Name, h.Value);
            }
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
            _createProxyViewModel.RemoteAddress?.Clear();
            _createProxyViewModel.RemoteAddress?.AddRange(de.Domains);
        }
    }

    private async void CheckPort(object? sender, RoutedEventArgs e)
    {
        var psv = new PortScannerView();
        var cd = new ContentDialog
        {
            Title = Languages.Text_CreateProxy_FindMinecraftPort,
            Content = psv,
            PrimaryButtonText = Languages.Text_Global_Confirm,
            DefaultButton = ContentDialogButton.Primary,
            IsSecondaryButtonEnabled = false,
            CloseButtonText = Languages.Text_Global_Cancel
        };
        if (await cd.ShowAsync() == ContentDialogResult.Primary)
        {
            _createProxyViewModel.LocalPort = psv.DataContext.SelectedResult?.Port ?? 0;
        }
    }

    private async void EditResponseHeaders(object? sender, RoutedEventArgs e)
    {
        var he = new HeadersEdit();
        if (_createProxyViewModel.ResponseHeaders is { Count: > 0 })
        {
            he.Headers.AddRange(_createProxyViewModel.ResponseHeaders
                .Where(kv => kv.Key is not null && kv.Value is not null)
                .Select(kv => new RequestHeader { Name = kv.Key, Value = kv.Value }));
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
        if (await cd.ShowAsync() == ContentDialogResult.Primary)
        {
            foreach (var h in he.Headers)
            {
                if (!_createProxyViewModel.ResponseHeaders.ContainsKey(h.Name))
                    _createProxyViewModel.ResponseHeaders.Add(h.Name, h.Value);
            }
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
    public CreateProxyViewModel()
    {
        // 本地地址默认值优先取配置（Settings.json -> CreateProxyDefaults），缺省 127.0.0.1
        LocalAddress = ConfigManager.CurrentConfig.CreateProxyDefaults?.LocalAddress ?? "127.0.0.1";
        Templates = ConfigManager.CurrentConfig.ProxyTemplates ?? [];
    }

    public TunnelNodeViewModel TunnelNode
    {
        get;
        set;
    }

    [LegalProxyNameValidator(ErrorMessageResourceName = "Text_Validation_ProxyNameNoDot", ErrorMessageResourceType = typeof(Languages))]
    [Required(ErrorMessageResourceName = "Text_Validation_Required", ErrorMessageResourceType = typeof(Languages))]
    public string ProxyName
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "";

    [Required(ErrorMessageResourceName = "Text_Validation_Required", ErrorMessageResourceType = typeof(Languages))]
    public string LocalAddress
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "";

    [Required(ErrorMessageResourceName = "Text_Validation_Required", ErrorMessageResourceType = typeof(Languages))]
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

    [Required(ErrorMessageResourceName = "Text_Validation_Required", ErrorMessageResourceType = typeof(Languages))]
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

    /// <summary>已保存的创建模板（持久化于 Settings.json）</summary>
    public List<ProxyTemplate> Templates
    {
        get;
        set;
    }

    /// <summary>当前选中的模板</summary>
    public ProxyTemplate? SelectedTemplate
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>保存模板时输入的名称</summary>
    public string? TemplateName
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }
}