using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using MEFrpLauncherX.Controls;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.MEFIntergrated;
using MEFrpLauncherX.Views;
using ReactiveUI;

namespace MEFrpLauncherX.ViewModels;

public class CreateProxyPageViewModel : ViewModelBase
{
    internal Dictionary<string, Control> pages = new();

    internal TunnelNodeViewModel? selectedNode;
    private void CreateProxyPage_NodeSelected(TunnelNodeViewModel? obj) => selectedNode = obj;

    /// <summary>
    /// 获取当前地图视图中选中的区域名称（仅当 SelectedType == 2 或 3 时有效）
    /// </summary>
    public string? GetSelectedAreaFromMap()
    {
        if (SelectedType == 2 && pages.TryGetValue("MapLegacy", out var legacyMap) && legacyMap is MappedNodesContainerLegacy legacy)
        {
            return legacy.SelectedAreaName;
        }
        else if (SelectedType == 3 && pages.TryGetValue("Map", out var newMap) && newMap is MappedNodesContainer newMapControl)
        {
            // 如果新的地图控件也有类似的属性，可以在这里添加
            // return newMapControl.SelectedAreaName;
        }
        return null;
    }

    private bool _isPageLoading;
    public bool IsPageLoading
    {
        get => _isPageLoading;
        set => this.RaiseAndSetIfChanged(ref _isPageLoading, value);
    }

    public Control? CurrentPage
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
        }
    }

    /// <summary>
    /// 异步加载专家版数据：先显示 ProgressRing，再加载数据
    /// </summary>
    public async Task LoadDataAsync()
    {
        if (Design.IsDesignMode) return;

        IsPageLoading = true;
        try
        {
            // 先让 UI 渲染 loading 状态
            await Task.Yield();

            var nc = new NodesContainer();
            pages["Create"] = nc;

            // 立即显示节点容器（带 loading）
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                CurrentPage = nc;
            }, DispatcherPriority.Render);

            // 并行加载状态和列表数据
            var statusTask = MEFrpApiConverter.EnsureNodesStatusInfoAsync();
            var listInfoTask = MEFrpApiConverter.EnsureNodesListInfoAsync();
            await Task.WhenAll(statusTask, listInfoTask);

            var status = statusTask.Result;
            var listInfo = listInfoTask.Result;

            await nc.LoadNodesAsync(listInfo, status);
            (nc.DataContext as NodesContainerViewModel)!.NodeSelected += CreateProxyPage_NodeSelected;
        }
        finally
        {
            IsPageLoading = false;
            MainPageFrameViewModel.Instance.IsLoading = false;
        }
    }

    private int _selectedType = 1;
    public int SelectedType
    {
        get => _selectedType;
        set
        {
            if (_selectedType == value) return;
            this.RaiseAndSetIfChanged(ref _selectedType, value);
            _ = SwitchToTabAsync(value);
        }
    }

    /// <summary>
    /// 异步切换 Tab，避免 UI 假死
    /// </summary>
    private async Task SwitchToTabAsync(int tabIndex)
    {
        // 先让 UI 响应 Tab 切换
        await Task.Yield();

        switch (tabIndex)
        {
            case 0: // 引导版
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (pages.TryGetValue("Guide", out var control))
                    {
                        CurrentPage = control;
                    }
                    else
                    {
                        var page = new CreateProxyGuide()
                        {
                            DataContext = new CreateProxyGuideViewModel()
                        };
                        pages["Guide"] = page;
                        CurrentPage = page;
                    }
                }, DispatcherPriority.Render);
                break;

            case 1: // 专家版
                if (pages.TryGetValue("Create", out var existingCreate))
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        CurrentPage = existingCreate;
                    }, DispatcherPriority.Render);
                }
                else
                {
                    IsPageLoading = true;
                    try
                    {
                        await LoadDataAsync();
                    }
                    finally
                    {
                        IsPageLoading = false;
                    }
                }
                break;

            case 2: // 嘉豪版（旧地图）
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (pages.TryGetValue("MapLegacy", out var control))
                    {
                        CurrentPage = control;
                    }
                    else
                    {
                        IsPageLoading = true;
                        // 创建后让 UI 渲染 loading
                        Dispatcher.UIThread.Post(async () =>
                        {
                            await Task.Yield();
                            var page = new MappedNodesContainerLegacy();
                            pages["MapLegacy"] = page;
                            CurrentPage = page;
                            IsPageLoading = false;
                        }, DispatcherPriority.Background);
                    }
                }, DispatcherPriority.Render);
                break;

            case 3: // 嘉豪版（新地图）
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (pages.TryGetValue("Map", out var control))
                    {
                        CurrentPage = control;
                    }
                    else
                    {
                        IsPageLoading = true;
                        Dispatcher.UIThread.Post(async () =>
                        {
                            await Task.Yield();
                            var page = new MappedNodesContainer();
                            pages["Map"] = page;
                            CurrentPage = page;
                            IsPageLoading = false;
                        }, DispatcherPriority.Background);
                    }
                }, DispatcherPriority.Render);
                break;
        }
    }

    /// <summary>
    /// 在后台预热 Tab 页面，减少切换延迟
    /// </summary>
    public void PrewarmTabs()
    {
        // 预热引导版（轻量）
        if (!pages.ContainsKey("Guide"))
        {
            Dispatcher.UIThread.Post(() =>
            {
                pages["Guide"] = new CreateProxyGuide()
                {
                    DataContext = new CreateProxyGuideViewModel()
                };
            }, DispatcherPriority.Background);
        }
    }

    public CreateProxyPageViewModel()
    {
        _selectedType = 1;
        // 异步触发初始加载，不阻塞构造函数
        _ = SwitchToTabAsync(1);
    }
}