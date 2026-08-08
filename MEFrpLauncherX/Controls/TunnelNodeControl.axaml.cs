using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.ViewModels.Controls;

namespace MEFrpLauncherX.Controls;

public partial class TunnelNodeControl : UserControl
{
    public static readonly BoolToColorConverter BoolToColorConverter = new();
    public static readonly BoolToStatusConverter BoolToStatusConverter = new();
    public static readonly BoolToBorderThicknessConverter BoolToBorderThicknessConverter = new();
    public static readonly SelectedToBorderBrushConverter SelectedToBorderBrushConverter = new();
    public static readonly LoadPercentColorConverter LoadPercentColorConverter = new();

    public TunnelNodeControl()
    {
        InitializeComponent();
        if (Design.IsDesignMode)
        {
            DataContext = new TunnelNodeViewModel();
        }
    }


    private async void PerformAnimation(object? sender, VisualTreeAttachmentEventArgs e)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(100));
        var pg = sender as ProgressBar;
        if (pg == null)
        {
            return;
        }

        // 更精细的控制
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