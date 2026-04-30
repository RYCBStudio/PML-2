using Avalonia.Controls;

namespace MEFrpLauncherX.Views;

public partial class ThemesPage : UserControl
{
    public ThemesPage()
    {
        InitializeComponent();
        this.DataContext = new ViewModels.ThemesPageViewModel();
    }
}