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
        init;
    }

    public string Name
    {
        get;
        init;
    }

    /// <summary>节点主机名（迁移对话框探测延迟时使用）</summary>
    public string Hostname
    {
        get;
        init;
    }

    /// <summary>节点服务端口（迁移对话框探测延迟时使用）</summary>
    public int ServicePort
    {
        get;
        init;
    }

    public string Description
    {
        get;
        init;
    }

    public List<string> AllowTypes
    {
        get;
        init;
    }

    public string AllowPorts
    {
        get;
        init;
    }

    public string Bandwidth
    {
        get;
        init;
    }

    public int LoadPercent
    {
        get;
        init
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
        init
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
        init;
    }

    public bool AllowHighTraffic
    {
        get;
        set;
    }

    public string Region
    {
        get;
        init;
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

    /// <summary>
    ///     迁移对话框按需探测得到的节点延迟（毫秒）；null 表示尚未探测或探测失败。
    ///     不影响既有的节点选择逻辑，仅在迁移候选列表中展示/排序。
    /// </summary>
    public long? LatencyMs
    {
        get => field;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LatencyText));
        }
    }

    /// <summary>延迟展示文案（未探测显示占位符）</summary>
    public string LatencyText => LatencyMs is { } ms
        ? string.Format(Languages.Text_UserProxy_MigrateLatencyFormat, ms)
        : Languages.Text_UserProxy_MigrateLatencyUnknown;


    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}