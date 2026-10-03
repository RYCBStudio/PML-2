using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Styling;
using MEFrpLauncherX.Core;

namespace MEFrpLauncherX.Styling;

/// <summary>
///     标签页切换过渡：旧内容轻微缩退并淡出，新内容略微放大推入并淡入。
/// </summary>
/// <remarks>
///     <para>
///         相比 <see cref="CrossFade" />（纯透明度交叉淡化），这里额外加入了
///         <c>scale(0.98) → scale(1)</c> 的 <see cref="Visual.RenderTransform" /> 变化，
///         让「内容已被替换」有明确的方向感。
///     </para>
///     <para>
///         只动 <see cref="Visual.OpacityProperty" /> 与 <see cref="Visual.RenderTransformProperty" />，
///         <b>绝不触碰 Width/Height/Margin/Padding</b>——后四者会触发每帧完整布局，
///         在 165Hz 下足以让切换动画本身变成掉帧源。
///     </para>
///     <para>
///         本类不持有可变状态，可在多处共享同一实例（<c>App.axaml</c> 中以
///         <c>TabContentTransition</c> 为键注册的就是同一个共享实例）。
///     </para>
/// </remarks>
public sealed class TabContentFadeScaleTransition : IPageTransition
{
    /// <summary>标准动画程度：时长。</summary>
    public static readonly TimeSpan StandardDuration = TimeSpan.FromMilliseconds(160);

    /// <summary>标准动画程度：缓出曲线。</summary>
    public static readonly Easing StandardEasing = new CubicEaseOut();

    /// <summary>标准动画程度：入场起始缩放。</summary>
    public const double StandardFromScale = 0.98;

    /// <summary>
    ///     共享实例。本类不持有可变状态，可被 <c>App.axaml</c> 的资源引用与
    ///     <c>TabStripContentTransitionBehavior</c> 同时使用。
    /// </summary>
    public static readonly TabContentFadeScaleTransition Shared = new();

    private static readonly TimeSpan ReducedDuration = TimeSpan.FromMilliseconds(110);
    private static readonly Easing ReducedEasing = new LinearEasing();

    /// <summary>
    ///     解析当前「动画程度」设置对应的过渡参数。
    /// </summary>
    /// <returns>
    ///     动画已关闭（级别 0）时返回 <c>false</c>，调用方应直接跳过动画并清理残留值。
    /// </returns>
    /// <remarks>
    ///     0=关闭 1=精简 2=标准（范围由 <c>ConfigManager</c> 归一化保证）。
    ///     每次调用实时读取配置，因此在设置页改动后无需重建过渡实例即可即时生效。
    /// </remarks>
    public static bool TryGetAnimationParameters(out TimeSpan duration, out Easing easing, out double fromScale)
    {
        duration = StandardDuration;
        easing = StandardEasing;
        fromScale = StandardFromScale;

        int level;
        try
        {
            level = ConfigManager.CurrentConfig.AnimationLevel;
        }
        catch
        {
            // 配置尚未就绪时按「标准动画」处理
            level = 2;
        }

        switch (level)
        {
            case <= 0:
                return false;
            case 1:
                duration = ReducedDuration;
                easing = ReducedEasing;
                fromScale = 1;
                return true;
            default:
                return true;
        }
    }

