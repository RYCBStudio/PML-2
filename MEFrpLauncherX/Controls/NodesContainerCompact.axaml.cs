using System.Threading.Tasks;
using Avalonia.Controls;
using MEFrpLauncherX.Core.MEFIntegrated;
using MEFrpLauncherX.Models;
using MEFrpLauncherX.ViewModels;

namespace MEFrpLauncherX.Controls;

public partial class NodesContainerCompact : UserControl, INodeContainer
{
    public NodesContainerCompact(NodesContainerViewModel? viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
        //viewModel?.SelectedRegion = "all";
    }

    public NodesContainerViewModel? ViewModel
    {
        get;
        set;
    }

    public Task LoadNodesAsync(InfoClasses.NodesListInfo listInfo, InfoClasses.NodesStatusInfo statusInfo)
    {
        if (DataContext is NodesContainerViewModel vm)
        {
            return vm.LoadNodesAsync(listInfo, statusInfo);
        }

        return Task.CompletedTask;
    }
}