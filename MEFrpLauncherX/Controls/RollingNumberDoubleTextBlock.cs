using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;

namespace MEFrpLauncherX.Controls;

public class RollingNumberDoubleTextBlock : TextBlock
{
    private const double AnimationDuration = 1.0; // 秒

    public static readonly StyledProperty<double> TargetNumberProperty =
        AvaloniaProperty.Register<RollingNumberDoubleTextBlock, double>(
            nameof(TargetNumber),
            coerce: OnTargetNumberChanged);

    public static readonly StyledProperty<string> NumberFormatProperty =
        AvaloniaProperty.Register<RollingNumberTextBlock, string>(
            nameof(NumberFormat),
            "F2");

    private readonly DispatcherTimer _timer;
    private double _currentValue;

    private DateTime _startTime;
    private Stopwatch _stopwatch;
    private double _targetValue;

    public RollingNumberDoubleTextBlock()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) }; // ~60fps
        _timer.Tick += OnTimerTick;
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

        _timer.Stop();
        _timer.Start();
    }


    private void OnTimerTick(object sender, EventArgs e)
    {
        var elapsed = (DateTime.Now - _startTime).TotalSeconds;
        var progress = Math.Min(elapsed / AnimationDuration, 1.0);

        var value = _currentValue + (_targetValue - _currentValue) * progress;
        Text = value.ToString(NumberFormat);

        if (progress >= 1.0)
        {
            _timer.Stop();
            Text = _targetValue.ToString(NumberFormat);
        }
    }
}