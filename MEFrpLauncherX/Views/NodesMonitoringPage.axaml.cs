using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;
using MEFrpLauncherX.ViewModels;

namespace MEFrpLauncherX.Views;

public partial class NodesMonitoringPage : UserControl
{
    public NodesMonitoringPage()
    {
        InitializeComponent();
        Instance = this;
        DataContext = null;
    }

    public static NodesMonitoringPage Instance
    {
        get;
        private set;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        DataContext = new NodesOverviewViewModel();
    }

    private async void PerformAnimation(object? sender, VisualTreeAttachmentEventArgs e)
    {
        var pg = sender as ProgressBar;
        if (pg == null)
        {
            return;
        }

        var animation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(800),
            IterationCount = new IterationCount(1),
            PlaybackDirection = PlaybackDirection.Normal,
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame
                {
                    Setters = { new Setter(RangeBase.ValueProperty, 0d) },
                    Cue = new Cue(0d)
                },
                new KeyFrame
                {
                    Setters = { new Setter(RangeBase.ValueProperty, pg.Value * 0.3) },
                    Cue = new Cue(0.3d)
                },
                new KeyFrame
                {
                    Setters = { new Setter(RangeBase.ValueProperty, pg.Value) },
                    Cue = new Cue(1d)
                }
            },
            Easing = Easing.Parse("CubicEaseIn")
        };

        await animation.RunAsync(pg);
    }
}