using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Web;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Skia;
using Avalonia.Threading;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.Helpers;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Core.Services;
using SkiaSharp;

namespace MEFrpLauncherX.Controls;

/// <summary>单个二维码条目：域名（展示用）与其对应的二维码内容。</summary>
/// <param name="Domain">域名，用于在界面上标识当前查看的是哪一个 domain</param>
/// <param name="Content">二维码承载的内容</param>
public sealed record QRCodeItem(string Domain, string Content);

/// <summary>
/// 二维码查看与定制控件。可承载同一代理下全部 domain 的二维码集合，
/// 以轮播方式（左右按钮 / 圆点 / ← → 方向键）在集合内切换；
/// 集合只有一个元素时不显示任何切换控件，与原有单二维码场景保持一致。
/// </summary>
public partial class CustomizeQRCode : UserControl
{
    /// <summary>横向滑入偏移量（px）</summary>
    private const double SlideOffset = 26;

    /// <summary>切换动画时长，接近 Fluent 的 MotionEaseOut 观感</summary>
    private static readonly TimeSpan SlideDuration = TimeSpan.FromMilliseconds(180);

    /// <summary>圆点指示器最多展示数量，超过时仅保留“当前/总数”序号，避免指示器溢出</summary>
    private const int MaxDots = 12;

    private readonly List<QRCodeItem> _items = [];
    private readonly List<Border> _dots = [];

    /// <summary>按条目索引缓存位图；大小/颜色/图标等设置变化时整体失效</summary>
    private readonly Dictionary<int, Bitmap> _bitmapCache = new();
    private readonly Stopwatch _slideWatch = new();

    /// <summary>当前图层与出栈图层的横向位移变换（切换动画使用）</summary>
    private readonly TranslateTransform _qrTranslate = new();
    private readonly TranslateTransform _outgoingTranslate = new();

    private DispatcherTimer? _slideTimer;
    private TopLevel? _topLevel;
    private int _index;
    private int _slideDirection = 1;
    private bool _hasOutgoing;
    private bool _isReady;

    private int _size = 256;
    private SKColor _foreground;
    private SKColor _background = SKColors.Transparent;
    private int _iconSize = 10;
    private SKBitmap? _icon;

    public CustomizeQRCode()
    {
        InitializeComponent();
        AttachTransforms();
    }

    /// <summary>单二维码构造：保留旧用法（内容同时作为域名标识），不显示切换控件。</summary>
    public CustomizeQRCode(string qrCode) : this([new QRCodeItem(qrCode, qrCode)])
    {
    }

    /// <summary>集合构造：承载该代理下全部 domain 的二维码，默认定位到第一个。</summary>
    public CustomizeQRCode(IEnumerable<QRCodeItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _foreground = GetAccentColor();
        InitializeComponent();
        AttachTransforms();
        _items.AddRange(items);
        // 初始化期间滑块的取值会触发 ValueChanged，此处之后再放开渲染与设置同步
        _isReady = true;
        ForeColorPicker.Color = ToColor(_foreground);
        BackColorPicker.Color = ToColor(_background);
        BuildDots();
        RenderCurrent();
    }

    /// <summary>为两个图层挂载位移变换，切换时由此驱动横向滑动。</summary>
    private void AttachTransforms()
    {
        QRCode.RenderTransform = _qrTranslate;
        PreviousQRCode.RenderTransform = _outgoingTranslate;
    }

    /// <summary>当前条目的二维码位图；导出 PNG、复制到剪贴板等功能均作用于该位图。</summary>
    public Bitmap? CurrentBitmap => QRCode.Source as Bitmap;

    /// <summary>条目总数</summary>
    public int ItemCount => _items.Count;

    /// <summary>当前显示的条目索引（从 0 开始）</summary>
    public int CurrentIndex => _index;

    /// <summary>当前显示的条目</summary>
    public QRCodeItem? CurrentItem => _index >= 0 && _index < _items.Count ? _items[_index] : null;

    #region 轮播切换

    private void ShowPreviousDomain(object? sender, RoutedEventArgs e) => NavigateTo(_index - 1, -1);

    private void ShowNextDomain(object? sender, RoutedEventArgs e) => NavigateTo(_index + 1, 1);

