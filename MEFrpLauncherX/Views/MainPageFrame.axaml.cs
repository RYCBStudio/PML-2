using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
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

    private void CloseNRTip(object? sender, PointerReleasedEventArgs e)
    {
        var viewModel = DataContext as MainPageFrameViewModel;
        viewModel?.NeedRestart = false;
    }
}

public static class Extensions
{
    public static bool ContainsAny(this string str, params string[] tokens) => tokens.Any(str.Contains);

    extension(Control ctrl)
    {
        public void Show()
        {
            Dispatcher.UIThread.Invoke((Action)(() =>
                ctrl.IsVisible = true));

            if (ctrl is InfoBar bar)
            {
                bar.IsOpen = true;
            }
        }

        public void Hide()
        {
            Dispatcher.UIThread.Invoke((Action)(() =>
                ctrl.IsVisible = false));
            
            
            if (ctrl is InfoBar bar)
            {
                bar.IsOpen = false;
            }
        }

        public void Collapse()
        {
            Dispatcher.UIThread.Invoke((Action)(() =>
                ctrl.IsVisible = false));
        }
    }
}