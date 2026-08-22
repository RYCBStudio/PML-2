using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using MEFrpLauncherX.Core;

namespace MEFrpLauncherX.Views.ProxyMonitor;

public partial class ProxyFloatSettings : Window
{
    private bool _init;

    public ProxyFloatSettings()
    {
        InitializeComponent();
        Loaded += (sender, args) =>
        {
            PosBox.SelectedIndex = ConfigManager.CurrentConfig.PMSettings.Position switch
            {
                "lt" => 0,
                "rt" => 1,
                "lb" => 2,
                "rb" => 3,
                "ct" => 4,
                "cb" => 5,
                _ => 0
            };
            ClickThroughSwitch.IsChecked = ConfigManager.CurrentConfig.PMSettings.ClickThrough;
            ShowChartSwitch.IsChecked = ConfigManager.CurrentConfig.PMSettings.ShowChart;
            OpacitySlider.Value = ConfigManager.CurrentConfig.PMSettings.Opacity;
            _init = true;
        };
    }

    private void SavePosition(object? sender, SelectionChangedEventArgs e)
    {
        if (!_init)
        {
            return;
        }

        if (sender is not ComboBox comboBox)
        {
            return;
        }

        if (comboBox.SelectedItem is not ComboBoxItem comboBoxItem)
        {
            return;
        }

        var tag = comboBoxItem.Tag?.ToString();
        if (tag is null)
        {
            return;
        }

        ConfigManager.UpdateConfig(config =>
        {
            config.PMSettings.Position = tag;
        });
    }

    private void ClickThroughChanged(object? sender, RoutedEventArgs e)
    {
        if (!_init)
        {
            return;
        }

        ConfigManager.UpdateConfig(config =>
        {
            config.PMSettings.ClickThrough = (sender as ToggleSwitch)?.IsChecked ?? false;
        });
        ProxyFloat.Instance?.ApplySettings();
    }

    private void ShowChartChanged(object? sender, RoutedEventArgs e)
    {
        if (!_init)
        {
            return;
        }

        ConfigManager.UpdateConfig(config =>
        {
            config.PMSettings.ShowChart = (sender as ToggleSwitch)?.IsChecked ?? true;
        });
        ProxyFloat.Instance?.ApplySettings();
    }

    private void OpacityChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_init)
        {
            return;
        }

        var value = Math.Clamp(Math.Round(e.NewValue, 2), 0.5, 1.0);
        ConfigManager.UpdateConfig(config =>
        {
            config.PMSettings.Opacity = value;
        });
        ProxyFloat.Instance?.ApplySettings();
    }
}
