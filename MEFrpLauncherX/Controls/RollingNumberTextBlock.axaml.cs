using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace MEFrpLauncherX.Controls;

public class RollingNumberTextBlock : TextBlock
{
    private const double AnimationDuration = 1.0; // 秒

    public static readonly StyledProperty<int> TargetNumberProperty =
        AvaloniaProperty.Register<RollingNumberTextBlock, int>(
            nameof(TargetNumber),
            coerce: OnTargetNumberChanged);

    private readonly Action _tickHandler;
    private int _currentValue;
    private bool _isAnimating;
    private DateTime _startTime;
    private int _targetValue;

    public RollingNumberTextBlock()
    {
        // 不再各自 new DispatcherTimer：
        // 首页 + 节点监控页同时常驻约 10 个实例，独立计时器会带来约 600 次/秒的
        // 调度器唤醒，且彼此之间、以及与渲染帧之间都不同步。
        // 改为共用 RollingAnimationTicker（16ms，Render 优先级，与渲染帧合并）。
        _tickHandler = OnTimerTick;
        HorizontalAlignment = HorizontalAlignment.Center;
    }

    public int TargetNumber
    {
        get => GetValue(TargetNumberProperty);
        set => SetValue(TargetNumberProperty, value);
    }

    private static int OnTargetNumberChanged(AvaloniaObject d, int value)
    {
        if (d is RollingNumberTextBlock control)
        {
            control.StartAnimation(value);
        }

        return value;
    }

    private void StartAnimation(int target)
    {
        if (string.IsNullOrEmpty(Text) || !int.TryParse(Text, out _currentValue))
        {
            _currentValue = 0;
        }

        _targetValue = target;
        _startTime = DateTime.Now;

        _isAnimating = true;
        RollingAnimationTicker.Register(_tickHandler);
    }

    private void OnTimerTick()
    {
        if (!_isAnimating)
        {
            return;
        }

        var elapsed = (DateTime.Now - _startTime).TotalSeconds;
        var progress = Math.Min(elapsed / AnimationDuration, 1.0);

        var value = _currentValue + (int)((_targetValue - _currentValue) * progress);
        Text = value.ToString();

        if (progress >= 1.0)
        {
            StopAnimation();
            Text = _targetValue.ToString();
        }
    }

    private void StopAnimation()
    {
        if (!_isAnimating)
        {
            return;
        }

        _isAnimating = false;
        RollingAnimationTicker.Unregister(_tickHandler);
    }

    /// <summary>
    ///     控件离开可视树时立即停止动画。
    ///     否则「动画未跑完就被导航切走」的控件会在后台持续 tick，无谓占用 UI 线程。
    /// </summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        StopAnimation();
        base.OnDetachedFromVisualTree(e);
    }
}