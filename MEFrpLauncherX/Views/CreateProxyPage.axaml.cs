using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using MEFrpLauncherX.Controls;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.MEFIntegrated;
using MEFrpLauncherX.Core.Services;
using MEFrpLauncherX.Models;
using MEFrpLauncherX.ViewModels;
using MEFrpLauncherX.ViewModels.Controls;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using MessageBox = MEFrpLauncherX.Core.Controls.MessageBox;
using MessageBoxIcon = MEFrpLauncherX.Core.Controls.MessageBoxIcon;

namespace MEFrpLauncherX.Views;

public partial class CreateProxyPage : UserControl
{
    private int _index;
    private CreateProxyPageViewModel _createProxyPageViewModel;
    private string _targetService;
    internal InfoClasses.CreateProxyRequestData _targetRequest;

    public CreateProxyPage()
    {
        InitializeComponent();
        _createProxyPageViewModel = new CreateProxyPageViewModel();
        _index = 0;
        Instance = this;
        DataContext = _createProxyPageViewModel;

        // 附加到可视化树后预热 Tab 页面（后台低优先级）
        AttachedToVisualTree += (_, _) =>
        {
            _createProxyPageViewModel.PrewarmTabs();
        };
    }

    public static CreateProxyPage Instance
    {
        get;
        private set;
    }

    public event Func<Task<bool>>? OnCreateProxy;
    private bool _isMap;

