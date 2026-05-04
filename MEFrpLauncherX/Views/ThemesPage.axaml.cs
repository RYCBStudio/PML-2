using Avalonia.Controls;

namespace MEFrpLauncherX.Views;

public partial class ThemesPage : UserControl
{
    public ThemesPage()
    {
        InitializeComponent();
        DataContext = new ViewModels.ThemesPageViewModel();
    }
}