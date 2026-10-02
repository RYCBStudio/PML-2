using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MEFrpLauncherX.Styling;

namespace MEFrpLauncherX.Behaviours;

/// <summary>
///     <see cref="TabStrip" /> 选中项切换时的内容区过渡行为。
/// </summary>
/// <remarks>
///     <para>项目里存在两类「标签页」控件，能力并不对等：</para>
///     <list type="bullet">
///         <item>
///             <description>
///                 <see cref="TabControl" />：Avalonia 12.1.3 原生支持 <c>PageTransition</c>
///                 （配合 Fluent 主题自带的 <c>PART_SelectedContentHost2</c>），设属性即可。
///             </description>
///         </item>
///         <item>
///             <description>
///                 <b><see cref="TabStrip" /> 只负责标签栏</b>，是普通 <see cref="SelectingItemsControl" />，
///                 既不承载内容、也没有过渡机制。因此「TabStrip 的内容切换」只能由本行为代劳：
///                 监听选中变化，对 <see cref="TargetProperty" /> 指定的内容宿主播放过渡。
///             </description>
///         </item>
///     </list>
///     <para>
///         只动 <see cref="Visual.OpacityProperty" /> 与 <see cref="Visual.RenderTransformProperty" />，
///         不触碰 Width/Height/Margin/Padding，因此不会触发每帧完整布局。
///     </para>
/// </remarks>
public sealed class TabStripContentTransitionBehavior
{
    /// <summary>要播放过渡的内容宿主（通常是承载切换内容的 ContentControl）。</summary>
    public static readonly AttachedProperty<Control?> TargetProperty =
        AvaloniaProperty.RegisterAttached<TabStripContentTransitionBehavior, TabStrip, Control?>("Target");

    /// <summary>是否启用切换过渡，默认启用；可对单个 TabStrip 关闭。</summary>
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<TabStripContentTransitionBehavior, TabStrip, bool>(
            "IsEnabled", true);

    public static void SetTarget(TabStrip obj, Control? value) => obj.SetValue(TargetProperty, value);

    public static Control? GetTarget(TabStrip obj) => obj.GetValue(TargetProperty);

    public static void SetIsEnabled(TabStrip obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(TabStrip obj) => obj.GetValue(IsEnabledProperty);

    /// <summary>
    ///     每个 TabStrip 一份的取消源，用于在快速连续切换时取消上一段过渡。
    /// </summary>
    /// <remarks>
    ///     <b>为什么必须取消旧过渡：</b>TabStrip 的内容宿主是<b>同一个控件</b>
    ///     （不像 TabControl 那样轮换两个 ContentPresenter）。
    ///     若不取消，旧过渡完成后会执行归位（Opacity=1、RenderTransform=null），
    ///     把新过渡正在进行的变换一起抹掉，表现为「切换时闪一下」。
    ///     过渡内部对「已取消」的情况会跳过归位，因此最新那段动画始终独占该控件。
    /// </remarks>
    private static readonly ConditionalWeakTable<TabStrip, CancellationTokenSource> PendingTransitions = new();

    static TabStripContentTransitionBehavior()
    {
        // 只在挂载时统一装配。
        // 不能用「附加属性 Changed 就立刻装配」的做法：XAML 里 Target 与 IsEnabled 的赋值顺序
        // 决定了哪个先触发，先触发的那个看到另一个还是默认值，装配结果不可预期。
        TargetProperty.Changed.AddClassHandler<TabStrip>((tabStrip, _) => ScheduleAttach(tabStrip));
        IsEnabledProperty.Changed.AddClassHandler<TabStrip>((tabStrip, _) => ScheduleAttach(tabStrip));
    }

    private static void ScheduleAttach(TabStrip tabStrip)
    {
        // Attach 本身是幂等的（内部先 -= 再 +=），因此这里可以「先挂事件、再立即尝试装配」：
        // 若此刻已挂载则立刻生效，否则等 AttachedToVisualTree 到达后再装配。
        tabStrip.AttachedToVisualTree -= OnAttachedToVisualTree;
        tabStrip.AttachedToVisualTree += OnAttachedToVisualTree;
        Attach(tabStrip);
    }

    private static void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is TabStrip tabStrip)
        {
            tabStrip.AttachedToVisualTree -= OnAttachedToVisualTree;
            Attach(tabStrip);
        }
    }

    private static void Attach(TabStrip tabStrip)
    {
        tabStrip.SelectionChanged -= OnSelectionChanged;
        tabStrip.SelectionChanged += OnSelectionChanged;

        // 首次显示也走一遍入场动画。
        // 必须等一帧：此刻 Target 的内容往往还是 null，直接播看不到效果。
        Dispatcher.UIThread.Post(() => PlayEntrance(tabStrip), DispatcherPriority.Background);
    }

    private static void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is TabStrip tabStrip)
        {
            PlayEntrance(tabStrip);
        }
    }

    /// <summary>
    ///     手动重播内容区入场过渡（切换选中项时自动触发，一般无需调用）。
    /// </summary>
    /// <remarks>
    ///     复用 <see cref="TabContentFadeScaleTransition" /> 的「淡入 + 缩放」实现，避免两套动画各写一遍：
    ///     <c>from = null</c> 表示只播入场（没有需要淡出的旧内容，因为 TabStrip 的内容宿主是同一个控件）。
    ///     该过渡内部会自行读取「动画程度」配置，并负责把动画结束后残留的
    ///     Opacity / RenderTransform 归位。
    /// </remarks>
    public static void PlayEntrance(TabStrip tabStrip)
    {
        if (!GetIsEnabled(tabStrip))
        {
            return;
        }

        var target = GetTarget(tabStrip);
        if (target is null || !target.IsVisible)
        {
            return;
        }

        _ = RunAsync(tabStrip, target);
    }

    private static async Task RunAsync(TabStrip tabStrip, Control target)
    {
        // 取消上一段尚未结束的过渡，保证新动画独占该内容宿主
        if (PendingTransitions.TryGetValue(tabStrip, out var previous))
        {
            previous.Cancel();
        }

        var cts = new CancellationTokenSource();
        PendingTransitions.Remove(tabStrip);
        PendingTransitions.Add(tabStrip, cts);
        var token = cts.Token;

        try
        {
            // 第二个参数为 null：TabStrip 没有「旧内容宿主」可淡出，
            // 空壳 TabStripItem 也不会被选中，故此路径等价于纯入场。
            await TabContentFadeScaleTransition.Shared.Start(null, target, true, token);
        }
        catch (OperationCanceledException)
        {
            // 快速连续切换时上一次动画会被取消，属正常路径
        }
        catch (Exception ex)
        {
            // 过渡失败不应该把切换本身搞崩
            Core.App.CurrentLogger?.Error(ex, "TabStrip 内容过渡播放失败");
        }
        finally
        {
            // 只有未被取消（即「自己就是最新那段过渡」）时才清掉登记，
            // 避免把后来者登记的 CTS 一起清掉。CTS 本身总是要释放。
            if (!token.IsCancellationRequested)
            {
                PendingTransitions.Remove(tabStrip);
            }

            cts.Dispose();
        }
    }
}
