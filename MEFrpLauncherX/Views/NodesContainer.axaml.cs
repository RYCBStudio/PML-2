using System.Threading.Tasks;
using Avalonia.Controls;
using MEFrpLauncherX.Core.MEFIntergrated;
using MEFrpLauncherX.ViewModels;

namespace MEFrpLauncherX.Views;

public partial class NodesContainer : UserControl
{
    public static NodesContainer Instance
    {
        get;
        private set;
    }
    public NodesContainerViewModel ViewModel { get; } = new();
    
    public NodesContainer()
    {
        InitializeComponent();
        DataContext = ViewModel;
        Instance = this;
    }

    public async Task LoadNodesAsync(InfoClasses.NodesListInfo listInfo, InfoClasses.NodesStatusInfo statusInfo)
    {
        await ViewModel.LoadNodesAsync(listInfo, statusInfo);
    }
}