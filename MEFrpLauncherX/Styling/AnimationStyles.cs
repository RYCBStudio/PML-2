using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using FluentAvalonia.UI.Controls;

namespace MEFrpLauncherX.Styling;

/// <summary>
///     按动画程度生成全局入场动画样式, 支持运行时切换。
///     复刻原 App.axaml 中的 .r2l / InfoBar.animated-bar / .animated 三组动画。
/// </summary>
public static class AnimationStyles
{
    private static Styles _current;

    /// <summary>
    ///     应用指定的动画程度: 0=关闭动画 1=精简 2=标准。
    /// </summary>
    public static void Apply(int level)
    {
        if (Application.Current is null)
        {
            return;
        }

        if (_current is not null)
        {
            Application.Current.Styles.Remove(_current);
        }

        _current = Build(level);
        Application.Current.Styles.Add(_current);
    }

    private static Styles Build(int level)
    {
        var styles = new Styles();
        if (level <= 0)
        {
            return styles;
        }

        var reduced = level == 1;

        // :is(Control).r2l —— 从右侧淡入
        styles.Add(BuildStyle(
            Selectors.OfType<Control>(null).Class("r2l"),
            reduced
                ? BuildFadeAnimation(TimeSpan.FromSeconds(0.2), null, null)
                : BuildSlideAnimation(TimeSpan.FromSeconds(0.75), null, FillMode.Forward,
                    new QuarticEaseIn(), TranslateTransform.XProperty, 25)));

        // InfoBar.animated-bar —— 通知条入场
        styles.Add(BuildStyle(
            Selectors.OfType<InfoBar>(null).Class("animated-bar"),
            reduced
                ? BuildFadeAnimation(TimeSpan.FromSeconds(0.2), null, null)
                : BuildSlideAnimation(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(0.25), FillMode.Both,
                    new QuarticEaseInOut(), TranslateTransform.XProperty, 25)));

        // :is(Control).animated —— 页面区块上浮入场
        var animatedStyle = BuildStyle(
            Selectors.OfType<Control>(null).Class("animated"),
            reduced
                ? BuildFadeAnimation(TimeSpan.FromSeconds(0.2), null, null)
                : BuildSlideAnimation(TimeSpan.FromSeconds(0.75), null, FillMode.Both,
                    Easing.Parse("0.00, 1.00, 0.00, 1.00"), TranslateTransform.YProperty, 50));
        if (!reduced)
        {
            animatedStyle.Setters.Add(new Setter(Visual.RenderTransformProperty, new TranslateTransform
            {
                Y = -25
            }));
        }

        styles.Add(animatedStyle);
        return styles;
    }

    private static Style BuildStyle(Selector selector, Animation animation)
    {
        var style = new Style(_ => selector);
        style.Animations.Add(animation);
        return style;
    }

    private static Animation BuildFadeAnimation(TimeSpan duration, TimeSpan? delay, Easing easing) =>
        BuildAnimation(duration, delay, FillMode.Both, easing ?? new LinearEasing(), null, 0);

    private static Animation BuildSlideAnimation(TimeSpan duration, TimeSpan? delay, FillMode fillMode,
        Easing easing, StyledProperty<double> transformProperty, double offset) =>
        BuildAnimation(duration, delay, fillMode, easing, transformProperty, offset);

    private static Animation BuildAnimation(TimeSpan duration, TimeSpan? delay, FillMode fillMode, Easing easing,
        StyledProperty<double> transformProperty, double offset)
    {
        var animation = new Animation
        {
            Duration = duration,
            FillMode = fillMode,
            Easing = easing
        };
        if (delay is not null)
        {
            animation.Delay = delay.Value;
        }

        var startFrame = new KeyFrame
        {
            Cue = new Cue(0)
        };
        if (transformProperty is not null)
        {
            startFrame.Setters.Add(new Setter(transformProperty, offset));
        }

        startFrame.Setters.Add(new Setter(Visual.OpacityProperty, 0d));

        var endFrame = new KeyFrame
        {
            Cue = new Cue(1)
        };
        if (transformProperty is not null)
        {
            endFrame.Setters.Add(new Setter(transformProperty, 0d));
        }

        endFrame.Setters.Add(new Setter(Visual.OpacityProperty, 1d));

        animation.Children.Add(startFrame);
        animation.Children.Add(endFrame);
        return animation;
    }
}
