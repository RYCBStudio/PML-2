using Avalonia;
using Avalonia.Controls;
using FluentAvalonia.UI.Controls;

namespace MEFrpLauncherX.Controls;

public partial class IconContent : UserControl
{
    public static readonly DirectProperty<IconContent, Symbol> SymbolProperty = AvaloniaProperty.RegisterDirect<IconContent, Symbol>(
        nameof(Symbol), o => o.Symbol, (o, v) => o.Symbol = v);

    public Symbol Symbol
    {
        get;
        set => SetAndRaise(SymbolProperty, ref field, value);
    }

    public static readonly DirectProperty<IconContent, object?> LabelProperty = AvaloniaProperty.RegisterDirect<IconContent, object?>(
        nameof(Label), o => o.Label, (o, v) => o.Label = v);

    public object? Label
    {
        get;
        set => SetAndRaise(LabelProperty, ref field, value);
    }

    public IconContent()
    {
        InitializeComponent();
    }
}