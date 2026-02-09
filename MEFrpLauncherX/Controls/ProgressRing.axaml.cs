using System;
using Avalonia;
using Avalonia.Controls;

namespace MEFrpLauncherX.Controls;

public partial class ProgressRing : UserControl
{
    public static Control? ProgressContent;
    
    public static readonly StyledProperty<bool> IsIndeterminateProperty =
        AvaloniaProperty.Register<ProgressRing, bool>(nameof(IsIndeterminate));

    public bool IsIndeterminate
    {
        get => GetValue(IsIndeterminateProperty);
        set => SetValue(IsIndeterminateProperty, value);
    }
    
    public static readonly StyledProperty<bool> IsVisibleProperty =
        AvaloniaProperty.Register<ProgressRing, bool>(nameof(IsVisible));

    public bool IsVisible
    {
        get => GetValue(IsVisibleProperty);
        set => SetValue(IsVisibleProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (ProgressContent is null)
        {
            return;
        }

        ProgressContent = (Control)Activator.CreateInstance(ProgressContent?.GetType());
    }

    public ProgressRing()
    {
        InitializeComponent();
        DataContext = this;
        if (ProgressContent != null)
        {
            Presenter.Child = ProgressContent;
        }
    }
}