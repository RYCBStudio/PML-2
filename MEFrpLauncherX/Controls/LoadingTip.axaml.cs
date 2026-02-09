using Avalonia;
using Avalonia.Controls;

namespace MEFrpLauncherX.Controls;

public partial class LoadingTip : UserControl
{
    public string Tip
    {
        get => GetValue(TipProperty);
        set => SetValue(TipProperty, value);
    }
    public static readonly StyledProperty<string> TipProperty =
        AvaloniaProperty.Register<LoadingTip, string>(nameof(Tip));

    public LoadingTip()
    {
        InitializeComponent();
    }
    public LoadingTip(string tip)
    {
        InitializeComponent();
        Tip = tip;
    }
}