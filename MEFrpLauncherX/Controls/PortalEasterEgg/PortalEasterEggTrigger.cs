using System;

namespace MEFrpLauncherX.Controls.PortalEasterEgg;

/// <summary>
///     「传送门彩蛋」的点击序列判定器。
/// </summary>
/// <remarks>
///     <para>
///         刻意不依赖任何 Avalonia 类型：触发规则（连续点击次数、时间窗口、冷却）可以脱离
///         UI 单独验证，动画与视图也不需要知道「为什么被触发」。
///     </para>
///     <para>
///         规则：从第一次点击开始计时，<see cref="Window" /> 内累计 <see cref="RequiredClicks" />
///         次点击即触发；任意一次点击超出窗口会以该次点击为新的起点重新计数。
///         触发后进入 <see cref="Cooldown" /> 冷却，期间所有点击都被忽略且不计数。
///     </para>
/// </remarks>
public sealed class PortalEasterEggTrigger
{
    /// <summary>凑齐触发所需的连续点击次数。</summary>
    public const int RequiredClicks = 7;

    /// <summary>连续点击的时间窗口，从窗口内第一次点击开始计算。</summary>
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(3);

    /// <summary>触发后的冷却时间，避免动画刚结束就被下一次点击重复拉起。</summary>
    public static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(5);

    private int _count;
    private DateTimeOffset _windowStart;
    private DateTimeOffset _cooldownUntil = DateTimeOffset.MinValue;

    /// <summary>当前窗口内已累计的点击数（便于排查与验证）。</summary>
    public int Count => _count;

    /// <summary>是否处于触发后的冷却期。</summary>
    public bool IsCoolingDown(DateTimeOffset now) => now < _cooldownUntil;

    /// <summary>
    ///     记录一次点击。
    /// </summary>
    /// <param name="now">本次点击的时间戳（调用方传入，便于测试与使用单调时钟）。</param>
    /// <returns>本次点击凑齐了触发条件时返回 <see langword="true" />，其余情况返回 <see langword="false" />。</returns>
    public bool RegisterClick(DateTimeOffset now)
    {
        if (now < _cooldownUntil)
        {
            // 冷却期内不计数：否则「动画刚结束时的残留点击」会在下一轮窗口里被当成有效输入
            _count = 0;
            return false;
        }

        if (_count == 0 || now - _windowStart > Window)
        {
            _count = 0;
            _windowStart = now;
        }

        _count++;

        if (_count < RequiredClicks)
        {
            return false;
        }

        _count = 0;
        _cooldownUntil = now + Cooldown;
        return true;
    }

    /// <summary>清空当前计数，但不影响冷却状态。</summary>
    public void Reset() => _count = 0;
}
