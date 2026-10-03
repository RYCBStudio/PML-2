using Avalonia.Controls;
using MEFrpLauncherX.ViewModels;

namespace MEFrpLauncherX.Views;

/// <summary>
///     DNS 账户管理窗口（26.4 阶段 B）。
///     视图只负责装配 <see cref="DnsAccountsWindowViewModel" /> 并转发生命周期事件，
///     表单校验、动态字段、加密存储等业务逻辑全部位于 ViewModel。
/// </summary>
public partial class DnsAccountsWindow : Window
{
    private readonly DnsAccountsWindowViewModel _viewModel;

    public DnsAccountsWindow()
    {
        InitializeComponent();
        _viewModel = new DnsAccountsWindowViewModel();
        DataContext = _viewModel;

        _viewModel.RequestClose += Close;
        _viewModel.RequestFocusName += OnRequestFocusName;
    }

    /// <summary>新建账户后把焦点移到备注名输入框。</summary>
    private void OnRequestFocusName() => NameBox?.Focus();
}
