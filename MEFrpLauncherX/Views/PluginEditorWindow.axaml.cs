using Avalonia.Controls;
using MEFrpLauncherX.ViewModels.PluginEditor;

namespace MEFrpLauncherX.Views;

public partial class PluginEditorWindow : Window
{
    public PluginEditorWindow(PluginEditorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += Close;
    }
}
