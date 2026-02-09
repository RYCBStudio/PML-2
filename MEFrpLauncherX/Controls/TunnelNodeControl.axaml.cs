using System;
using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using FluentAvalonia.Core;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Models;
using MEFrpLauncherX.ViewModels.Debug;

namespace MEFrpLauncherX.Controls
{
    public partial class TunnelNodeControl : UserControl
    {
        public TunnelNodeControl()
        {
            InitializeComponent();
            if (Design.IsDesignMode)
            {
                DataContext = new Debug_TunnelNodeViewModel();
            }
        }

        public static readonly BoolToColorConverter BoolToColorConverter = new();
        public static readonly BoolToStatusConverter BoolToStatusConverter = new();
        public static readonly BoolToBorderThicknessConverter BoolToBorderThicknessConverter = new();
        public static readonly SelectedToBorderBrushConverter SelectedToBorderBrushConverter = new();
        public static readonly LoadPercentColorConverter LoadPercentColorConverter = new();
    }

    public class BoolToBorderThicknessConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return (bool)value ? 2 : -1;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

    public class TunnelNodeViewModel : ProxyBase, INotifyPropertyChanged
    {
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

        public string[] AllowTypes
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
            set;
        }

        public bool IsOnline
        {
            get;
            set;
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

        public bool IsOverloaded => LoadPercent >= 85;
        public bool IsSemiOverloaded => LoadPercent > 60;
        public bool IsNotOverloaded => !IsOverloaded;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class AllowGroupToIsVisibleConverter : IValueConverter
    {
        public static AllowGroupToIsVisibleConverter Instance
        {
            get;
        } = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is IEnumerable e && !e.Contains("default");
        }

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
            return value is IEnumerable e && !e.Contains("default")
                ? App.Current.TryGetResource("SystemFillColorCautionBackgroundBrush", App.Current.ActualThemeVariant,
                    out var o)
                    ? o
                    : null
                : null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}