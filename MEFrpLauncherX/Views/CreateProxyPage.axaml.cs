using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MEFrpLauncherX.Controls;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.MEFIntergrated;
using MEFrpLauncherX.ViewModels;
using MsBox.Avalonia;
using MessageBox = MEFrpLauncherX.Core.Controls.MessageBox;
using MessageBoxIcon = MEFrpLauncherX.Core.Controls.MessageBoxIcon;

namespace MEFrpLauncherX.Views;

public partial class CreateProxyPage : UserControl, INotifyPropertyChanged
{
    private int _index;
    private TunnelNodeViewModel? _selected;

    public Control CurrentPage
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(CurrentPage));
            }
        }
    }

    public static CreateProxyPage Instance
    {
        get;
        private set;
    }

    public event Func<Task<bool>> OnCreateProxy;

    public CreateProxyPage()
    {
        InitializeComponent();
        DataContext = this;
        AttachedToVisualTree += CreateProxyPage_Loaded;
        Instance = this;
    }

    private async void CreateProxyPage_Loaded(object sender, VisualTreeAttachmentEventArgs e)
    {
        MainPageFrameViewModel.Instance.IsLoading = true;
        _index = 0;
        var nc = new NodesContainer();
        CurrentPage = nc;

        var status = MEFApiConverter.CurrentNodesStatusInfo;
        status ??= new InfoClasses.NodesStatusInfo
            { NodesStatus = (await MEFApiConverter.GetNodesStatusAsync()).data };

        var listInfo = MEFApiConverter.CurrentNodesListInfo;
        listInfo ??= new InfoClasses.NodesListInfo
        {
            NodesList = (await MEFApiConverter.GetNodesInfoAsync()).data
        };

        await nc.LoadNodesAsync(listInfo, status);
        (nc.DataContext as NodesContainerViewModel).NodeSelected += CreateProxyPage_NodeSelected;
        MainPageFrameViewModel.Instance.IsLoading = false;
    }

    private void CreateProxyPage_NodeSelected(TunnelNodeViewModel? obj)
    {
        _selected = obj;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private async void Next(object sender, RoutedEventArgs e)
    {
        _index++;
        switch (_index)
        {
            case 0:
                CreateProxyPage_Loaded(null, null);
                break;
            case 1:
                if (_selected != null)
                {
                    if (_selected.IsOverloaded)
                    {
                        await MessageBox.ShowAsync("节点已过载，无法再创建隧道", "", MessageBoxIcon.Error);
                        _index--;
                        break;
                    }

                    var cp = new CreateProxy(_selected);
                    CurrentPage = cp;
                }
                else
                {
                    await MessageBoxManager.GetMessageBoxStandard("错误", "请选择一个节点").ShowAsync();
                    _index--;
                }

                break;
            case 2:
                if (OnCreateProxy is null)
                {
                    _index -= 2;
                    Next(null, null);
                }

                if (await OnCreateProxy?.Invoke())
                {
                    Growl.Success("成功创建隧道");
                    CurrentPage = new OperationSuccess();
                    NextBtn.IsEnabled = false;
                }
                else
                {
                    Growl.Error("创建隧道失败");
                }

                _index--;
                break;
        }
    }

    private void Back(object sender, RoutedEventArgs e)
    {
        if (_index > 0)
        {
            _index--;
            _selected = null;
            NextBtn.IsEnabled = true;
        }

        if (_index == 0)
        {
            CreateProxyPage_Loaded(null, null);
        }
    }

    private async void Refresh(object? sender, RoutedEventArgs e)
    {
        MEFApiConverter.CurrentNodesStatusInfo = new InfoClasses.NodesStatusInfo
        {
            NodesStatus = (await MEFApiConverter.GetNodesStatusAsync()).data
        };
        MEFApiConverter.CurrentNodesListInfo = new InfoClasses.NodesListInfo
        {
            NodesList = (await MEFApiConverter.GetNodesInfoAsync()).data
        };
        CreateProxyPage_Loaded(null, null);
    }
}