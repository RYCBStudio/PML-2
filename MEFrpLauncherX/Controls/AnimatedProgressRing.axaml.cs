using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace MEFrpLauncherX.Controls;

public partial class AnimatedProgressRing : UserControl
{
    private const double DurTop = 2.0;
    private const double DurLeft = 2.5;
    private const double DurRight = 2.2;
    private readonly Ellipse _movingLeft;
    private readonly Ellipse _movingRight;
    private readonly Ellipse _movingTop;

    private readonly Stopwatch _sw = new();
    private readonly DispatcherTimer _timer;

    public AnimatedProgressRing()
    {
        InitializeComponent();

        _movingTop = this.FindControl<Ellipse>("MovingTop");
        _movingLeft = this.FindControl<Ellipse>("MovingLeft");
        _movingRight = this.FindControl<Ellipse>("MovingRight");

        _sw.Start();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (s, e) => Tick();
        _timer.Start();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private static double TwoPhaseInterp(double t)
    {
        if (t <= 0.5)
        {
            return t * 2.0;
        }

        return (1.0 - t) * 2.0;
    }

    private void Tick()
    {
        var elapsed = _sw.Elapsed.TotalSeconds;

        // 用已知的 Width/Height 做偏移（Ellipse 在 XAML 中指定了 Width/Height）
        // Top moving (cy 30 -> 50 -> 30)
        {
            var t = elapsed % DurTop / DurTop;
            var f = TwoPhaseInterp(t);
            var y = Lerp(30.0, 50.0, f);
            var half = _movingTop.Height / 2.0; // 使用 Height 而不是 Bounds
            Canvas.SetTop(_movingTop, y - half);
            _movingTop.Opacity = Lerp(0.8, 0.3, f);
        }

        // Left moving (cx 30 -> 50 -> 30)
        {
            var t = elapsed % DurLeft / DurLeft;
            var f = TwoPhaseInterp(t);
            var x = Lerp(30.0, 50.0, f);
            var half = _movingLeft.Width / 2.0;
            Canvas.SetLeft(_movingLeft, x - half);
            _movingLeft.Opacity = Lerp(0.8, 0.3, f);
        }

        // Right moving (cx 90 -> 70 -> 90)
        {
            var t = elapsed % DurRight / DurRight;
            var f = TwoPhaseInterp(t);
            var x = Lerp(90.0, 70.0, f);
            var half = _movingRight.Width / 2.0;
            Canvas.SetLeft(_movingRight, x - half);
            _movingRight.Opacity = Lerp(0.8, 0.3, f);
        }
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
}