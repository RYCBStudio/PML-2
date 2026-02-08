using Avalonia.Controls;

namespace MEFrpLauncherX.Views;

public partial class CustomPresenter : Window
{
    public CustomPresenter(Control ctrl)
    {
        InitializeComponent();
        Main.Content = ctrl;
    }
}