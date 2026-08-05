using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using MEFrpLauncherX.Core.MEFIntegrated;
using MEFrpLauncherX.Core.MEFIntergrated;
using MEFrpLauncherX.Models;
using MEFrpLauncherX.ViewModels;

namespace MEFrpLauncherX.Views;

public partial class NodesContainer : UserControl,INodeContainer
{
    public NodesContainer()
    {
        InitializeComponent();
        DataContext = ViewModel;
        Instance = this;
        AttachedToVisualTree += OnAttached;
    }

    /// <summary>
    /// 确保指定项在 ListBox 中可见并触发布局，实现对虚拟化 WrapPanel 的强制实现。
    /// </summary>
    public async Task EnsureItemVisibleAsync(object? item)
    {
        if (item == null) return;

        try
        {
            // 首先滚动到该项，触发虚拟化面板去实现该项
            await Dispatcher.UIThread.InvokeAsync(() => Nodes?.ScrollIntoView(item), DispatcherPriority.Render);

            // 给面板一点时间去真实化（短暂延时），然后强制刷新产生的容器的布局
            await Task.Delay(100);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    var lbi = Nodes?.ContainerFromItem(item) as ListBoxItem;
                    lbi?.InvalidateMeasure();
                    lbi?.InvalidateArrange();

                    // 同时刷新 ItemsPanelRoot
                    var panel = Nodes?.ItemsPanelRoot as Control;
                    panel?.InvalidateMeasure();
                    panel?.InvalidateArrange();
                }
                catch
                {
                    // 忽略
                }
            }, DispatcherPriority.Render);
        }
        catch
        {
            // 忽略
        }
    }

    private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        // 当控件被附加到可视化树时，确保内部 ListBox 的布局被刷新，
        // 以避免在从地图页面切换回该页面时，ItemsPanel 仍保持旧的测量信息导致偏移。
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                Nodes?.InvalidateMeasure();
                Nodes?.InvalidateArrange();
            }
            catch
            {
                // 忽略任何布局刷新时的异常
            }
        }, DispatcherPriority.Render);
    }

    public static NodesContainer Instance
    {
        get;
        private set;
    }

    public NodesContainerViewModel ViewModel
    {
        get;
        set;
    } = new();

    public async Task LoadNodesAsync(InfoClasses.NodesListInfo listInfo, InfoClasses.NodesStatusInfo statusInfo) =>
        await ViewModel.LoadNodesAsync(listInfo, statusInfo);
    
    
}