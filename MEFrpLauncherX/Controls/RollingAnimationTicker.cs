using System;
using System.Collections.Generic;
using Avalonia.Threading;

namespace MEFrpLauncherX.Controls;

/// <summary>
///     数字滚动动画的共享节拍器。
/// </summary>
/// <remarks>
///     优化背景：<see cref="RollingNumberTextBlock" /> 与 <see cref="RollingNumberDoubleTextBlock" />
///     曾经各自持有一个 <see cref="DispatcherTimer" />。而首页 + 节点监控页同时常驻 10 个实例，
///     意味着约 600 次/秒的独立调度器唤醒，且这些唤醒彼此不同步，会与渲染帧错位。
///     <para>
///         现在所有滚动动画共用这一个计时器，并且使用 <see cref="DispatcherPriority.Render" />，
///         使回调与渲染帧合并到同一优先级批次中，避免额外的 Dispatcher 往返。
///         没有动画在跑时计时器会自动停止，空闲时零开销。
///     </para>
/// </remarks>
internal static class RollingAnimationTicker
{
    private static readonly List<Action> Subscribers = new();
    private static DispatcherTimer _timer;

    /// <summary>注册一个每帧回调。重复注册同一委托是幂等的。</summary>
    public static void Register(Action tick)
    {
        if (Subscribers.Contains(tick))
        {
            return;
        }

        Subscribers.Add(tick);

        _timer ??= new DispatcherTimer(
            TimeSpan.FromMilliseconds(16),
            DispatcherPriority.Render,
            (_, _) => Tick());

        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }
    }

    /// <summary>注销回调；当没有任何订阅者时停止计时器。</summary>
    public static void Unregister(Action tick)
    {
        Subscribers.Remove(tick);

        if (Subscribers.Count == 0)
        {
            _timer?.Stop();
        }
    }

    private static void Tick()
    {
        // 回调自身可能调用 Unregister（动画结束），因此先快照再遍历，避免枚举期间修改集合
        var snapshot = Subscribers.ToArray();
        foreach (var subscriber in snapshot)
        {
            subscriber();
        }
    }
}
