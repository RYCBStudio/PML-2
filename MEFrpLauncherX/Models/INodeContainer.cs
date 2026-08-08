using System.Threading.Tasks;
using Avalonia;
using MEFrpLauncherX.Core.MEFIntegrated;
using MEFrpLauncherX.ViewModels;

namespace MEFrpLauncherX.Models;

public interface INodeContainer : IDataContextProvider
{
    NodesContainerViewModel? ViewModel
    {
        get;
        set;
    }

    Task LoadNodesAsync(InfoClasses.NodesListInfo listInfo, InfoClasses.NodesStatusInfo statusInfo);
}