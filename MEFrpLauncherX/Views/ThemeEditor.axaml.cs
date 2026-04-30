using Avalonia.Controls;
using MEFrpLauncherX.ViewModels;

namespace MEFrpLauncherX.Views;

public partial class ThemeEditor : Window
{
    private ThemeEditorViewModel _viewModel;

    public ThemeEditor() : this(null)
    {
    }

    public ThemeEditor(string? themeFilePath)
    {
        InitializeComponent();
        _viewModel = new ThemeEditorViewModel(themeFilePath);
        DataContext = _viewModel;
    }
}