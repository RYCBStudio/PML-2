using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using MEFrpLauncherX.Controls;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.MEFIntergrated;
using MEFrpLauncherX.Views;

namespace MEFrpLauncherX.ViewModels;

public class NodesContainerViewModel : INotifyPropertyChanged
{
    public bool IsLoading
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<TunnelNodeViewModel> AllNodes
    {
        get;
    } = [];

    public ObservableCollection<TunnelNodeViewModel> FilteredNodes
    {
        get;
    } = [];

    public Dictionary<string, string> RegionOptions
    {
        get;
    } = new()
    {
        { "all", "全部地区" },
        { "cn", "中国大陆" },
        { "cnos", "港澳台" },
        { "oversea", "海外" }
    };

    public object SelectedRegion
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged();
                FilterNodes();
            }
        }
    } = "all";

    public string SearchText
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
            FilterNodes();
        }
    }

    public bool FilterCanBuildSite
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
            FilterNodes();
        }
    }

    public bool FilterAllowHighTraffic
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
            FilterNodes();
        }
    }

    public bool FilterNotOverLoaded
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
            FilterNodes();
        }
    }

    public async Task LoadNodesAsync(InfoClasses.NodesListInfo listInfo, InfoClasses.NodesStatusInfo statusInfo)
    {
        try
        {
            IsLoading = true;
            AllNodes.Clear();

            await Task.Run(async () =>
            {
                var s = statusInfo.NodesStatus;
                if (s is null || s.Length < 1)
                {
                    s = (await MEFApiConverter.GetNodesStatusAsync()).data;
                }

                foreach (var node in listInfo.NodesList)
                {
                    var status = s?.FirstOrDefault(s => s.nodeId == node.nodeId);
                    var vm = new TunnelNodeViewModel
                    {
                        NodeId = node.nodeId,
                        Name = node.name,
                        Description = node.description,
                        AllowTypes = node.allowType?.Split(';')
                            ?.Select(type => type.ToUpper())
                            ?.ToArray() ?? [],
                        Bandwidth = node.bandwidth,
                        LoadPercent = status?.loadPercent ?? 0,
                        IsOnline = status?.isOnline ?? false,
                        AllowPorts = node.allowPort,
                        CanBuildSite = (node.allowType?.Split(';')
                            ?.Select(type => type.ToUpper())
                            ?.ToArray() ?? []).Any(s =>
                            s.Equals("http", StringComparison.OrdinalIgnoreCase) ||
                            s.Equals("https", StringComparison.OrdinalIgnoreCase)),
                        Region = node.region,
                        AllowGroup = node.allowGroup.Split(';')
                    };
                    vm.AllowHighTraffic = new Func<bool>(() =>
                    {
                        if (string.IsNullOrEmpty(vm.Bandwidth))
                        {
                            return false;
                        }

                        var bandwidth = vm.Bandwidth.ToLower();

                        if (bandwidth.Contains("gbps"))
                        {
                            if (double.TryParse(bandwidth.Replace("gbps", ""), out var gbpsValue))
                            {
                                return gbpsValue >= 0.07;
                            }
                        }
                        else if (bandwidth.Contains("mbps"))
                        {
                            if (double.TryParse(bandwidth.Replace("mbps", ""), out var mbpsValue))
                            {
                                return mbpsValue >= 70;
                            }
                        }

                        return false;
                    }).Invoke();
// Debug
//                     Core.App.CurrentLogger.LogDebug("\n==========\nAdding node: Info: \n" +
//                                                     $"""
//                                                      NodeId: {vm.NodeId}
//                                                      Name: {vm.Name}
//                                                      Description: {vm.Description}
//                                                      AllowTypes: {string.Join(", ", vm.AllowTypes)}
//                                                      Bandwidth: {vm.Bandwidth}
//                                                      LoadPercent: {vm.LoadPercent}
//                                                      IsOnline: {vm.IsOnline}
//                                                      AllowPorts: {vm.AllowPorts}
//                                                      CanBuildSite: {vm.CanBuildSite}
//                                                      AllowHighTraffic: {vm.AllowHighTraffic}
//                                                      Region: {vm.Region}
//                                                      ===========
//                                                      """);
                    AllNodes.Add(vm);
                }
            });

            FilterNodes();
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger.Log($"加载节点时出错: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public event Action<TunnelNodeViewModel> NodeSelected;

    public TunnelNodeViewModel SelectedNode
    {
        get;
        set
        {
            if (field != null)
            {
                field.IsSelected = false;
            }

            field = value;


            if (field is { IsOverloaded: true })
            {
                field.IsSelected = false;
                var tmplsi = (NodesContainer.Instance.Nodes.ContainerFromItem(field) as
                    ListBoxItem)!;
                if (tmplsi != null)
                {
                    tmplsi.IsEnabled = false;
                }

                NodesContainer.Instance.Nodes.SelectedIndex = -1;
                return;
            }

            if (field != null)
            {
                field.IsSelected = true;
            }

            OnPropertyChanged();
            NodeSelected?.Invoke(field);
        }
    }

    private void FilterNodes()
    {
        IsLoading = true;
        FilteredNodes.Clear();

        var realRegion = "all";
        var filtered = AllNodes.Where(node =>
            (string.IsNullOrEmpty(SearchText) || SearchText.StartsWith("/d:") &&
             node.Description.Contains(SearchText.Remove(0, 3),
                 StringComparison.OrdinalIgnoreCase) ||
             (SearchText.StartsWith("/pd:") && PinYinHelper
                 .ConvertToAllSpell(node.Description).Contains(
                     PinYinHelper.ConvertToAllSpell(SearchText.Remove(0, 4)),
                     StringComparison.OrdinalIgnoreCase)) ||
             node.Name.Contains(SearchText,
                 StringComparison.OrdinalIgnoreCase) ||
             (SearchText.StartsWith("/pn:") && PinYinHelper
                 .ConvertToAllSpell(node.Name).Contains(
                     PinYinHelper.ConvertToAllSpell(SearchText.Remove(0, 4)),
                     StringComparison.OrdinalIgnoreCase)) ||
             node.NodeId.ToString().Contains(SearchText,
                 StringComparison.OrdinalIgnoreCase)) &&
            IsRegionMeets(node, out realRegion) &&
            (!FilterCanBuildSite || node.CanBuildSite) &&
            (!FilterAllowHighTraffic || node.AllowHighTraffic) &&
            (!FilterNotOverLoaded || node.IsNotOverloaded));
//         Core.App.CurrentLogger.LogDebug("Requirements: \n" +
//                                         $"""
//                                          SearchText: {SearchText},
//                                          CanBuildSite: {FilterCanBuildSite},
//                                          AllowHighTraffic: {FilterAllowHighTraffic},
//                                          Region: {realRegion}
//                                          """);
        foreach (var node in filtered)
        {
            // Debug
//             Core.App.CurrentLogger.LogDebug("\n=========\nNode #" + node.NodeId + ": " + node.Name +
//                                             ", meets requirements: \n" +
//                                             $"""
//                                              CanBuildSite: {node.CanBuildSite}(re: {FilterCanBuildSite}),
//                                              AllowHighTraffic: {node.AllowHighTraffic}(re: {FilterAllowHighTraffic}),
//                                              Region: {node.Region}(re: {realRegion})
//                                              ==========
//                                              """);
            FilteredNodes.Add(node);
        }

        Core.App.CurrentLogger.Log($"{FilteredNodes.Count} nodes added.", EnumLogType.Debug);

        OnPropertyChanged(nameof(FilteredNodes));
        // }, DispatcherPriority.Background);
        IsLoading = false;
        MainPageFrameViewModel.Instance.IsLoading = false;
    }

    private bool IsRegionMeets(TunnelNodeViewModel vm, out string region)
    {
        var _tmpRegion = "all";
        var _ = SelectedRegion.GetType();
        if (SelectedRegion is TabStripItem item)
        {
            _tmpRegion = item.Tag.ToString();
        }
        else
        {
            _tmpRegion = SelectedRegion.ToString();
        }

        region = _tmpRegion;
        return _tmpRegion == "all" || vm.Region == _tmpRegion;
    }
}