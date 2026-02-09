using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MEFrpLauncherX.Core;

namespace MEFrpLauncherX.Controls;

public partial class TextBlockWithHeader : UserControl
{
    public static readonly StyledProperty<object> ContentProperty =
        AvaloniaProperty.Register<TextBlockWithHeader, object>(nameof(Content));
    
    public static readonly StyledProperty<string> HeaderProperty =
        AvaloniaProperty.Register<TextBlockWithHeader, string>(nameof(Header));

    public object Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public string Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public bool IsDark
    {
        get => ConfigManager.CurrentConfig.Theme.Equals("Dark", StringComparison.OrdinalIgnoreCase);
    }

    public TextBlockWithHeader()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}