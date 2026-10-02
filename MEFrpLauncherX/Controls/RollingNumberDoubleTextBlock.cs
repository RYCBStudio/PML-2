using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace MEFrpLauncherX.Controls;

public class RollingNumberDoubleTextBlock : TextBlock
{
    private const double AnimationDuration = 1.0; // 秒

    public static readonly StyledProperty<double> TargetNumberProperty =
        AvaloniaProperty.Register<RollingNumberDoubleTextBlock, double>(
            nameof(TargetNumber),
            coerce: OnTargetNumberChanged);

    // 修正：原先把 OwnerType 写成了 RollingNumberTextBlock，
    // 导致 NumberFormat 的 GetValue/SetValue 会因 OwnerType 校验失败而抛
    // ArgumentException（"Property ... is not registered on this type"）。
    // 目前全仓库没有任何 XAML 设置 NumberFormat，所以该缺陷一直未被触发。
    public static readonly StyledProperty<string> NumberFormatProperty =
        AvaloniaProperty.Register<RollingNumberDoubleTextBlock, string>(
            nameof(NumberFormat),
            "F2");

    private readonly Action _tickHandler;
    private double _currentValue;
    private bool _isAnimating;
    private DateTime _startTime;
    private double _targetValue;

    public RollingNumberDoubleTextBlock()
    {
        // 与 RollingNumberTextBlock 共用同一个节拍器：避免多实例各自 60Hz 唤醒，
        // 且唤醒时机与渲染帧错位。
        _tickHandler = OnTimerTick;
        HorizontalAlignment = HorizontalAlignment.Center;
    }

    public double TargetNumber
    {
        get => GetValue(TargetNumberProperty);
        set => SetValue(TargetNumberProperty, value);
    }

    public string NumberFormat
    {
        get => GetValue(NumberFormatProperty);
        set => SetValue(NumberFormatProperty, value);
    }

    private static double OnTargetNumberChanged(AvaloniaObject d, double value)
    {
        if (d is RollingNumberDoubleTextBlock control)
        {
            control.StartAnimation(value);
        }

        return value;
    }

    private void StartAnimation(double target)
    {
        if (string.IsNullOrEmpty(Text) || !double.TryParse(Text, out _currentValue))
        {
            _currentValue = 0.0;
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

        var value = _currentValue + (_targetValue - _currentValue) * progress;
        Text = value.ToString(NumberFormat);

        if (progress >= 1.0)
        {
            StopAnimation();
            Text = _targetValue.ToString(NumberFormat);
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
    ///     控件离开可视树时立即停止动画，避免后台空转占用 UI 线程。
    /// </summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        StopAnimation();
        base.OnDetachedFromVisualTree(e);
    }
}