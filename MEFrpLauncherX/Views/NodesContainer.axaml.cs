using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Styling;
using MEFrpLauncherX.Core.MEFIntergrated;
using MEFrpLauncherX.ViewModels;

namespace MEFrpLauncherX.Views;

public partial class NodesContainer : UserControl
{
    public NodesContainer()
    {
        InitializeComponent();
        DataContext = ViewModel;
        Instance = this;
    }

    public static NodesContainer Instance
    {
        get;
        private set;
    }

    public NodesContainerViewModel ViewModel
    {
        get;
    } = new();

    public async Task LoadNodesAsync(InfoClasses.NodesListInfo listInfo, InfoClasses.NodesStatusInfo statusInfo) =>
        await ViewModel.LoadNodesAsync(listInfo, statusInfo);
    
    
    private async void PerformAnimation(object? sender, VisualTreeAttachmentEventArgs e)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(300));
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
                    Setters = { new Setter(ProgressBar.ValueProperty, 0d) },
                    Cue = new Cue(0d)
                },
                new KeyFrame
                {
                    Setters = { new Setter(ProgressBar.ValueProperty, pg.Value * 0.3) },
                    Cue = new Cue(0.3d)
                },
                new KeyFrame
                {
                    Setters = { new Setter(ProgressBar.ValueProperty, pg.Value) },
                    Cue = new Cue(1d)
                }
            },
            Easing = Easing.Parse("CubicEaseIn")
        };

        await animation.RunAsync(pg);
    }
}