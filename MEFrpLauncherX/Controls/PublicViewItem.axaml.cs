using Avalonia;
using Avalonia.Controls;

namespace MEFrpLauncherX.Controls;

public partial class PublicViewItem : UserControl
{
    public static readonly DirectProperty<PublicViewItem, int> TargetNumberProperty =
        AvaloniaProperty.RegisterDirect<PublicViewItem, int>(nameof(TargetNumber), pvi => pvi.TargetNumber,
            (pvi, target) => pvi.TargetNumber = target);

    public int TargetNumber
    {
        get;
        set => SetAndRaise(TargetNumberProperty, ref field, value);
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

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TargetNumberProperty)
        {
            TargetNumber = change.GetNewValue<int>();
            Number.TargetNumber = TargetNumber;
        }
    }
}