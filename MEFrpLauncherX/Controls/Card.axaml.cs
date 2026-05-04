using Avalonia;
using Avalonia.Controls;

namespace MEFrpLauncherX.Controls;

public partial class Card : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<Card, string>(nameof(Title));

    public static readonly StyledProperty<object?> CardContentProperty =
        AvaloniaProperty.Register<Card, object?>(nameof(CardContent));

    public Card()
    {
        InitializeComponent();
        if (Design.IsDesignMode)
        {
            Title = "Test111";
            CardContent = "Test222";
        }
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public object? CardContent
    {
        get => GetValue(CardContentProperty);
        set => SetValue(CardContentProperty, value);
    }
}