    private async void Next(object sender, RoutedEventArgs e)
    {
        _index++;
        switch (_index)
        {
            case 0:
                await _createProxyPageViewModel.LoadDataAsync();
                break;
            case 1:
                if (_createProxyPageViewModel.SelectedType == 1 || _isMap) //专家模式(原版)
                {
                    if (_createProxyPageViewModel.selectedNode != null)
                    {
                        if (_createProxyPageViewModel.selectedNode.IsOverloaded)
                        {
                            await MessageBox.ShowAsync("节点已过载，无法再创建隧道", "", MessageBoxIcon.Error);
                            _index--;
                            break;
                        }

                        var cp = new CreateProxy(_createProxyPageViewModel.selectedNode);
                        _createProxyPageViewModel.CurrentPage = cp;
                    }
                    else
                    {
                        await MessageBoxManager.GetMessageBoxStandard(Core.Languages.Languages.Caption_Error, "请选择一个节点").ShowAsync();
                        _index--;
                    }
                }
                else if (_createProxyPageViewModel.SelectedType is 2 or 3) // 地图模式（嘉豪原版/嘉豪版）
                {
                    // 检查是否有选中的区域
                    var selectedArea = _createProxyPageViewModel.GetSelectedAreaFromMap();

                    if (string.IsNullOrEmpty(selectedArea))
                    {
                        await MessageBoxManager.GetMessageBoxStandard("提示", "请在地图上选择一个区域").ShowAsync();
                        _index--;
                        break;
                    }

                    // 尝试获取已加载的节点容器
                    if (_createProxyPageViewModel.pages.TryGetValue("Create", out var ctrl) &&
                        ctrl is INodeContainer nc)
                    {
                        var vm = nc.ViewModel;

                        // 根据选中的区域筛选节点
                        var candidates = FilterNodesByArea(vm.AllNodes, selectedArea,
                            _createProxyPageViewModel.SelectedType == 2);

                        if (!candidates.Any())
                        {
                            await MessageBox.ShowAsync($"在“{selectedArea}”区域未找到可用节点，请手动选择", "提示", ButtonEnum.Ok);
                            _index--;
                            break;
                        }

                        // 将候选节点排序放到列表前面，并在 UI 渲染后切换回节点列表页
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            try
                            {
                                vm.FilteredNodes.Clear();
                                vm.FilteredNodes.AddRange(candidates);

                                // 自动选择第一个节点
                                var chosen = candidates.First();
                                _createProxyPageViewModel.selectedNode = chosen;

                                if (chosen.IsOverloaded)
                                {
                                    // Show message outside of the UI invoke to avoid blocking layout; mark to handle after
                                    return Task.CompletedTask;
                                }

                                nc = new NodesContainerCompact(vm);
                                // 切换页面并确保使用 Render 优先级以触发布局测量/排列
                                _createProxyPageViewModel.CurrentPage = nc as Control;
                                _isMap = true;

                                // 确保虚拟化 WrapPanel 已实际实现第一个项：滚动到该项会触发面板创建并测量
                                try
                                {
                                    //await nc.EnsureItemVisibleAsync(chosen);
                                }
                                catch
                                {
                                    // 忽略
                                }

                                return Task.CompletedTask;
                            }
                            catch (Exception exception)
                            {
                                return Task.FromException(exception);
                            }
                        }, DispatcherPriority.Render);

                        // 检查已选节点是否过载（在 UI 线程的操作后判断）
                        var chosenCheck = candidates.First();
                        if (chosenCheck.IsOverloaded)
                        {
                            await MessageBox.ShowAsync("节点已过载，无法再创建隧道", buttons: [TaskDialogButton.OKButton]);
                            _index--;
                            break;
                        }

                        _index--;
                    }
                    else
                    {
                        // 若未加载节点，回退到加载页
                        await _createProxyPageViewModel.LoadDataAsync();
                        await MessageBoxManager.GetMessageBoxStandard("提示", "正在加载节点，请重试").ShowAsync();
                        _index--;
                    }
                }
                else
                {
                    var g = _createProxyPageViewModel.CurrentPage as CreateProxyGuide;
                    var g_vm = g?.DataContext as CreateProxyGuideViewModel;
                    if (g_vm?.SelectedType is not null)
                    {
                        if (g_vm.SelectedType.Category == "Game")
                        {
                            // 根据选择的类型智能筛选节点，并为创建页填充默认值
                            string? preferredProtocol;
                            int defaultLocalPort;
                            var typeName = g_vm.SelectedType.Name;
                            if (typeName.Contains("Java", StringComparison.OrdinalIgnoreCase))
                            {
                                preferredProtocol = "tcp";
                                defaultLocalPort = 25565;
                            }
                            else if (typeName.Contains("Bedrock", StringComparison.OrdinalIgnoreCase))
                            {
                                preferredProtocol = "udp";
                                defaultLocalPort = 19132;
                            }
                            else
                            {
                                // 默认游戏使用 tcp
                                preferredProtocol = "tcp";
                                defaultLocalPort = 25565;
                            }

                            // 尝试获取已加载的节点容器
                            if (_createProxyPageViewModel.pages.TryGetValue("Create", out var ctrl) &&
                                ctrl is NodesContainer nc)
                            {
                                var vm = nc.ViewModel;
                                // 首先查找满足协议、在线且未过载的节点
                                var candidates = vm.AllNodes
                                    .Where(n => n.AllowTypes.Any(t =>
                                                    string.Equals(t, preferredProtocol,
                                                        StringComparison.OrdinalIgnoreCase)) &&
                                                n is { IsOnline: true, IsNotOverloaded: true })
                                    .OrderByDescending(n => n.AllowHighTraffic)
                                    .ThenBy(n => n.LoadPercent)
                                    .ToList();

                                // 如果没有找到，放宽条件允许已过载的节点
                                if (!candidates.Any())
                                {
                                    candidates = vm.AllNodes
                                        .Where(n => n.AllowTypes.Any(t =>
                                                        string.Equals(t, preferredProtocol,
                                                            StringComparison.OrdinalIgnoreCase)) &&
                                                    n.IsOnline)
                                        .OrderByDescending(n => n.AllowHighTraffic)
                                        .ThenBy(n => n.LoadPercent)
                                        .ToList();
                                }

                                if (!candidates.Any())
                                {
                                    await MessageBoxManager.GetMessageBoxStandard("提示", "未找到满足条件的节点，请手动选择").ShowAsync();
                                    _index--;
                                    break;
                                }

                                // 将候选节点排序放到列表前面（改变原有顺序）
                                await Dispatcher.UIThread.InvokeAsync(() =>
                                {
                                    vm.FilteredNodes.Clear();
                                    vm.FilteredNodes.AddRange(candidates);
                                }, DispatcherPriority.Background);

                                // 自动选择第一个节点
                                var chosen = candidates.First();
                                _createProxyPageViewModel.selectedNode = chosen;

                                if (chosen.IsOverloaded)
                                {
                                    await MessageBox.ShowAsync("节点已过载，无法再创建隧道", buttons: [TaskDialogButton.OKButton]);
                                    _index--;
                                    break;
                                }

                                var cp = new CreateProxy(chosen)
                                {
                                    PreferredProtocol = preferredProtocol
                                };

                                // 填充默认设置到 CreateProxy 的 ViewModel
                                cp.ViewModel.ProxyName = $"{g_vm.SelectedType.Name}-{chosen.NodeId}";
                                cp.ViewModel.LocalAddress = "127.0.0.1";
                                cp.ViewModel.LocalPort = defaultLocalPort;
                                // 对于 HTTP/HTTPS，Locations/RemoteAddress 可在引导后由用户进一步修改

                                _createProxyPageViewModel.CurrentPage = cp;
                            }
                            else
                            {
                                // 若未加载节点，回退到加载页
                                await _createProxyPageViewModel.LoadDataAsync();
                                await MessageBoxManager.GetMessageBoxStandard("提示", "正在加载节点，请重试").ShowAsync();
                                _index--;
                            }
                        }
                        else if (g_vm.SelectedType.Category == "Productivity")
                        {
                            if (g_vm.SelectedType.Name == "远程桌面连接")
                            {
                                _targetService = "rdp";
                                var defaultLocalPort = 3389;
                                // 尝试获取已加载的节点容器
                                if (_createProxyPageViewModel.pages.TryGetValue("Create", out var ctrl) &&
                                    ctrl is NodesContainer nc)
                                {
                                    var vm = nc.ViewModel;
                                    // 首先查找满足协议、在线且未过载的节点
                                    var candidates = vm.AllNodes
                                        .Where(n =>
                                        {
                                            var allowTypesLowered = from allowType in n.AllowTypes
                                                select allowType.ToLower();
                                            var typesLowered = allowTypesLowered as string[] ??
                                                               allowTypesLowered.ToArray();
                                            return typesLowered.Contains("tcp") && typesLowered.Contains("udp") &&
                                                   n is { IsOnline: true, IsNotOverloaded: true };
                                        })
                                        .OrderByDescending(n => n.AllowHighTraffic)
                                        .ThenBy(n => n.LoadPercent)
                                        .ToList();

                                    // 如果没有找到，放宽条件允许已过载的节点
                                    if (!candidates.Any())
                                    {
                                        candidates = vm.AllNodes
                                            .Where(n => n.AllowTypes.Any(t =>
                                                            string.Equals(t, "tcp",
                                                                StringComparison.OrdinalIgnoreCase)) &&
                                                        n.IsOnline)
                                            .OrderByDescending(n => n.AllowHighTraffic)
                                            .ThenBy(n => n.LoadPercent)
                                            .ToList();
                                    }

                                    if (!candidates.Any())
                                    {
                                        await MessageBoxManager.GetMessageBoxStandard("提示", "未找到满足条件的节点，请手动选择")
                                            .ShowAsync();
                                        _index--;
                                        break;
                                    }

                                    // 将候选节点排序放到列表前面（改变原有顺序）
                                    await Dispatcher.UIThread.InvokeAsync(() =>
                                    {
                                        vm.FilteredNodes.Clear();
                                        vm.FilteredNodes.AddRange(candidates);
                                    }, DispatcherPriority.Background);

                                    // 自动选择第一个节点
                                    var chosen = candidates.First();
                                    _createProxyPageViewModel.selectedNode = chosen;

                                    if (chosen.IsOverloaded)
                                    {
                                        await MessageBox.ShowAsync("节点已过载，无法再创建隧道",
                                            buttons: [TaskDialogButton.OKButton]);
                                        _index--;
                                        break;
                                    }

                                    var cp = new CreateProxy(chosen)
                                    {
                                        PreferredProtocol = "tcp",
                                    };

                                    // 填充默认设置到 CreateProxy 的 ViewModel
                                    cp.ViewModel.ProxyName = $"{g_vm.SelectedType.Name}-#{chosen.NodeId}";
                                    cp.ViewModel.LocalAddress = "127.0.0.1";
                                    cp.ViewModel.LocalPort = defaultLocalPort;
                                    cp.GetRemotePort_Click(cp.PreferredProtocol, null);
                                    // 对于 HTTP/HTTPS，Locations/RemoteAddress 可在引导后由用户进一步修改

                                    _createProxyPageViewModel.CurrentPage = cp;
                                }
                                else
                                {
                                    // 若未加载节点，回退到加载页
                                    await _createProxyPageViewModel.LoadDataAsync();
                                    await MessageBoxManager.GetMessageBoxStandard("提示", "正在加载节点，请重试").ShowAsync();
                                    _index--;
                                }
                            }
                        }
                        else if (g_vm.SelectedType.Category == "Web")
                        {
                            if (g_vm.SelectedType.Name.Contains("AList"))
                            {
                                var defaultLocalPort = 5244;
                                // 尝试获取已加载的节点容器
                                if (_createProxyPageViewModel.pages.TryGetValue("Create", out var ctrl) &&
                                    ctrl is NodesContainer nc)
                                {
                                    var vm = nc.ViewModel;
                                    // 首先查找满足协议、在线且未过载的节点
                                    var candidates = vm.AllNodes
                                        .Where(n => n.AllowTypes.Contains("tcp") &&
                                                    n is { IsOnline: true, IsNotOverloaded: true } &&
                                                    ParseBandwidthToMbps(n.Bandwidth) >= 100)
                                        .OrderByDescending(n => n.AllowHighTraffic)
                                        .ThenBy(n => n.LoadPercent)
                                        .ToList();

                                    // 如果没有找到，放宽条件允许已过载的节点
                                    if (!candidates.Any())
                                    {
                                        candidates = vm.AllNodes
                                            .Where(n => n.AllowTypes.Any(t =>
                                                            string.Equals(t, "tcp",
                                                                StringComparison.OrdinalIgnoreCase)) &&
                                                        n.IsOnline &&
                                                        ParseBandwidthToMbps(n.Bandwidth) >= 100)
                                            .OrderByDescending(n => n.AllowHighTraffic)
                                            .ThenBy(n => n.LoadPercent)
                                            .ToList();
                                    }

                                    if (!candidates.Any())
                                    {
                                        await MessageBoxManager.GetMessageBoxStandard("提示", "未找到满足条件的节点，请手动选择")
                                            .ShowAsync();
                                        _index--;
                                        break;
                                    }

                                    // 将候选节点排序放到列表前面（改变原有顺序）
                                    await Dispatcher.UIThread.InvokeAsync(() =>
                                    {
                                        vm.FilteredNodes.Clear();
                                        vm.FilteredNodes.AddRange(candidates);
                                    }, DispatcherPriority.Background);

                                    // 自动选择第一个节点
                                    var chosen = candidates.First();
                                    _createProxyPageViewModel.selectedNode = chosen;

                                    if (chosen.IsOverloaded)
                                    {
                                        await MessageBox.ShowAsync("节点已过载，无法再创建隧道",
                                            buttons: [TaskDialogButton.OKButton]);
                                        _index--;
                                        break;
                                    }

                                    var cp = new CreateProxy(chosen)
                                    {
                                        PreferredProtocol = "tcp",
                                    };

                                    // 填充默认设置到 CreateProxy 的 ViewModel
                                    cp.ViewModel.ProxyName = $"{g_vm.SelectedType.Name}-#{chosen.NodeId}";
                                    cp.ViewModel.LocalAddress = "127.0.0.1";
                                    cp.ViewModel.LocalPort = defaultLocalPort;
                                    cp.GetRemotePort_Click(cp.PreferredProtocol, null);
                                    // 对于 HTTP/HTTPS，Locations/RemoteAddress 可在引导后由用户进一步修改

                                    _createProxyPageViewModel.CurrentPage = cp;
                                }
                                else
                                {
                                    // 若未加载节点，回退到加载页
                                    await _createProxyPageViewModel.LoadDataAsync();
                                    await MessageBoxManager.GetMessageBoxStandard("提示", "正在加载节点，请重试").ShowAsync();
                                    _index--;
                                }
                            }
                        }
                    }
                }