    public async Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken)
    {
        if (!TryGetAnimationParameters(out var duration, out var easing, out var fromScale))
        {
            // 动画已关闭：直接归位，避免上一次过渡的残留值把元素留在合成层上
            Reset(from);
            Reset(to);
            return;
        }

        var tasks = new List<Task>(2);

        if (from is not null)
        {
            // 旧内容：轻微缩退 + 淡出
            var shrink = fromScale is >= 1 or <= 0 ? 1 : 1 / fromScale;
            Prepare(from, shrink);
            tasks.Add(BuildFade(duration, easing, 1d, 0d, 1d, shrink).RunAsync(from, cancellationToken));
        }

        if (to is not null)
        {
            // 新内容：略微放大推入 + 淡入
            Prepare(to, fromScale is >= 1 or <= 0 ? 1 : fromScale);
            tasks.Add(BuildFade(duration, easing, 0d, 1d,
                fromScale is >= 1 or <= 0 ? 1 : fromScale, 1d).RunAsync(to, cancellationToken));
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }

        if (!cancellationToken.IsCancellationRequested)
        {
            // 动画用 FillMode.Forward 保持值，结束后必须手动归位：
            //  - opacity 残留会让下一次淡出的起点错误
            //  - RenderTransform 不为 null 会把元素提升为独立合成层
            Reset(from);
            Reset(to);
        }
    }

    /// <summary>
    ///     确保目标上存在一个由本过渡独占的 <see cref="ScaleTransform" />。
    /// </summary>
    /// <remarks>
    ///     这里刻意直接改写 RenderTransform，而不是通过过渡动画完成，原因见
    ///     <see cref="BuildFade" /> 的说明。
    ///     若调用方另有 RenderTransform 需求（例如 hover 放大），本过渡会覆盖它，
    ///     这类宿主应改用不带缩放的过渡（<c>fromScale = 1</c>）。
    /// </remarks>
    private static void Prepare(Visual visual, double initialScale)
    {
        visual.RenderTransform = new ScaleTransform(initialScale, initialScale);
    }

    /// <summary>
    ///     构建「淡入/淡出 + 缩放」动画。
    /// </summary>
    /// <remarks>
    ///     <b>为什么动画的是 <see cref="ScaleTransform.ScaleXProperty" /> 而不是
    ///     <see cref="Visual.RenderTransformProperty" />：</b>
    ///     Avalonia 的动画器为<b>属性类型</b>自动注册（见 <c>Animation.AnimatorRegistry</c>），
    ///     注册表里<b>没有 <c>ITransform</c> / <c>TransformOperations</c> 的条目</b>，
    ///     所以对 RenderTransform 做关键帧动画会直接抛
    ///     <c>InvalidOperationException: No animator registered for the property RenderTransform</c>。
    ///     而 <c>ScaleX</c>/<c>ScaleY</c> 是 <see cref="Transform" /> 上的 double 属性，
    ///     会命中 <c>DoubleAnimator</c>（内部由 <c>TransformAnimator</c> 代理到具体变换对象），
    ///     这也正是 Avalonia 自带 <c>PageSlide</c> 的做法（它动画 <c>TranslateTransform.X</c>）。
    ///     <para>
    ///         <c>Animation.Animators</c> 与 <c>Animator&lt;T&gt;</c> 均为 internal，外部无法手动补注册，
    ///         因此只能走这条「自动解析」路径。同理，官方也明确不支持对
    ///         <c>RenderTransform</c> 使用 <c>TransformOperationsTransition</c>。
    ///     </para>
    /// </remarks>
    private static Animation BuildFade(
        TimeSpan duration, Easing easing,
        double fromOpacity, double toOpacity,
        double fromScale, double toScale)
    {
        var animation = new Animation
        {
            Duration = duration,
            Easing = easing,
            FillMode = FillMode.Forward
        };

        animation.Children.Add(new KeyFrame
        {
            Cue = new Cue(0d),
            Setters =
            {
                new Setter(Visual.OpacityProperty, fromOpacity),
                new Setter(ScaleTransform.ScaleXProperty, fromScale),
                new Setter(ScaleTransform.ScaleYProperty, fromScale)
            }
        });
        animation.Children.Add(new KeyFrame
        {
            Cue = new Cue(1d),
            Setters =
            {
                new Setter(Visual.OpacityProperty, toOpacity),
                new Setter(ScaleTransform.ScaleXProperty, toScale),
                new Setter(ScaleTransform.ScaleYProperty, toScale)
            }
        });

        return animation;
    }

    /// <summary>把过渡期间可能残留的动画值恢复到中性状态。</summary>
    private static void Reset(Visual? visual)
    {
        if (visual is null)
        {
            return;
        }

        visual.Opacity = 1;
        visual.RenderTransform = null;
    }
}
