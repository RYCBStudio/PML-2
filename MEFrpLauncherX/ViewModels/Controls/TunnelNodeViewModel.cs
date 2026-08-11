using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MEFrpLauncherX.Core.Languages;

namespace MEFrpLauncherX.ViewModels.Controls;

public class TunnelNodeViewModel : INotifyPropertyChanged
{
    private bool? _cachedIsNotOverloaded;

    // 添加缓存字段
    private bool? _cachedIsOverloaded;

    public int NodeId
    {
        get;
        set;
    }

    public string Name
    {
        get;
        set;
    }

    public string Description
    {
        get;
        set;
    }

    public List<string> AllowTypes
    {
        get;
        set;
    }

    public string AllowPorts
    {
        get;
        set;
    }

    public string Bandwidth
    {
        get;
        set;
    }

    public int LoadPercent
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                // 清除缓存
                _cachedIsOverloaded = null;
                _cachedIsNotOverloaded = null;
                OnPropertyChanged();
            }
        }
    }

    public bool IsOnline
    {
        get => field;
        set
        {
            if (field != value)
            {
                field = value;
                // 可能影响显示状态，清除相关缓存
                OnPropertyChanged();
            }
        }
    }

    public bool CanBuildSite
    {
        get;
        set;
    }

    public bool AllowHighTraffic
    {
        get;
        set;
    }

    public string Region
    {
        get;
        set;
    }

    public bool IsSelected
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    public string[] AllowGroup
    {
        get;
        set;
    }

    public string RegionDisplay => Region switch
    {
        "cn" => Languages.Text_Nodes_RegionCN,
        "cnos" => Languages.Text_Nodes_RegionHKMT,
        "oversea" => Languages.Text_Nodes_RegionOversea,
        _ => Languages.Text_Nodes_RegionUnknown
    };

    public bool IsOverloaded => _cachedIsOverloaded ??= LoadPercent >= 85;
    public bool IsNotOverloaded => _cachedIsNotOverloaded ??= !IsOverloaded;


    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}