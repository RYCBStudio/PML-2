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

        // 非点击导航（代码调用 NavigateToPage，如托盘/悬浮窗/页面内跳转）时，同步 FANavigationView 选中指示条
        viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // 冷启动时链接可能在用户登录之前就已触发（当时主界面还不存在，导航请求被挂起）。
        // 这里补做那次请求，否则终端页永远不显示、终端进程也不会启动。
        MainPageFrameViewModel.ConsumePendingTerminalNavigation();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainPageFrameViewModel.SelectedTag))
        {
            SyncNavSelection(((MainPageFrameViewModel)sender).SelectedTag);
        }
    }

    // 按 Tag 找到对应的菜单项并设为选中，驱动 FANavigationView 移动指示条（设置 SelectedItem 是权威选中路径）
    private void SyncNavSelection(string tag)
    {
        foreach (var item in NavView.MenuItems)
        {
            if (item is FANavigationViewItem navItem &&
                string.Equals(navItem.Tag?.ToString(), tag, StringComparison.Ordinal))
            {
                NavView.SelectedItem = navItem;
                return;
            }
        }
    }

    private void OnFANavigationViewItemInvoked(object sender, FANavigationViewItemInvokedEventArgs e)
    {
        if (e.InvokedItemContainer is FANavigationViewItem item)
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

            if (ctrl is FAInfoBar bar)
            {
                bar.IsOpen = true;
            }
        }

        public void Hide()
        {
            Dispatcher.UIThread.Invoke((Action)(() =>
                ctrl.IsVisible = false));
            
            
            if (ctrl is FAInfoBar bar)
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