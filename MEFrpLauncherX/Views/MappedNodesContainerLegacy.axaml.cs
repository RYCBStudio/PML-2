using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.MEFIntegrated;
using MEFrpLauncherX.Core.MEFIntergrated;
using MEFrpLauncherX.Core.Services;

namespace MEFrpLauncherX.Views;

public partial class MappedNodesContainerLegacy : UserControl
{
    private Path? _selectedArea;
    
    /// <summary>
    /// 获取当前选中的区域名称（点击锁定后）
    /// </summary>
    public string? SelectedAreaName { get; private set; }

    private static IBrush GetBrushFromTheme(bool isActive = true)
    {
        var b = isActive ? Application.Current.TryGetResource("AccentFillColorDefaultBrush",
                Application.Current.ActualThemeVariant, out var brush)
                ? brush as Brush
                : Application.Current.TryGetResource("SurfaceStrokeColorDefaultBrush",
                    Application.Current.ActualThemeVariant, out var brush2)
                    ? brush2 as Brush
                    : null
            : Application.Current.TryGetResource("SurfaceStrokeColorDefaultBrush",
                Application.Current.ActualThemeVariant, out var brush3) ? brush3 as Brush
            : Application.Current.TryGetResource("SurfaceStrokeColorDefaultBrush",
                Application.Current.ActualThemeVariant, out var brush4) ? brush4 as Brush
            : null;
        return b;
    }

    private void OnPointerEntered_China(object? sender, PointerEventArgs e)
    {
        var b = GetBrushFromTheme();
        ChinaMainland.Stroke = b;
        ChineseTaiwan.Stroke = b;
        ChinaMainland.StrokeThickness = 1.5;
        ChineseTaiwan.StrokeThickness = 1.5;
        ChineseHongkongSAR.Stroke = b;
        ChineseHongkongSAR.StrokeThickness = 1.5;
        ChineseMacauSAR.Stroke = b;
        ChineseMacauSAR.StrokeThickness = 1.5;
    }

    private void OnPointerExited_China(object? sender, PointerEventArgs e)
    {
        var b = GetBrushFromTheme(false);
        ChinaMainland.Stroke = b;
        ChineseTaiwan.Stroke = b;
        ChinaMainland.StrokeThickness = 1.0;
        ChineseTaiwan.StrokeThickness = 1.0;
        ChineseHongkongSAR.Stroke = b;
        ChineseHongkongSAR.StrokeThickness = 1.0;
        ChineseMacauSAR.StrokeThickness = 1.0;
        ChineseMacauSAR.Stroke = b;
    }

// 存储每个大洲/省对应的所有 Path 控件
    private readonly Dictionary<string, List<Path>> _continentOrProvincePaths = new();

    // 存储原始颜色/描边信息，用于恢复
    private readonly Dictionary<Path, (IBrush? Brush, double Thickness)> _originalState = new();

    private bool _initialized;

    public MappedNodesContainerLegacy()
    {
        InitializeComponent();
        // 延迟到控件附加到可视化树后才初始化 Path 扫描，避免阻塞页面切换
        AttachedToVisualTree += OnAttachedToVisualTree;
    }

    private async void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        AttachedToVisualTree -= OnAttachedToVisualTree;
        if (_initialized) return;
        _initialized = true;

