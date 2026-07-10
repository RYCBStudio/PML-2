using Avalonia.Controls;

namespace MEFrpLauncherX.Views;

public partial class PluginListPage : UserControl
{
    public PluginListPage()
    {
        InitializeComponent();
        DataContext = new ViewModels.PluginListViewModel();
    }
}