                break;
            case 2:
                if (OnCreateProxy is null)
                {
                    _index -= 2;
                    Next(sender, e);
                }

                if (await OnCreateProxy?.Invoke()!)
                {
                    Growl.Success("成功创建隧道");
                    if (_targetService == "rdp")
                    {
                        _targetRequest.proxyType = _targetRequest.proxyType.ToLower() == "tcp" ? "udp" : "tcp";
                        _targetRequest.proxyName = $"{_targetRequest.proxyName}({_targetRequest.proxyType.ToUpper()})";
                        await MEFrpApiConverter.PostNewTunnelAsync(JsonSerializer.Serialize(_targetRequest,
                            App.AppJsonSerializerContext.CreateProxyRequestData));
                        Growl.Success($"成功创建同名{_targetRequest.proxyType.ToUpper()}隧道");
                    }

                    _createProxyPageViewModel.CurrentPage = new OperationSuccess();
                    NextBtn.IsEnabled = false;
                }
                else
                {
                    Growl.Error("创建隧道失败");
                }

                _index--;
                break;
        }
    }

    private async void Back(object sender, RoutedEventArgs e)
    {
        if (_index > 0)
        {
            _index--;
            _createProxyPageViewModel.selectedNode = null;
            NextBtn.IsEnabled = true;
        }

        if (_index == 0)
        {
            _createProxyPageViewModel.selectedNode = null;
            if (_createProxyPageViewModel.SelectedType == 0) //引导模式
            {
                if (_createProxyPageViewModel.pages.TryGetValue("Guide", out var control))
                {
                    _createProxyPageViewModel.CurrentPage = control as CreateProxyGuide;
                }
                else
                {
                    var page = new CreateProxyGuide()
                    {
                        DataContext = new CreateProxyGuideViewModel()
                    };
                    _createProxyPageViewModel.pages["Guide"] = page;
                    _createProxyPageViewModel.CurrentPage = page;
                }
            }
            else if (_createProxyPageViewModel.SelectedType is 2) //地图模式
            {
                if (_createProxyPageViewModel.pages.TryGetValue("MapLegacy", out var control))
                {
                    _createProxyPageViewModel.CurrentPage = control as MappedNodesContainerLegacy;
                }
                else
                {
                    var page = new MappedNodesContainerLegacy();
                    _createProxyPageViewModel.pages["MapLegacy"] = page;
                    _createProxyPageViewModel.CurrentPage = page;
                }
            }
            // else if (_createProxyPageViewModel.SelectedType is 3) //地图模式
            // {
            //     if (_createProxyPageViewModel.pages.TryGetValue("Map", out var control))
            //     {
            //         _createProxyPageViewModel.CurrentPage = control as MappedNodesContainer;
            //     }
            //     else
            //     {
            //         var page = new MappedNodesContainer();
            //         _createProxyPageViewModel.pages["Map"] = page;
            //         _createProxyPageViewModel.CurrentPage = page;
            //     }
            // }
            else
            {
                // 为避免虚拟化面板复用带来的布局问题，始终创建新的 NodesContainer 并加载数据
                await _createProxyPageViewModel.LoadDataAsync(true);
            }
        }

        _isMap = false;
    }

    private async void Refresh(object? sender, RoutedEventArgs e)
    {
        await MEFrpApiConverter.EnsureNodesListInfoAsync();
        await MEFrpApiConverter.EnsureNodesStatusInfoAsync();
        switch (_index)
        {
            case 0: 
                await _createProxyPageViewModel.LoadDataAsync();
                break;
            case 1:
                switch (_createProxyPageViewModel.SelectedType)
                {
                    //专家模式(原版)
                    case 1:
                    {
                        if (_createProxyPageViewModel.pages.TryGetValue("Create", out var ctrl) &&
                            ctrl is NodesContainer nc)
                        {
                            await nc.ViewModel.LoadNodesAsync(MEFrpApiConverter.CurrentNodesListInfo,
                                MEFrpApiConverter.CurrentNodesStatusInfo);
                        }

                        break;
                    }
                    //地图模式（嘉豪原版/嘉豪版）
                    case 2 or 3:
                    {
                        if (_createProxyPageViewModel.pages.TryGetValue("Create", out var ctrl) &&
                            ctrl is INodeContainer nc)
                        {
                            await nc.ViewModel.LoadNodesAsync(MEFrpApiConverter.CurrentNodesListInfo,
                                MEFrpApiConverter.CurrentNodesStatusInfo);
                        }

                        break;
                    }
                    default:
                        await _createProxyPageViewModel.LoadDataAsync(true);
                        break;
                }

                break;
            case 2:
                break;
        }
        //await _createProxyPageViewModel.LoadDataAsync();
    }

    /// <summary>
    /// 将带宽字符串（如 "5Mbps", "10Gbps"）转换为 Mbps 值
    /// </summary>
    /// <param name="bandwidth">带宽字符串，支持格式: Mbps, Gbps, Kbps, bps</param>
    /// <returns>带宽值（单位：Mbps）</returns>
    public static long ParseBandwidthToMbps(string bandwidth)
    {
        if (string.IsNullOrWhiteSpace(bandwidth))
            throw new ArgumentException("带宽字符串不能为空", nameof(bandwidth));

        bandwidth = bandwidth.Trim();

        // 提取数字部分和单位部分
        var match = BandWidthRegex().Match(bandwidth);

        if (!match.Success)
            throw new FormatException($"无效的带宽格式: {bandwidth}。支持的格式: 5Mbps, 10Gbps, 100Kbps, 1000bps");

        double value = double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        string unit = match.Groups[2].Value.ToLowerInvariant();

        // 根据单位转换为 Mbps
        return unit switch
        {
            "bps" => (long)(value / 1_000_000), // 1 Mbps = 1,000,000 bps
            "kbps" => (long)(value / 1_000), // 1 Mbps = 1,000 Kbps
            "mbps" => (long)value, // 已经是 Mbps
            "gbps" => (long)(value * 1_000), // 1 Gbps = 1,000 Mbps
            _ => throw new NotSupportedException($"不支持的单位: {unit}")
        };
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^(\d+(?:\.\d+)?)\s*(kbps|mbps|gbps|bps)$",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase, "zh-CN")]
    private static partial System.Text.RegularExpressions.Regex BandWidthRegex();

    /// <summary>
    /// 根据区域名称筛选节点
    /// </summary>
    /// <param name="allNodes">所有节点</param>
    /// <param name="areaName">区域名称（省份/大洲）</param>
    /// <param name="isChinaMap">是否为中国地图模式</param>
    /// <returns>筛选后的节点列表</returns>
    private List<TunnelNodeViewModel> FilterNodesByArea(List<TunnelNodeViewModel> allNodes, string areaName,
        bool isChinaMap)
    {
        var candidates = allNodes.Where(n =>
            {
                // 当显示世界地图时，如果节点region是cn或cnos，归类为亚洲
                if (!isChinaMap && n.Region is "cn" or "cnos")
                {
                    return areaName == "亚洲" || areaName == "Asia";
                }

                // 清理节点名称
                var cleanName = n.Name.Split('/')[0].Trim()
                    .ReplaceAnyToOne("①②③④⑤⑥⑦⑧⑨⑩⑪⑫⑬⑭⑮⑯⑰⑱⑲⑳㉑㉒㉓㉔㉕㉖㉗㉘㉙㉚㉛㉜㉝㉞㉟㊱㊲㊳㊴㊵㊶㊷㊸㊹㊺㊻㊼㊽㊾㊿".Select(c => c.ToString()))
                    .Trim();

                // 原有判断逻辑
                return n.Region is "cn" or "cnos"
                    ? n.Name.Contains(areaName) ||
                      (ChineseRegionService.CityToProvince.TryGetValue(cleanName, out var province) &&
                       province.Contains(areaName))
                    : WorldRegionService.CountriesToContinent.TryGetValue(cleanName, out var countries) &&
                    countries.Contains(areaName) || WorldRegionService.WellKnownCitiesToContinent.TryGetValue(
                        cleanName, out var city) &&
                    city.Contains(areaName);
            })
            .Where(n => n.IsOnline) // 只选择在线且未过载的节点
            .OrderByDescending(n => n.AllowHighTraffic)
            .ThenBy(n => n.LoadPercent)
            .ToList();

        // 如果没有找到符合条件的节点，放宽条件允许已过载的节点
        if (!candidates.Any())
        {
            candidates = allNodes.Where(n =>
                {
                    if (!isChinaMap && n.Region is "cn" or "cnos")
                    {
                        return areaName == "亚洲" || areaName == "Asia";
                    }

                    var cleanName = n.Name.Split('/')[0].Trim()
                        .ReplaceAnyToOne("①②③④⑤⑥⑦⑧⑨⑩⑪⑫⑬⑭⑮⑯⑰⑱⑲⑳㉑㉒㉓㉔㉕㉖㉗㉘㉙㉚㉛㉜㉝㉞㉟㊱㊲㊳㊴㊵㊶㊷㊸㊹㊺㊻㊼㊽㊾㊿".Select(c => c.ToString()))
                        .Trim();

                    return n.Region is "cn" or "cnos"
                        ? n.Name.Contains(areaName) ||
                          (ChineseRegionService.CityToProvince.TryGetValue(cleanName, out var province) &&
                           province.Contains(areaName))
                        : WorldRegionService.CountriesToContinent.TryGetValue(cleanName, out var countries) &&
                        countries.Contains(areaName) || WorldRegionService.WellKnownCitiesToContinent.TryGetValue(
                            cleanName, out var city) &&
                        city.Contains(areaName);
                })
                .Where(n => n.IsOnline) // 只要求在线
                .OrderByDescending(n => n.AllowHighTraffic)
                .ThenBy(n => n.LoadPercent)
                .ToList();
        }

        return candidates;
    }
}