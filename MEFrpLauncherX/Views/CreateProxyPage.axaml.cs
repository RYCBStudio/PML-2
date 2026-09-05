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
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Core.MEFIntegrated;
using MEFrpLauncherX.Core.Services;
using MEFrpLauncherX.Models;
using MEFrpLauncherX.Plugin.Core;
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
    internal InfoClasses.CreateProxyRequestData _targetRequest;

    // 引导模式（SelectedType==0）向导状态：与 _index 整数流程隔离（专家/地图仍用 _index）
    private GuideStage _guideStage = GuideStage.PickTemplate;
    private readonly NodesContainerViewModel _guideNodesVm;
    private TunnelNodeViewModel? _guideSelectedNode;
    private ProxyType? _activeGuideTemplate;
    private ProxyTemplateExtraTunnelDefinition? _activeExtraTunnel;
    private CreateProxy? _activeCreateProxyPage;

    public CreateProxyPage()
    {
        InitializeComponent();
        _createProxyPageViewModel = new CreateProxyPageViewModel();
        _index = 0;
        Instance = this;
        DataContext = _createProxyPageViewModel;

        // 引导候选节点容器：独立 VM，只更新 _guideSelectedNode，避免污染专家/地图模式的选中态
        _guideNodesVm = new NodesContainerViewModel();
        _guideNodesVm.NodeSelected += node => _guideSelectedNode = node;

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
        // 引导模式（SelectedType==0）走独立向导（GuideStage），不参与 _index 整数状态机
        if (_createProxyPageViewModel.SelectedType == 0)
        {
            await HandleGuideNextAsync();
            return;
        }

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
                            await MessageBox.ShowAsync(Languages.Text_CreateProxy_NodeOverloaded, "", MessageBoxIcon.Error);
                            _index--;
                            break;
                        }

                        var cp = new CreateProxy(_createProxyPageViewModel.selectedNode);
                        _createProxyPageViewModel.CurrentPage = cp;
                    }
                    else
                    {
                        await MessageBoxManager.GetMessageBoxStandard(Languages.Caption_Error, Languages.Text_CreateProxy_SelectANode).ShowAsync();
                        _index--;
                    }
                }
                else if (_createProxyPageViewModel.SelectedType is 2 or 3) // 地图模式（嘉豪原版/嘉豪版）
                {
                    // 检查是否有选中的区域
                    var selectedArea = _createProxyPageViewModel.GetSelectedAreaFromMap();

                    if (string.IsNullOrEmpty(selectedArea))
                    {
                        await MessageBoxManager.GetMessageBoxStandard(Languages.Caption_Hint, Languages.Text_CreateProxy_SelectAreaOnMap).ShowAsync();
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
                            await MessageBox.ShowAsync(string.Format(Languages.Text_CreateProxy_NoNodesInAreaFormat, selectedArea), Languages.Caption_Hint, ButtonEnum.Ok);
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
                            await MessageBox.ShowAsync(Languages.Text_CreateProxy_NodeOverloaded, buttons: [TaskDialogButton.OKButton]);
                            _index--;
                            break;
                        }

                        _index--;
                    }
                    else
                    {
                        // 若未加载节点，回退到加载页
                        await _createProxyPageViewModel.LoadDataAsync();
                        await MessageBoxManager.GetMessageBoxStandard(Languages.Caption_Hint, Languages.Text_CreateProxy_LoadingNodesRetry).ShowAsync();
                        _index--;
                    }
                }
                // 引导模式已迁移为 GuideStage 向导（见 HandleGuideNextAsync），此旧分支保留仅供对照、不可达
#if false
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
                                    await MessageBoxManager.GetMessageBoxStandard(Languages.Caption_Hint, Languages.Text_CreateProxy_NoMatchingNodes).ShowAsync();
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
                                    await MessageBox.ShowAsync(Languages.Text_CreateProxy_NodeOverloaded, buttons: [TaskDialogButton.OKButton]);
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
                                await MessageBoxManager.GetMessageBoxStandard(Languages.Caption_Hint, Languages.Text_CreateProxy_LoadingNodesRetry).ShowAsync();
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
                                        await MessageBoxManager.GetMessageBoxStandard(Languages.Caption_Hint, Languages.Text_CreateProxy_NoMatchingNodes)
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
                                        await MessageBox.ShowAsync(Languages.Text_CreateProxy_NodeOverloaded,
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
                                    await MessageBoxManager.GetMessageBoxStandard(Languages.Caption_Hint, Languages.Text_CreateProxy_LoadingNodesRetry).ShowAsync();
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
                                        await MessageBoxManager.GetMessageBoxStandard(Languages.Caption_Hint, Languages.Text_CreateProxy_NoMatchingNodes)
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
                                        await MessageBox.ShowAsync(Languages.Text_CreateProxy_NodeOverloaded,
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
                                    await MessageBoxManager.GetMessageBoxStandard(Languages.Caption_Hint, Languages.Text_CreateProxy_LoadingNodesRetry).ShowAsync();
                                    _index--;
                                }
                            }
                        }
                    }
                }
