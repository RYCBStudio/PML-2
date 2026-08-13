using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MEFrpLauncherX.Controls;

public partial class DailyTipDialog : UserControl
{
    
    /// <summary>
    /// Reserved for designer only.
    /// </summary>
    public DailyTipDialog()
    {
        InitializeComponent();
    }
    
    public DailyTipDialog(string tipContent)
    {
        InitializeComponent();
        TipContent.Value = tipContent;
    }
    
    public void UpdateContent(string tipContent)
    {
        TipContent.Value = tipContent;
    }
}