using Avalonia.Controls;
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
}