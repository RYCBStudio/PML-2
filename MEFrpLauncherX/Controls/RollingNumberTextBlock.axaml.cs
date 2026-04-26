using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;

namespace MEFrpLauncherX.Controls;

public class RollingNumberTextBlock : TextBlock
{
    private const double AnimationDuration = 1.0; // 秒

    public static readonly StyledProperty<int> TargetNumberProperty =
        AvaloniaProperty.Register<RollingNumberTextBlock, int>(
            nameof(TargetNumber),
            coerce: OnTargetNumberChanged);

    private readonly DispatcherTimer _timer;
    private int _currentValue;
    private DateTime _startTime;
    private Stopwatch _stopwatch;
    private int _targetValue;

    public RollingNumberTextBlock()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(8) }; // ~120fps
        _timer.Tick += OnTimerTick;
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

        // 在Avalonia中，我们使用DispatcherTimer而不是CompositionTarget.Rendering
        _timer.Stop();
        _timer.Start();
    }

    private void OnTimerTick(object sender, EventArgs e)
    {
        var elapsed = (DateTime.Now - _startTime).TotalSeconds;
        var progress = Math.Min(elapsed / AnimationDuration, 1.0);

        var value = _currentValue + (int)((_targetValue - _currentValue) * progress);
        Text = value.ToString();

        if (progress >= 1.0)
        {
            _timer.Stop();
            Text = _targetValue.ToString();
        }
    }
}