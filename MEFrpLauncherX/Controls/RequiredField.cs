using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace MEFrpLauncherX.Controls;

public class RequiredField : StackPanel
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<RequiredField, string>(nameof(Label));

    public static readonly StyledProperty<bool> IsRequiredProperty =
        AvaloniaProperty.Register<RequiredField, bool>(nameof(IsRequired), true);

    public RequiredField()
    {
        Orientation = Orientation.Vertical;
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public bool IsRequired
    {
        get => GetValue(IsRequiredProperty);
        set => SetValue(IsRequiredProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == LabelProperty || change.Property == IsRequiredProperty)
        {
            UpdateLabel();
        }
    }

    private void UpdateLabel()
    {
        Children.Clear();

        var labelPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        labelPanel.Children.Add(new TextBlock
        {
            Text = Label,
            FontFamily = Application.Current?.FindResource("GlobalFontFamily") as FontFamily
        });

        if (IsRequired)
        {
            labelPanel.Children.Add(new TextBlock
            {
                Text = "•",
                Foreground = Brushes.Red,
                FontWeight = FontWeight.Bold
            });
        }

        Children.Add(labelPanel);
    }
}