    /// <summary>
    /// 切换到指定索引。<paramref name="direction"/> 只决定滑入方向（正数表示查看下一个）。
    /// 首尾采用「禁用按钮、不循环」策略：越界请求会被钳制，因此按钮与方向键的体验完全一致。
    /// 不响应鼠标滚轮，避免用户滚动查看/调整设置时误切换域名。
    /// </summary>
    private void NavigateTo(int index, int direction)
    {
        if (!_isReady || _items.Count == 0)
        {
            return;
        }

        index = Math.Clamp(index, 0, _items.Count - 1);
        if (index == _index)
        {
            // 边界（或不循环）时原地不动，只同步一次按钮可用状态
            UpdateNavigationState();
            return;
        }

        // 先渲染目标位图，避免动画首帧因绘制而卡顿
        var incoming = GetOrCreateBitmap(index);
        var outgoing = QRCode.Source as Bitmap;

        PreviousQRCode.Source = outgoing;
        _outgoingTranslate.X = 0;
        PreviousQRCode.Opacity = outgoing is null ? 0 : 1;
        _hasOutgoing = outgoing is not null;

        _index = index;
        QRCode.Source = incoming;
        UpdateNavigationState();
        StartSlideTransition(direction);
    }

    /// <summary>重绘当前条目（不播放切换动画），用于初始化与设置变更。</summary>
    private void RenderCurrent()
    {
        if (!_isReady)
        {
            return;
        }

        StopSlideTransition();
        _hasOutgoing = false;
        PreviousQRCode.Source = null;
        PreviousQRCode.Opacity = 0;
        _outgoingTranslate.X = 0;
        QRCode.Source = GetOrCreateBitmap(_index);
        _qrTranslate.X = 0;
        QRCode.Opacity = 1;
        IconSizeSlider.Value = _iconSize;
        SizeSlider.Value = _size;
        UpdateNavigationState();
    }

    /// <summary>同步导航栏可见性、域名、序号、按钮可用性与圆点选中态。</summary>
    private void UpdateNavigationState()
    {
        var count = _items.Count;
        var multiple = count > 1;

        NavigationBar.IsVisible = count > 0;
        DomainText.Text = CurrentItem?.Domain ?? string.Empty;

        // 只有单个 domain 时隐藏全部切换控件（按钮 / 序号 / 圆点 / 提示）
        PreviousButton.IsVisible = multiple;
        NextButton.IsVisible = multiple;
        IndexText.IsVisible = multiple;
        IndicatorBar.IsVisible = multiple;
        if (!multiple)
        {
            return;
        }

        IndexText.Text = $"{_index + 1} / {count}";
        PreviousButton.IsEnabled = _index > 0;
        NextButton.IsEnabled = _index < count - 1;

        DotPanel.IsVisible = _dots.Count <= MaxDots;
        for (var i = 0; i < _dots.Count; i++)
        {
            _dots[i].Classes.Set("Active", i == _index);
        }
    }

