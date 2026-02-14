using Avalonia;
using Avalonia.Controls;

namespace MEFrpLauncherX.Controls;

public partial class Card : UserControl
{
    public static StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<Card, string>(nameof(Title));
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }public static StyledProperty<object?> CardContentProperty =
        AvaloniaProperty.Register<Card, object?>(nameof(CardContent));
    public object? CardContent
    {
        get => GetValue(CardContentProperty);
        set => SetValue(CardContentProperty, value);
    }

    public Card()
    {
        InitializeComponent();
        if (Design.IsDesignMode)
        {
            Title = "Test111";
            CardContent = "Test222";
        }
    }
}