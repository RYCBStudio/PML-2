using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Views;
using MsBox.Avalonia.ViewModels.Commands;
using ReactiveUI;

namespace MEFrpLauncherX.ViewModels;

public sealed class ManageProxyViewModel : ViewModelBase
{
    public ManageProxyViewModel()
    {
        SelectedProxies = [];
        SwitchViewCommand = new RelayCommand<ViewMode>(mode => CurrentViewMode = mode);
        SelectProxyCommand = new RelayCommand<UserProxyViewModel>(SelectProxy);
        DeselectProxyCommand = new RelayCommand<UserProxyViewModel>(DeselectProxy);
        ToggleSelectProxyCommand = new RelayCommand<UserProxyViewModel>(ToggleSelectProxy);
        ClearSelectionCommand = new RelayCommand(ClearSelection);
    }

    public string SearchText
    {
        get;
        set
        {
            field = value;
            FilterProxies();
        }
    } = string.Empty;

    public bool IsDetailedMode
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            // 当详细模式改变时，更新所有代理的详细状态
            UpdateAllProxiesDetailedStatus(value);
        }
    }


    public ObservableCollection<UserProxyViewModel> FilteredProxies
    {
        get;
    } = [];

    public ObservableCollection<UserProxyViewModel> AllProxies
    {
        get;
        set;
    } = [];

    public ICommand LaunchProxyCommand
    {
        get;
    }

    public ICommand LaunchMultiProxyCommand
    {
        get;
    }

    public ICommand EditProxyCommand
    {
        get;
    }

    public ICommand ForceOfflineProxyCommand
    {
        get;
    }

    public ICommand DisableProxyCommand
    {
        get;
    }

    public ICommand EnableProxyCommand
    {
        get;
    }

    public ICommand DeleteProxyCommand
    {
        get;
    }

    public ICommand GenerateLaunchConfigCommand
    {
        get;
    }

    public ICommand ShowExtraInfoCommand
    {
        get;
    }

    public ViewMode CurrentViewMode
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    } = ViewMode.Grid;

    public ICommand SwitchViewCommand
    {
        get;
    }

    public ICommand SelectProxyCommand
    {
        get;
    }

    public ICommand DeselectProxyCommand
    {
        get;
    }

    public ICommand ToggleSelectProxyCommand
    {
        get;
    }

    public ICommand ClearSelectionCommand
    {
        get;
    }

    public NotifyingCollection<UserProxyViewModel> SelectedProxies
    {
        get;
        set
        {
            if (field != null)
            {
                field.CollectionChangedWithNotification -= OnSelectionChanged;
            }

            field = value;

            if (field != null)
            {
                field.CollectionChangedWithNotification += OnSelectionChanged;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(IsAnyProxySelected));
        }
    }

    public bool IsAnyProxySelected => SelectedProxies?.Count > 1;

    public bool IsDark => ConfigManager.CurrentConfig.Theme.Equals("dark", StringComparison.OrdinalIgnoreCase);

    public bool IsNoData
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public async void FilterProxies()
    {
        MainPageFrameViewModel.Instance.IsLoading = true;
        Core.App.CurrentLogger.LogDebug("开始筛选隧道");
        FilteredProxies.Clear();
        IEnumerable<UserProxyViewModel> filtered = [];
        await Task.Run(() =>
        {
            filtered = AllProxies.Where(proxy =>
                string.IsNullOrEmpty(SearchText) ||
                proxy.proxyName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                PinYinHelper.ConvertToAllSpell(proxy.proxyName)
                    .Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                (SearchText.Replace(" ", string.Empty).StartsWith("/pid:") &&
                 proxy.proxyId.ToString().Contains(SearchText[5..])) ||
                (SearchText.Replace(" ", string.Empty).StartsWith("/n:") &&
                 (proxy.node.Contains(SearchText[3..]) ||
                  PinYinHelper.ConvertToAllSpell(proxy.node)
                      .Contains(SearchText[3..], StringComparison.OrdinalIgnoreCase))) ||
                (SearchText.Replace(" ", string.Empty).StartsWith("/nid:") &&
                 proxy.nodeId.ToString().Contains(SearchText[5..])));
        });
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var proxy in filtered)
            {
                FilteredProxies.Add(proxy);
            }
        });
        IsNoData = FilteredProxies.Count == 0;

        MainPageFrameViewModel.Instance.IsLoading = false;
        Core.App.CurrentLogger.LogDebug("筛选完成，数量: " + FilteredProxies.Count);
    }

    private void UpdateAllProxiesDetailedStatus(bool isDetailed)
    {
        foreach (var proxy in FilteredProxies)
        {
            proxy.Detailed = isDetailed;
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void SelectProxy(UserProxyViewModel proxy)
    {
        if (proxy == null || proxy.IsSelected)
        {
            return;
        }

        proxy.IsSelected = true;
        SelectedProxies.Add(proxy);
    }

    private void DeselectProxy(UserProxyViewModel proxy)
    {
        if (proxy == null || !proxy.IsSelected)
        {
            return;
        }

        proxy.IsSelected = false;
        SelectedProxies.Remove(proxy);
    }

    private void ToggleSelectProxy(UserProxyViewModel proxy)
    {
        if (proxy == null)
        {
            return;
        }

        proxy.IsSelected = !proxy.IsSelected;

        if (proxy.IsSelected)
        {
            if (!SelectedProxies.Contains(proxy))
            {
                SelectedProxies.Add(proxy); // 现在这会自动触发通知
            }
        }
        else
        {
            SelectedProxies.Remove(proxy); // 现在这会自动触发通知
        }
    }

    // 添加清除选择的方法
    public void ClearSelection(object s)
    {
        foreach (var proxy in SelectedProxies.ToList())
        {
            proxy.IsSelected = false;
        }

        SelectedProxies.Clear();
    }

    private void OnSelectionChanged(object sender, EventArgs e) => OnPropertyChanged(nameof(IsAnyProxySelected));
}