    /// <summary>按条目数量创建圆点指示器，点击圆点可直接跳转到对应二维码。</summary>
    private void BuildDots()
    {
        if (_dots.Count == _items.Count)
        {
            return;
        }

        DotPanel.Children.Clear();
        _dots.Clear();
        for (var i = 0; i < _items.Count; i++)
        {
            var dot = new Border();
            dot.Classes.Add("QRCodeDot");
            var target = i;
            dot.PointerPressed += (_, _) => NavigateTo(target, Math.Sign(target - _index));
            DotPanel.Children.Add(dot);
            _dots.Add(dot);
        }
    }

    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        if (_items.Count <= 1)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Left:
                NavigateTo(_index - 1, -1);
                e.Handled = true;
                break;
            case Key.Right:
                NavigateTo(_index + 1, 1);
                e.Handled = true;
                break;
        }
    }

    #endregion

    #region 过渡动画

    /// <summary>
    /// 启动「横向滑动 + 淡入淡出」过渡。每帧都按绝对进度重新计算两个图层的位置与透明度，
    /// 因此连续快速切换只会把动画重定向到新的起点，不会叠加动画，也不会残留位移、透明度或残影。
    /// </summary>
    private void StartSlideTransition(int direction)
    {
        _slideDirection = direction >= 0 ? 1 : -1;
        _slideTimer ??= CreateSlideTimer();
        _slideWatch.Restart();
        _slideTimer.Stop();
        ApplySlideFrame(0);
        _slideTimer.Start();
    }

    private DispatcherTimer CreateSlideTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += OnSlideTick;
        return timer;
    }

    private void OnSlideTick(object? sender, EventArgs e)
    {
        var progress = _slideWatch.Elapsed.TotalMilliseconds / SlideDuration.TotalMilliseconds;
        if (progress >= 1)
        {
            StopSlideTransition();
            ApplySlideFrame(1);
            return;
        }

        ApplySlideFrame(progress);
    }

    private void StopSlideTransition() => _slideTimer?.Stop();

    /// <summary>缓出曲线（1 - (1 - t)^3），与 Fluent 主题的过渡观感一致。</summary>
    private void ApplySlideFrame(double progress)
    {
        var eased = 1 - Math.Pow(1 - Math.Clamp(progress, 0, 1), 3);
        _qrTranslate.X = _slideDirection * SlideOffset * (1 - eased);
        QRCode.Opacity = 0.25 + 0.75 * eased;

        if (!_hasOutgoing)
        {
            return;
        }

        _outgoingTranslate.X = -_slideDirection * SlideOffset * eased;
        PreviousQRCode.Opacity = 1 - eased;
    }

    #endregion

    #region 绘制

    private Bitmap? GetOrCreateBitmap(int index)
    {
        if (index < 0 || index >= _items.Count)
        {
            return null;
        }

        if (_bitmapCache.TryGetValue(index, out var cached))
        {
            return cached;
        }

        var content = _items[index].Content;
        if (string.IsNullOrEmpty(content))
        {
            return null;
        }

        try
        {
            var bitmap = QRCodeService.GetQRCodeBitmapWithIcon(content, _icon, iconSizePercent: _iconSize,
                size: _size, foreground: _foreground, background: _background);
            _bitmapCache[index] = bitmap;
            return bitmap;
        }
        catch (Exception exception)
        {
            // 内容超出二维码容量等异常：记录日志并保持空白，避免中断切换
            Core.App.CurrentLogger.Error(exception, "生成二维码位图失败");
            return null;
        }
    }

    /// <summary>设置变化后作废位图缓存并重绘当前二维码。</summary>
    private void ApplySettingChange()
    {
        if (!_isReady)
        {
            return;
        }

        _bitmapCache.Clear();
        RenderCurrent();
    }

    /// <summary>默认前景色沿用当前主题色（与改造前保持一致）。</summary>
    private static SKColor GetAccentColor()
    {
        var accent = App.FATheme?.CustomAccentColor;
        return new SKColor(accent?.R ?? 0, accent?.G ?? 0, accent?.B ?? 0, accent?.A ?? 255);
    }

    private static Color ToColor(SKColor color) =>
        Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);

    #endregion

    #region 生命周期

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // 在对话框打开期间生效，无需抢占焦点即可响应 ← → 方向键
        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel is not null)
        {
            _topLevel.KeyDown += OnTopLevelKeyDown;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        StopSlideTransition();
        if (_topLevel is not null)
        {
            _topLevel.KeyDown -= OnTopLevelKeyDown;
            _topLevel = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    #endregion

    #region 设置项（作用于当前正在显示的二维码）

    private void UpdateSize(object? sender, RangeBaseValueChangedEventArgs e)
    {
        _size = (int)e.NewValue;
        ApplySettingChange();
    }

    private void UpdateIconSize(object? sender, RangeBaseValueChangedEventArgs e)
    {
        _iconSize = (int)e.NewValue;
        ApplySettingChange();
    }

    private void UpdateForeground(object? sender, ColorChangedEventArgs e)
    {
        if (!_isReady)
        {
            return;
        }

        _foreground = e.NewColor.ToSKColor();
        ApplySettingChange();
    }

    private void UpdateBackground(object? sender, ColorChangedEventArgs e)
    {
        if (!_isReady)
        {
            return;
        }

        _background = e.NewColor.ToSKColor();
        ApplySettingChange();
    }

    private async void SelectIconFile(object? sender, RoutedEventArgs e)
    {
        var files = await FileSystemHelper.OpenFilePickerAsync(Languages.Text_UserProxy_QRCodeView_SelectIconFile,
            "icon.png", "", Languages.Text_Global_FileType_Picture);
        if (files?.Count > 0)
        {
            var file = HttpUtility.UrlDecode(files[0].Path.AbsolutePath);
            _icon = new Bitmap(file).ToSKBitmap();
            ApplySettingChange();
        }
    }

    /// <summary>导出当前正在显示的二维码为 PNG 文件。</summary>
    private async void SaveFile(object? sender, RoutedEventArgs e)
    {
        var file = await FileSystemHelper.SaveFilePickerAsync(Languages.Text_UserProxy_QRCodeView_ExportPng,
            "qrcode.png", "", Languages.Text_Global_FileType_Picture);
        try
        {
            if (file is null)
            {
                return;
            }

            (QRCode.Source as Bitmap)?.Save(HttpUtility.UrlDecode(file.Path.AbsolutePath));
            Growl.Success(Languages.Text_UserProxy_QRCodeView_SavedSuccessfully);
        }
        catch (Exception exception)
        {
            Core.App.CurrentLogger.Error(exception);
            Growl.Error(Languages.Text_UserProxy_QRCodeView_FailedToSave);
        }
    }

    #endregion

    private async void CopyToClipboard(object? sender, RoutedEventArgs e)
    {
        var clipboard = Core.App.MainWindow?.Clipboard;
        await clipboard?.SetBitmapAsync(QRCode.Source as Bitmap);
        Growl.Success(Languages.Text_UserProxy_QRCodeCopiedToClipboard);
    }
}
