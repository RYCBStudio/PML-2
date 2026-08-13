using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using MarkdownAIRender.Helper;

namespace MarkdownAIRender.Controls.MarkdownRender;

/// <summary>
///     Markdown 链接控件。
///     使用用户主题色（AccentTextFillColor 系列画刷，见 Default.axaml 样式），
///     并在按下时播放“下划线从左向右迅速划过”的动画。
/// </summary>
public class MarkdownLink : Panel
{
    private readonly Border _underline;
    private Control? _linkContent;

    public MarkdownLink()
    {
        // 透明背景保证整个区域可命中
        Background = Brushes.Transparent;
        Cursor = new Cursor(StandardCursorType.Hand);

        _underline = new Border
        {
            Classes = { "MdLinkUnderline" },
            Height = 1,
            Width = 0,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        Children.Add(_underline);

        // 即使内部控件处理了事件也要响应点击
        AddHandler(TappedEvent, OnTapped, RoutingStrategies.Bubble, true);
    }

    #region Properties

    /// <summary>
    ///     链接目标地址
    /// </summary>
    public string? Url
    {
        get;
        set;
    }

    /// <summary>
    ///     链接正文内容（文本控件）
    /// </summary>
    public Control? LinkContent
    {
        get => _linkContent;
        set
        {
            if (_linkContent != null)
            {
                Children.Remove(_linkContent);
            }

            _linkContent = value;
            if (value != null)
            {
                Children.Insert(0, value);
            }
        }
    }

    #endregion

    #region Overrides

    protected override Size MeasureOverride(Size availableSize)
    {
        var desired = new Size();
        foreach (var child in Children)
        {
            child.Measure(availableSize);
            desired = new Size(
                Math.Max(desired.Width, child.DesiredSize.Width),
                Math.Max(desired.Height, child.DesiredSize.Height));
        }

        return desired;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            child.Arrange(new Rect(finalSize));
        }

        return finalSize;
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(":pointerover", true);

        // hover 时下划线从左向右迅速划过，并保持显示
        UpdateUnderlineHeight();
        _underline.Width = Math.Max(Bounds.Width, DesiredSize.Width);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(":pointerover", false);

        // 未按住时收回下划线
        if (!PseudoClasses.Contains(":pressed"))
        {
            _underline.Width = 0;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        e.Pointer.Capture(this);
        PseudoClasses.Set(":pressed", true);

        // 确保按下时下划线可见（例如触摸设备等无 hover 场景）
        UpdateUnderlineHeight();
        _underline.Width = Math.Max(Bounds.Width, DesiredSize.Width);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!PseudoClasses.Contains(":pressed"))
        {
            return;
        }

        PseudoClasses.Set(":pressed", false);

        // 松开后若指针已离开，则收回下划线
        if (!PseudoClasses.Contains(":pointerover"))
        {
            _underline.Width = 0;
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        ResetPressedState();
    }

    #endregion

    #region Private Methods

    private void UpdateUnderlineHeight()
    {
        // 下划线粗细随字号自适应
        var fontSize = GetValue(TextElement.FontSizeProperty);
        _underline.Height = Math.Max(1.0, fontSize / 12.0);
    }

    private void ResetPressedState()
    {
        if (!PseudoClasses.Contains(":pressed"))
        {
            return;
        }

        PseudoClasses.Set(":pressed", false);

        // 捕获丢失（如窗口失焦）后，仅在仍悬停时保留下划线
        if (!PseudoClasses.Contains(":pointerover"))
        {
            _underline.Width = 0;
        }
    }

    private void OnTapped(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(Url))
        {
            UrlHelper.OpenUrl(Url);
        }
    }

    #endregion
}