#endif

                break;
            case 2:
                if (OnCreateProxy is null)
                {
                    _index -= 2;
                    Next(sender, e);
                }

                if (await OnCreateProxy?.Invoke()!)
                {
                    Growl.Success(Languages.Text_CreateProxy_CreateSuccess);
                    // 副隧道（如 RDP 双隧道）已由引导向导在 HandleGuideNextAsync 中统一处理
                    _createProxyPageViewModel.CurrentPage = new OperationSuccess();
                    NextBtn.IsEnabled = false;
                }
                else
                {
                    Growl.Error(Languages.Text_CreateProxy_CreateFailed);
                }

                _index--;
                break;
        }
    }

    private async void Back(object sender, RoutedEventArgs e)
    {
        // 引导模式回退走独立向导（GuideStage）
        if (_createProxyPageViewModel.SelectedType == 0)
        {
            await HandleGuideBackAsync();
            return;
        }

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

    // ============ 引导模式（SelectedType==0）向导：PickTemplate → PickNode → FillForm → Submit ============

    /// <summary>引导向导：下一步</summary>
    private async Task HandleGuideNextAsync()
    {
        // 若已离开向导中途（如提交成功后切走再回来停在类型页），重置回类型选择
        if (_guideStage != GuideStage.PickTemplate && _createProxyPageViewModel.CurrentPage is CreateProxyGuide)
        {
            _guideStage = GuideStage.PickTemplate;
            _guideSelectedNode = null;
        }

        switch (_guideStage)
        {
            case GuideStage.PickTemplate:
            {
                var g = _createProxyPageViewModel.CurrentPage as CreateProxyGuide;
                var gVm = g?.DataContext as CreateProxyGuideViewModel;
                var tpl = gVm?.SelectedType;
                if (tpl?.SourceTemplate is null)
                {
                    await MessageBox.ShowAsync(Languages.Text_Proxy_Guide_SelectTemplate, Languages.Caption_Hint,
                        ButtonEnum.Ok);
                    return;
                }

                // 候选筛选基于已加载节点数据；未就绪时先加载（加载后恢复引导页展示）
                if (!TryGetLoadedNodes(out var allNodes))
                {
                    await _createProxyPageViewModel.LoadDataAsync();
                    if (!TryGetLoadedNodes(out allNodes))
                    {
                        await MessageBox.ShowAsync(Languages.Text_CreateProxy_LoadingNodesRetry, Languages.Caption_Hint,
                            ButtonEnum.Ok);
                        return;
                    }

                    // LoadDataAsync 会把 CurrentPage 切到节点容器，恢复引导类型页
                    ShowGuideTypePage();
                }

                var candidates = FilterGuideCandidates(tpl.SourceTemplate.NodeFilter, allNodes);
                if (candidates.Count == 0)
                {
                    await MessageBox.ShowAsync(Languages.Text_CreateProxy_NoMatchingNodes, Languages.Caption_Hint,
                        ButtonEnum.Ok);
                    return;
                }

                _activeGuideTemplate = tpl;
                _activeExtraTunnel = null;
                ShowGuideCandidatePage(candidates);
                _guideStage = GuideStage.PickNode;
                break;
            }

            case GuideStage.PickNode:
            {
                var node = _guideSelectedNode;
                if (node is null || _activeGuideTemplate?.SourceTemplate is null)
                {
                    await MessageBox.ShowAsync(Languages.Text_CreateProxy_SelectANode, Languages.Caption_Hint,
                        ButtonEnum.Ok);
                    return;
                }

                _activeCreateProxyPage = BuildCreateProxyFromTemplate(node, _activeGuideTemplate.SourceTemplate);
                _activeExtraTunnel = _activeGuideTemplate.SourceTemplate.ExtraTunnel;
                _createProxyPageViewModel.CurrentPage = _activeCreateProxyPage;
                _guideStage = GuideStage.FillForm;
                break;
            }

            case GuideStage.FillForm:
            {
                if (OnCreateProxy is null)
                {
                    return;
                }

                var created = await OnCreateProxy.Invoke();
                if (!created)
                {
                    Growl.Error(Languages.Text_CreateProxy_CreateFailed);
                    return; // 停留在表单
                }

                Growl.Success(Languages.Text_CreateProxy_CreateSuccess);
                await CreateExtraTunnelIfDeclaredAsync();
                _createProxyPageViewModel.CurrentPage = new OperationSuccess();
                NextBtn.IsEnabled = false;
                _guideStage = GuideStage.Submit;
                break;
            }

            case GuideStage.Submit:
                break;
        }
    }

    /// <summary>引导向导：上一步（Submit→FillForm→PickNode→PickTemplate）</summary>
    private async Task HandleGuideBackAsync()
    {
        switch (_guideStage)
        {
            case GuideStage.Submit:
                // 从成功页返回表单
                _guideStage = GuideStage.FillForm;
                NextBtn.IsEnabled = true;
                if (_activeCreateProxyPage != null)
                {
                    _createProxyPageViewModel.CurrentPage = _activeCreateProxyPage;
                }

                break;
            case GuideStage.FillForm:
                // 返回候选容器页，允许改选节点
                if (_createProxyPageViewModel.pages.TryGetValue("GuidePick", out var pick))
                {
                    _createProxyPageViewModel.CurrentPage = pick as Control;
                }

                _guideStage = GuideStage.PickNode;
                break;
            case GuideStage.PickNode:
                // 返回类型页并清空选择
                _guideSelectedNode = null;
                _guideNodesVm.SelectedNode = null;
                ShowGuideTypePage();
                _guideStage = GuideStage.PickTemplate;
                break;
            case GuideStage.PickTemplate:
                break;
        }

        await Task.CompletedTask;
    }

    /// <summary>取共享已加载的节点全量（pages["Create"] 的 NodesContainer）</summary>
    private bool TryGetLoadedNodes(out List<TunnelNodeViewModel> allNodes)
    {
        allNodes = [];
        if (_createProxyPageViewModel.pages.TryGetValue("Create", out var ctrl) &&
            ctrl is NodesContainer nc &&
            nc.ViewModel.AllNodes.Count > 0)
        {
            allNodes = nc.ViewModel.AllNodes;
            return true;
        }

        return false;
    }

    /// <summary>显示引导类型页（缓存优先）</summary>
    private Control ShowGuideTypePage()
    {
        if (_createProxyPageViewModel.pages.TryGetValue("Guide", out var control) && control is CreateProxyGuide)
        {
            _createProxyPageViewModel.CurrentPage = control as Control;
            return control as Control;
        }

        var page = new CreateProxyGuide { DataContext = new CreateProxyGuideViewModel() };
        _createProxyPageViewModel.pages["Guide"] = page;
        _createProxyPageViewModel.CurrentPage = page;
        return page;
    }

    /// <summary>把候选节点写入引导专用容器并展示（PickNode 步）</summary>
    private void ShowGuideCandidatePage(List<TunnelNodeViewModel> candidates)
    {
        _guideNodesVm.AllNodes = candidates;
        _guideNodesVm.FilteredNodes.Clear();
        _guideNodesVm.FilteredNodes.AddRange(candidates);
        _guideNodesVm.SelectedNode = null;
        _guideSelectedNode = null;

        if (_createProxyPageViewModel.pages.TryGetValue("GuidePick", out var ctrl) && ctrl is NodesContainerCompact)
        {
            _createProxyPageViewModel.CurrentPage = ctrl as Control;
            return;
        }

        var page = new NodesContainerCompact(_guideNodesVm);
        _createProxyPageViewModel.pages["GuidePick"] = page;
        _createProxyPageViewModel.CurrentPage = page;
    }

    /// <summary>
    /// 按模板 nodeFilter 筛选候选节点：
    /// 首轮 = 在线 + 未过载 + 支持全部 protocols（+带宽下限）；无候选则放宽轮 = 在线 + 支持 fallbackProtocols（+带宽下限）。
    /// 排序：AllowHighTraffic 降序、LoadPercent 升序。
    /// </summary>
    private List<TunnelNodeViewModel> FilterGuideCandidates(ProxyTemplateNodeFilterDefinition filter,
        List<TunnelNodeViewModel> allNodes)
    {
        var protocols = filter.Protocols ?? [];
        var fallback = filter.FallbackProtocols is { Count: > 0 } ? filter.FallbackProtocols : protocols;
        var minBw = filter.MinBandwidthMbps;

        bool SupportsAll(List<string> need, TunnelNodeViewModel n) =>
            need.Count == 0 || need.All(proto => n.AllowTypes.Any(t =>
                string.Equals(t, proto, StringComparison.OrdinalIgnoreCase)));

        bool MeetsBandwidth(TunnelNodeViewModel n) =>
            minBw <= 0 ||
            (!string.IsNullOrWhiteSpace(n.Bandwidth) && ParseBandwidthToMbps(n.Bandwidth) >= minBw);

        static IEnumerable<TunnelNodeViewModel> Ranked(IEnumerable<TunnelNodeViewModel> source) =>
            source.OrderByDescending(n => n.AllowHighTraffic).ThenBy(n => n.LoadPercent);

        var primary = Ranked(allNodes.Where(n =>
            n.IsOnline && n.IsNotOverloaded && SupportsAll(protocols, n) && MeetsBandwidth(n))).ToList();
        if (primary.Count > 0)
        {
            return primary;
        }

        return Ranked(allNodes.Where(n =>
            n.IsOnline && SupportsAll(fallback, n) && MeetsBandwidth(n))).ToList();
    }

    /// <summary>按模板 create 声明构建并预填创建表单（FillForm 步）</summary>
    private CreateProxy BuildCreateProxyFromTemplate(TunnelNodeViewModel node, ProxyTemplateDefinition tpl)
    {
        var create = tpl.Create ?? new ProxyTemplateCreateDefinition();
        var cp = new CreateProxy(node)
        {
            PreferredProtocol = create.Protocol
        };

        var displayName = _activeGuideTemplate?.Name ?? tpl.Name;
        cp.ViewModel.ProxyName = (create.ProxyName ?? "")
            .Replace("{name}", displayName, StringComparison.Ordinal)
            .Replace("{nodeId}", node.NodeId.ToString());
        cp.ViewModel.LocalAddress = string.IsNullOrWhiteSpace(create.LocalAddress) ? "127.0.0.1" : create.LocalAddress;
        if (create.LocalPort > 0)
        {
            cp.ViewModel.LocalPort = create.LocalPort;
        }

        if (string.Equals(create.RemotePort, "auto", StringComparison.OrdinalIgnoreCase))
        {
            cp.GetRemotePort_Click(string.IsNullOrEmpty(create.Protocol) ? "tcp" : create.Protocol, null);
        }
        else if (int.TryParse(create.RemotePort, out var fixedPort))
        {
            cp.ViewModel.RemotePort = fixedPort;
        }

        return cp;
    }

    /// <summary>
    /// 主隧道创建成功后按模板 extraTunnel 声明补建互补协议隧道（RDP 双隧道）：
    /// 翻转 proxyType（tcp↔udp），名称追加 nameSuffix（{PROTO}→翻转协议大写）。
    /// </summary>
    private async Task CreateExtraTunnelIfDeclaredAsync()
    {
        if (_activeExtraTunnel is null || _targetRequest is null)
        {
            return;
        }

        var proto = _targetRequest.proxyType?.ToLower();
        var flipped = proto switch
        {
            "tcp" => "udp",
            "udp" => "tcp",
            _ => null
        };
        if (flipped is null)
        {
            return;
        }

        var extra = CloneRequest(_targetRequest);
        extra.proxyType = flipped;
        var suffix = (_activeExtraTunnel.NameSuffix ?? "").Replace("{PROTO}", flipped.ToUpper(), StringComparison.Ordinal);
        extra.proxyName = _targetRequest.proxyName + suffix;

        var body = JsonSerializer.Serialize(extra, App.AppJsonSerializerContext.CreateProxyRequestData);
        if ((await MEFrpApiConverter.PostNewTunnelAsync(body)).code == 200)
        {
            Growl.Success(string.Format(Languages.Text_CreateProxy_CreateSameNameSuccessFormat, flipped.ToUpper()));
        }
    }

    /// <summary>深拷贝创建请求（经由项目 AOT JSON 上下文）</summary>
    private static InfoClasses.CreateProxyRequestData CloneRequest(InfoClasses.CreateProxyRequestData source)
        => JsonSerializer.Deserialize(
            JsonSerializer.Serialize(source, App.AppJsonSerializerContext.CreateProxyRequestData),
            App.AppJsonSerializerContext.CreateProxyRequestData)!;

    private async void Refresh(object? sender, RoutedEventArgs e)
    {
        if (_createProxyPageViewModel.SelectedType == 0)
        {
            // 引导模式：仅刷新模板条目（候选节点在下次进入 PickNode 时按当前模板重筛）
            _createProxyPageViewModel.RefreshGuideIfPresent();
            return;
        }

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

/// <summary>引导模式（SelectedType==0）向导阶段：类型 → 候选节点 → 表单 → 提交</summary>
public enum GuideStage
{
    PickTemplate,
    PickNode,
    FillForm,
    Submit
}