        // 让 UI 先渲染
        await Task.Yield();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            InitializeContinentGroups();
            AttachPointerEvents();
        }, DispatcherPriority.Background);
    }

    private void InitializeContinentGroups()
    {
        // 找到当前 UserControl 下所有 Path 控件
        var allPaths = FindAllPaths(this);

        foreach (var path in allPaths)
        {
            var continentOrProvince = path.Tag as string;
            if (string.IsNullOrEmpty(continentOrProvince)) continue;

            if (!_continentOrProvincePaths.ContainsKey(continentOrProvince))
                _continentOrProvincePaths[continentOrProvince] = [];

            _continentOrProvincePaths[continentOrProvince].Add(path);

            // 保存原始状态
            _originalState[path] = (path.Stroke, path.StrokeThickness);
        }
    }

    private static List<Path> FindAllPaths(Control parent)
    {
        var result = new List<Path>();
        var children = parent.GetLogicalChildren();

        foreach (var child in children)
        {
            if (child is Path path)
                result.Add(path);
            else if (child is Control control)
                result.AddRange(FindAllPaths(control));
        }

        return result;
    }

    private void AttachPointerEvents()
    {
        foreach (var path in _continentOrProvincePaths.SelectMany(kvp => kvp.Value))
        {
            path.Tag = (path.Tag?.ToString() ?? string.Empty, false);
            path.PointerEntered += OnPathPointerEnter;
            path.PointerPressed += OnPathPointerPressed;
            //path.PointerReleased += OnPathPointerEnter;
            path.PointerExited += OnPathPointerExited;
        }
    }


    // 缓存节点数量，避免每次 hover 都调用 API
    private InfoClasses.NodesListInfo? _cachedNodesListInfo;
    private bool _nodesListLoaded;

    private async void OnPathPointerEnter(object? sender, PointerEventArgs e)
    {
        if (sender is Path path)
        {
            var (areaName, isClicked) = ((string, bool))(path.Tag ?? (string.Empty, false));

            if (!string.IsNullOrEmpty(areaName) && _continentOrProvincePaths.TryGetValue(areaName, out var paths))
            {
                var highlightBrush = GetBrushFromTheme(true);
                foreach (var p in paths)
                {
                    p.Stroke = highlightBrush;
                    p.StrokeThickness = 1.5;
                }

                // 延迟加载节点列表（仅首次）
                if (!_nodesListLoaded)
                {
                    _nodesListLoaded = true;
                    await MEFrpApiConverter.EnsureNodesListInfoAsync();
                    _cachedNodesListInfo = MEFrpApiConverter.CurrentNodesListInfo;
                }

                // 根据当前选中的地图类型使用不同的判断逻辑
                var isChinaMap = Option?.SelectedIndex == 0;
                var nodesList = _cachedNodesListInfo?.NodesList;
                var nodeCount = nodesList?.Count(n =>
                {
                    // 当显示世界地图时，如果节点region是cn或cnos，归类为亚洲
                    if (!isChinaMap && n.region is "cn" or "cnos")
                    {
                        return false;
                    }

                    // 原有判断逻辑
                    var cleanName = n.name.Split('/')[0].Trim()
                        .ReplaceAnyToOne("①②③④⑤⑥⑦⑧⑨⑩⑪⑫⑬⑭⑮⑯⑰⑱⑲⑳㉑㉒㉓㉔㉕㉖㉗㉘㉙㉚㉛㉜㉝㉞㉟㊱㊲㊳㊴㊵㊶㊷㊸㊹㊺㊻㊼㊽㊾㊿".Select(c => c.ToString()))
                        .Trim();

                    return n.region is "cn" or "cnos"
                        ? n.name.Contains(areaName) ||
                          (ChineseRegionService.CityToProvince.TryGetValue(cleanName, out var province) &&
                           province.Contains(areaName))
                        : WorldRegionService.CountriesToContinent.TryGetValue(cleanName, out var countries) &&
                        countries.Contains(areaName) || WorldRegionService.WellKnownCitiesToContinent.TryGetValue(
                            cleanName, out var city) &&
                        city.Contains(areaName);
                }) ?? 0;

                HoveredArea.Text = $"""
                                    {areaName}
                                    节点数: {nodeCount}
                                    """;
            }
        }
    }

    private void OnPathPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is not Path path)
        {
            return;
        }

        var (areaName, isClicked) = ((string, bool))(path.Tag ?? (string.Empty, false));

        // 如果是点击锁定状态，不要取消高亮
        if ((isClicked || string.IsNullOrEmpty(areaName)))
        {
            return;
        }

        if (!_continentOrProvincePaths.TryGetValue(areaName, out var paths))
        {
            return;
        }

        var normalBrush = GetBrushFromTheme(false);
        foreach (var p in paths)
        {
            if (_originalState.TryGetValue(p, out var original))
            {
                p.Stroke = original.Brush ?? normalBrush;
                p.StrokeThickness = 1.0; //请勿更改
            }
            else
            {
                p.Stroke = normalBrush;
                p.StrokeThickness = 1.0;
            }
        }

        // 清除悬停提示
        HoveredArea.Text = string.Empty;
    }

    private void OnPathPointerExited(object? sender, bool force)
    {
        if (sender is not Path path)
        {
            return;
        }

        var (areaName, isClicked) = ((string, bool))(path.Tag ?? (string.Empty, false));

        if (force) goto clear;
        // 如果是点击锁定状态，不要取消高亮
        if ((isClicked || string.IsNullOrEmpty(areaName)))
        {
            return;
        }

        clear:
        if (!_continentOrProvincePaths.TryGetValue(areaName, out var paths))
        {
            return;
        }

        var normalBrush = GetBrushFromTheme(false);
        foreach (var p in paths)
        {
            if (_originalState.TryGetValue(p, out var original))
            {
                p.Stroke = original.Brush ?? normalBrush;
                p.StrokeThickness = 1.0; //请勿更改
            }
            else
            {
                p.Stroke = normalBrush;
                p.StrokeThickness = 1.0;
            }

            p.Tag = (areaName, false);
        }

        // 清除悬停提示
        HoveredArea.Text = string.Empty;
    }

    private void OnPathPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Path path)
        {
            var (areaName, isClicked) = ((string, bool))(path.Tag ?? (string.Empty, false));

            if (string.IsNullOrEmpty(areaName))
            {
                return;
            }

            // 切换点击状态
            isClicked = !isClicked;
            path.Tag = (areaName, isClicked);
            if (_selectedArea != null)
            {
                // 取消之前锁定的区域的高亮
                OnPathPointerExited(_selectedArea, true);
            }

            if (!_continentOrProvincePaths.TryGetValue(areaName, out var paths))
            {
                return;
            }

            if (isClicked)
            {
                // 应用锁定高亮
                var highlightBrush = GetBrushFromTheme(true);
                foreach (var p in paths)
                {
                    p.Stroke = highlightBrush;
                    p.StrokeThickness = 1.5;
                }

                SelectedArea.Text = areaName;
                SelectedAreaName = areaName;
                _selectedArea = path;
                Core.App.CurrentLogger.Debug($"Locked on {areaName}");
            }
            else
            {
                // 取消锁定，恢复原始状态
                var normalBrush = GetBrushFromTheme(false);
                foreach (var p in paths)
                {
                    if (_originalState.TryGetValue(p, out var original))
                    {
                        p.Stroke = original.Brush ?? normalBrush;
                        p.StrokeThickness = 1.0; // 请勿更改
                    }
                    else
                    {
                        p.Stroke = normalBrush;
                        p.StrokeThickness = 1.0;
                    }
                }

                SelectedArea.Text = string.Empty;
                SelectedAreaName = null;
                Core.App.CurrentLogger.Debug($"Unlocked {areaName}");
            }
        }
    }


    private void SelectingItemsControl_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Option == null || Option.SelectedIndex == null)
        {
            return;
        }

        switch (Option.SelectedIndex)
        {
            case 1:
                ChinaMapControl.Hide();
                GlobalMap.Show();
                break;
            case 0:
                ChinaMapControl.Show();
                GlobalMap.Hide();
                break;
        }
    }
}