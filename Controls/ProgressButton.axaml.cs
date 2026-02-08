using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MEFrpLauncherX.Controls;

public partial class ProgressButton : UserControl
{
    public static readonly StyledProperty<object> BtnContentProperty =
        AvaloniaProperty.Register<ProgressButton, object>(nameof(BtnContent));

    public object BtnContent
    {
        get => GetValue(BtnContentProperty);
        set => SetValue(BtnContentProperty, value);
    }

    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<ProgressButton, double>(nameof(Progress));

    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public static readonly StyledProperty<bool> IsAccentProperty =
        AvaloniaProperty.Register<ProgressButton, bool>(nameof(IsAccent));

    public bool IsAccent
    {
        get => GetValue(IsAccentProperty);
        set => SetValue(IsAccentProperty, value);
    }

    public static readonly RoutedEvent<RoutedEventArgs> ClickEvent =
        RoutedEvent.Register<ProgressButton, RoutedEventArgs>(nameof(Click), RoutingStrategies.Bubble);

    public event EventHandler<RoutedEventArgs> Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    protected virtual void OnClick(object? sender, RoutedEventArgs e)
    {
        var args = new RoutedEventArgs(ClickEvent);
        RaiseEvent(args);
    }

    public ProgressButton()
    {
        InitializeComponent();
        DataContext = this;
        Button.Click += OnClick;
    }
}

