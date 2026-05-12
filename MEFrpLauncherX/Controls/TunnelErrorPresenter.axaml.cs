using System.Collections.Generic;
using Avalonia.Collections;
using Avalonia.Controls;

namespace MEFrpLauncherX.Controls;

public partial class TunnelErrorPresenter : UserControl
{
    public TunnelErrorPresenter(IEnumerable<string> errors)
    {
        InitializeComponent();
        DataContext = this;
        Errors.AddRange(errors);
    }

    public AvaloniaList<string> Errors
    {
        get;
    } = [];
}