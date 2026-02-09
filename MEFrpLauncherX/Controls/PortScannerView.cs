using Avalonia.Controls;
using MEFrpLauncherX.ViewModels;

namespace MEFrpLauncherX.Controls;

public partial class PortScannerView: UserControl
{
    public PortScannerView()
    {
        InitializeComponent();
        DataContext = new PortScannerViewModel();
    }
    
    public new PortScannerViewModel DataContext
    {
        get => (PortScannerViewModel)base.DataContext;
        set => base.DataContext = value;
    }
}