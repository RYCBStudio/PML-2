using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using Avalonia.Styling;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Models;
using MEFrpLauncherX.ViewModels.Debug;

namespace MEFrpLauncherX.Controls;

public partial class TunnelNodeControl : UserControl
{
    public static readonly BoolToColorConverter BoolToColorConverter = new();
    public static readonly BoolToStatusConverter BoolToStatusConverter = new();
    public static readonly BoolToBorderThicknessConverter BoolToBorderThicknessConverter = new();
    public static readonly SelectedToBorderBrushConverter SelectedToBorderBrushConverter = new();
    public static readonly LoadPercentColorConverter LoadPercentColorConverter = new();

    public TunnelNodeControl()
    {
        InitializeComponent();
        if (Design.IsDesignMode)
        {
            DataContext = new Debug_TunnelNodeViewModel();
        }
    }


    private async void PerformAnimation(object? sender, VisualTreeAttachmentEventArgs e)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(100));
        var pg = sender as ProgressBar;
        if (pg == null)
        {
            return;
        }

        // 更精细的控制
        var animation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(800),
            IterationCount = new IterationCount(1),
            PlaybackDirection = PlaybackDirection.Normal,
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame
                {
                    Setters = { new Setter(RangeBase.ValueProperty, 0d) },
                    Cue = new Cue(0d)
                },
                new KeyFrame
                {
                    Setters = { new Setter(RangeBase.ValueProperty, pg.Value * 0.3) },
                    Cue = new Cue(0.3d)
                },
                new KeyFrame
                {
                    Setters = { new Setter(RangeBase.ValueProperty, pg.Value) },
                    Cue = new Cue(1d)
                }
            },
            Easing = Easing.Parse("CubicEaseIn")
        };

        await animation.RunAsync(pg);
    }
}

public class BoolToBorderThicknessConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (bool)value ? 2 : -1;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class TunnelNodeViewModel : ProxyBase, INotifyPropertyChanged
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
        "cn" => "中国大陆",
        "cnos" => "港澳台",
        "oversea" => "海外",
        _ => "未知"
    };

    public bool IsOverloaded => _cachedIsOverloaded ??= LoadPercent >= 85;
    public bool IsNotOverloaded => _cachedIsNotOverloaded ??= !IsOverloaded;


    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public class AllowGroupToIsVisibleConverter : IValueConverter
{
    public static AllowGroupToIsVisibleConverter Instance
    {
        get;
    } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is IEnumerable<string> e && !e.Contains("default");

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public class AllowGroupToBorderBrushConverter : IValueConverter
{
    public static AllowGroupToBorderBrushConverter Instance
    {
        get;
    } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is IEnumerable<string> e && !e.Contains("default")
            ? App.Current.TryGetResource("SystemFillColorCautionBrush", App.Current.ActualThemeVariant,
                out var o)
                ? o
                : null
            : null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}