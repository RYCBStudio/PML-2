using Avalonia;
using Avalonia.Controls;

namespace MEFrpLauncherX.Controls;

public partial class PublicViewItem : UserControl
{
    public static readonly StyledProperty<int> TargetNumberProperty =
        AvaloniaProperty.Register<PublicViewItem, int>(nameof(TargetNumber));

    public int TargetNumber
    {
        get => GetValue(TargetNumberProperty);
        set => SetValue(TargetNumberProperty, value);
    }

    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<PublicViewItem, string>(nameof(Description), "");

    public string Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public PublicViewItem()
    {
        InitializeComponent();
        DataContext = this;
    }
}