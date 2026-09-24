using System;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.Xaml.Interactivity;

namespace MEFrpLauncherX.Behaviours;

/// <summary>
/// 渐近式假进度条 Behavior。
/// 支持 Fake（无限逼近 Target）与 Real（真实进度）无缝切换。
/// </summary>
public class AsymptoticProgressBehavior : Behavior<ProgressBar>
{
    // ==================== 依赖属性 ====================

    public static readonly StyledProperty<bool> IsRunningProperty =
        AvaloniaProperty.Register<AsymptoticProgressBehavior, bool>(nameof(IsRunning));

    public static readonly StyledProperty<bool> IsFakeModeProperty =
        AvaloniaProperty.Register<AsymptoticProgressBehavior, bool>(nameof(IsFakeMode), defaultValue: true);

    public static readonly StyledProperty<double> TargetProperty =
        AvaloniaProperty.Register<AsymptoticProgressBehavior, double>(nameof(Target), 99.0);

    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<AsymptoticProgressBehavior, double>(nameof(Speed), 0.15);

    public static readonly StyledProperty<TimeSpan> UpdateIntervalProperty =
        AvaloniaProperty.Register<AsymptoticProgressBehavior, TimeSpan>(
            nameof(UpdateInterval), TimeSpan.FromMilliseconds(30));

    public static readonly StyledProperty<double> RealProgressProperty =
        AvaloniaProperty.Register<AsymptoticProgressBehavior, double>(nameof(RealProgress));

    public static readonly StyledProperty<bool> JumpToMaximumOnStopProperty =
        AvaloniaProperty.Register<AsymptoticProgressBehavior, bool>(nameof(JumpToMaximumOnStop), true);

    public static readonly StyledProperty<TimeSpan> CompleteAnimationDurationProperty =
        AvaloniaProperty.Register<AsymptoticProgressBehavior, TimeSpan>(
            nameof(CompleteAnimationDuration), TimeSpan.FromMilliseconds(350));

    // ==================== 属性包装 ====================

    public bool IsRunning
    {
        get => GetValue(IsRunningProperty);
        set => SetValue(IsRunningProperty, value);
    }

    /// <summary>
    /// true = 假进度（渐近逼近 Target）
    /// false = 真实进度（直接使用 RealProgress）
    /// </summary>
    public bool IsFakeMode
    {
        get => GetValue(IsFakeModeProperty);
        set => SetValue(IsFakeModeProperty, value);
    }

    public double Target
    {
        get => GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    public double Speed
    {
        get => GetValue(SpeedProperty);
        set => SetValue(SpeedProperty, value);
    }

    public TimeSpan UpdateInterval
    {
        get => GetValue(UpdateIntervalProperty);
        set => SetValue(UpdateIntervalProperty, value);
    }

    /// <summary>
    /// 真实进度值（0~100）。当 IsFakeMode = false 时生效。
    /// </summary>
    public double RealProgress
    {
        get => GetValue(RealProgressProperty);
        set => SetValue(RealProgressProperty, value);
    }

    public bool JumpToMaximumOnStop
    {
        get => GetValue(JumpToMaximumOnStopProperty);
        set => SetValue(JumpToMaximumOnStopProperty, value);
    }

    public TimeSpan CompleteAnimationDuration
    {
        get => GetValue(CompleteAnimationDurationProperty);
        set => SetValue(CompleteAnimationDurationProperty, value);
    }

    // ==================== 内部状态 ====================

    private readonly CompositeDisposable _disposables = new();
    private DispatcherTimer? _timer;
    private DateTime _fakeStartTime;
    private double _fakeBaseProgress;   // 切换到 Fake 时的起始进度，保证平滑

    // ==================== 生命周期 ====================

    protected override void OnAttached()
    {
        base.OnAttached();

        // 监听 IsRunning
        _disposables.Add(
            this.GetObservable(IsRunningProperty)
                .Subscribe(running =>
                {
                    if (running) Start();
                    else Stop();
                }));

        // 监听 IsFakeMode 切换（保证从 Real → Fake 时平滑衔接）
        _disposables.Add(
            this.GetObservable(IsFakeModeProperty)
                .Subscribe(_ => OnModeChanged()));

        // 监听真实进度变化
        _disposables.Add(
            this.GetObservable(RealProgressProperty)
                .Subscribe(_ =>
                {
                    if (!IsFakeMode && IsRunning && AssociatedObject != null)
                        AssociatedObject.Value = Math.Clamp(RealProgress, 0, AssociatedObject.Maximum);
                }));

        // 监听刷新间隔变化
        _disposables.Add(
            this.GetObservable(UpdateIntervalProperty)
                .Subscribe(interval =>
                {
                    if (_timer != null)
                        _timer.Interval = interval;
                }));
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        Stop(force: true);
        _disposables.Dispose();
    }

    // ==================== 核心逻辑 ====================

    private void Start()
    {
        if (AssociatedObject == null) return;

        if (IsFakeMode)
        {
            // 关键修复：每次开始假进度都强制从 0 开始
            _fakeBaseProgress = 0;
            AssociatedObject.Value = 0;          // 立刻清零显示
            _fakeStartTime = DateTime.Now;
        }

        if (_timer == null)
        {
            _timer = new DispatcherTimer
            {
                Interval = UpdateInterval
            };
            _timer.Tick += OnTimerTick;
        }

        _timer.Start();
    }

    private async void Stop(bool force = false)
    {
        _timer?.Stop();

        if (force || AssociatedObject == null)
        {
            _timer = null;
            return;
        }

        if (JumpToMaximumOnStop)
        {
            // 1. 先停掉假进度
            // 2. 快速冲到 100%
            var animation = new Animation
            {
                Duration = CompleteAnimationDuration,
                FillMode = FillMode.Forward,
                Easing = new CubicEaseOut(),
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(1d),
                        Setters = { new Setter(ProgressBar.ValueProperty, AssociatedObject.Maximum) }
                    }
                }
            };

            await animation.RunAsync(AssociatedObject);

            // 3. 稍微停顿一下（模拟 Naive UI 的停留）
            await Task.Delay(150);

            // 4. 重置为 0，方便下次从头开始
            AssociatedObject.Value = 0;
        }

        _timer = null;
    }

    private void OnModeChanged()
    {
        if (!IsRunning || AssociatedObject == null) return;

        if (IsFakeMode)
        {
            // Real → Fake：以当前真实进度为新的基线继续渐近
            _fakeBaseProgress = AssociatedObject.Value;
            _fakeStartTime = DateTime.Now;
        }
        // Fake → Real 时直接由 RealProgress 订阅处理
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (AssociatedObject == null || !IsRunning) return;

        if (IsFakeMode)
        {
            var elapsed = (DateTime.Now - _fakeStartTime).TotalSeconds;

            // 从当前基线开始渐近逼近 Target
            // 公式：base + (Target - base) * (1 - e^{-kt})
            var progress = _fakeBaseProgress +
                           (Target - _fakeBaseProgress) * (1.0 - Math.Exp(-Speed * elapsed));

            // 严格保护，绝不允许超过 Target
            AssociatedObject.Value = Math.Min(Target, progress);
        }
        // Real 模式由 RealProgress 的订阅直接更新，不在这里处理
    }
}