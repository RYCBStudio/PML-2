using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace MEFrpLauncherX.Controls.PortalEasterEgg;

/// <summary>
///     传送门彩蛋的宿主覆盖层。
/// </summary>
/// <remarks>
///     <para>
///         刻意<b>不开新窗口</b>：覆盖层被追加到当前窗口内容根 <see cref="Panel" /> 的最上层，
///         因此不占任务栏、不产生模态对话框、不抢焦点，也不会触发任何窗口生命周期逻辑。
///     </para>
///     <para>
///         三种退出方式：Esc（窗口级 KeyDown，不依赖焦点）、鼠标点击（有 0.45s 宽限期，
///         避免触发彩蛋的那 7 次点击顺手把它关掉）、动画自然结束。
///         退出时统一走 <see cref="Teardown" />：停动画、退订窗口事件、从父面板移除，
///         并保证回调只执行一次。
///     </para>
/// </remarks>
internal sealed class PortalEasterEggOverlay : Panel
{
    /// <summary>触发后多久内忽略鼠标点击（秒）。</summary>
    private const double DismissGraceSeconds = 0.45;

    private readonly PortalEasterEggView _view;
    private readonly Action? _closed;

    private Window? _window;
    private DateTimeOffset _openedAt;
    private bool _finished;

    private PortalEasterEggOverlay(Action? closed)
    {
        _closed = closed;
        // 透明但非 null：让整块区域都能命中测试，点击任意位置都能退出
        Background = Brushes.Transparent;
        ZIndex = 100;

        _view = new PortalEasterEggView();
        _view.Completed += OnAnimationCompleted;
        Children.Add(_view);
    }

    /// <summary>
    ///     在 <paramref name="topLevel" /> 所属窗口上播放彩蛋。
    /// </summary>
    /// <param name="topLevel">触发点所在的顶层容器。</param>
    /// <param name="closed">覆盖层被移除后调用（无论自然结束还是被用户中断），只会执行一次。</param>
    public static void Show(TopLevel topLevel, Action? closed = null)
    {
        if (topLevel is not Window window || window.Content is not Panel root)
        {
            // 主窗口内容根不是 Panel 时安静放弃：彩蛋属于纯装饰，不能因此影响 About 页
            closed?.Invoke();
            return;
        }

        var overlay = new PortalEasterEggOverlay(closed);
        root.Children.Add(overlay);
        overlay.Attach(window);
    }

    private void Attach(Window window)
    {
        _window = window;
        _openedAt = DateTimeOffset.UtcNow;

        // 不抢焦点：Esc 由窗口级事件接收，页面原有的键盘交互在动画期间保持原状
        window.KeyDown += OnWindowKeyDown;

        _view.Start();
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;
        Teardown();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (_finished ||
            (DateTimeOffset.UtcNow - _openedAt).TotalSeconds < DismissGraceSeconds)
        {
            return;
        }

        e.Handled = true;
        Teardown();
    }

    private void OnAnimationCompleted(object? sender, EventArgs e) => Teardown();

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // 窗口被关闭 / 内容被替换时也会走到这里，保证事件订阅一并释放
        Teardown();
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>幂等收尾：停动画、退订、摘除自身、通知调用方。</summary>
    private void Teardown()
    {
        if (_finished)
        {
            return;
        }

        _finished = true;

        _view.Completed -= OnAnimationCompleted;
        _view.Stop();

        if (_window is not null)
        {
            _window.KeyDown -= OnWindowKeyDown;
            _window = null;
        }

        if (Parent is Panel root)
        {
            root.Children.Remove(this);
        }

        Children.Remove(_view);

        // 最后才回调：调用方在回调里可以立刻再次触发（冷却由 PortalEasterEggTrigger 负责）
        _closed?.Invoke();
    }
}
