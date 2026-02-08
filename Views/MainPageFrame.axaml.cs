using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using MEFrpLauncherX.ViewModels;

namespace MEFrpLauncherX.Views;

public partial class MainPageFrame : UserControl
{
    public MainPageFrame()
    {
        InitializeComponent();
        var viewModel = new MainPageFrameViewModel();
        DataContext = viewModel;
        MainPageFrameViewModel.Instance = viewModel;
        MainPageFrameViewModel.Instance.IsLoading = true;
    }

    private void OnNavigationViewItemInvoked(object sender, NavigationViewItemInvokedEventArgs e)
    {
        if (e.InvokedItemContainer is NavigationViewItem item)
        {
            var viewModel = DataContext as MainPageFrameViewModel;
            viewModel?.NavigateToPage(item.Tag);
        }
    }
}

public static class Extensions
{
    extension(Control ctrl)
    {
        public void Show()
        {
            Dispatcher.UIThread.Invoke((Action)(() =>
                ctrl.IsVisible = true));
        }

        public void Hide()
        {
            Dispatcher.UIThread.Invoke((Action)(() =>
                ctrl.IsVisible = false));
        }

        public void Collapse()
        {
            Dispatcher.UIThread.Invoke((Action)(() =>
                ctrl.IsVisible = false));
        }
    }

    public static bool ContainsAny(this string str, params string[] tokens)
    {
        return tokens.Any(str.Contains);
    }
}