using Avalonia.Controls;
using MEFrpLauncherX.ViewModels;

namespace MEFrpLauncherX.Views;

public partial class ThemeEditor : Window
{
    public ThemeEditor() : this(null)
    {
    }

    public ThemeEditor(string? themeFilePath)
    {
        InitializeComponent();
        var viewModel = new ThemeEditorViewModel(themeFilePath);
        DataContext = viewModel;
    }
}