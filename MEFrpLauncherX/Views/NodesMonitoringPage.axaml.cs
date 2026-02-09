using Avalonia;
using Avalonia.Controls;
using MEFrpLauncherX.ViewModels;

namespace MEFrpLauncherX.Views;

public partial class NodesMonitoringPage : UserControl
{
    public static NodesMonitoringPage Instance { get; private set; }

    public NodesMonitoringPage()
    {
        InitializeComponent();
        Instance = this;
        DataContext = null;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        DataContext = new NodesOverviewViewModel();
    }
}