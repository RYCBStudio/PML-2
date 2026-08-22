using System;
using System.ComponentModel;
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

        // 非点击导航（代码调用 NavigateToPage，如托盘/悬浮窗/页面内跳转）时，同步 NavigationView 选中指示条
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainPageFrameViewModel.SelectedTag))
        {
            SyncNavSelection(((MainPageFrameViewModel)sender).SelectedTag);
        }
    }

    // 按 Tag 找到对应的菜单项并设为选中，驱动 NavigationView 移动指示条（设置 SelectedItem 是权威选中路径）
    private void SyncNavSelection(string tag)
    {
        foreach (var item in NavView.MenuItems)
        {
            if (item is NavigationViewItem navItem &&
                string.Equals(navItem.Tag?.ToString(), tag, StringComparison.Ordinal))
            {
                NavView.SelectedItem = navItem;
                return;
            }
        }
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