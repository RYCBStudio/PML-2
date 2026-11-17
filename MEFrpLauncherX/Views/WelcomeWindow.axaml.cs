using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.ViewModels;

namespace MEFrpLauncherX.Views;

public partial class WelcomeWindow : Window
{
    private static readonly string[] Greetings =
    [
        "你好", "Hello", "Bonjour", "Hola", "こんにちは", "안녕하세요",
        "Guten Tag", "Ciao", "Olá", "Привет", "مرحبا", "สวัสดี"
    ];

    private static readonly string[] AccentColors =
    [
        "#FF0078D4", "#FF8764B8", "#FFE3008C", "#FFE81123",
        "#FFFF8C00", "#FFFFB900", "#FF107C10", "#FF008272",
        "#FF00B7C3", "#FF486991", "#FF767676"
    ];

    private readonly DispatcherTimer _greetingTimer = new() { Interval = TimeSpan.FromSeconds(2.5) };
    private int _step;
    private bool _initialized;

    public WelcomeWindow()
    {
        InitializeComponent();
        GreetingCarousel.ItemsSource = Greetings;
        AccentColorList.ItemsSource = AccentColors;
        LanguageBox.SelectedIndex = ConfigManager.CurrentConfig.Language switch
        {
            "en-US" => 2,
            "zh-Hant" => 1,
            _ => 0
        };
        ThemeBox.SelectedIndex = ConfigManager.CurrentConfig.Theme switch
        {
            "Light" => 1,
            "Dark" => 2,
            _ => 0
        };
        SystemAccentCheckBox.IsChecked = string.IsNullOrWhiteSpace(ConfigManager.CurrentConfig.AccentColor);
        _initialized = true;
        UpdateStep();

        _greetingTimer.Tick += (_, _) =>
            GreetingCarousel.SelectedIndex = (GreetingCarousel.SelectedIndex + 1) % Greetings.Length;
        Opened += (_, _) => _greetingTimer.Start();
        Closing += (_, _) =>
        {
            _greetingTimer.Stop();
            if (!_initialized)
            {
                return;
            }

            // 直接关窗也视为跳过，避免每次启动重复弹出。
            ConfigManager.UpdateConfig(config => config.WelcomeCompleted = true);
        };
    }

    public static async System.Threading.Tasks.Task ShowIfNeededAsync(Window owner, bool force = false)
    {
        if (!force && ConfigManager.CurrentConfig.WelcomeCompleted)
        {
            return;
        }

        await new WelcomeWindow().ShowDialog(owner);
    }

    private void UpdateStep()
    {
        LanguagePanel.IsVisible = _step == 0;
        ThemePanel.IsVisible = _step == 1;
        AccentPanel.IsVisible = _step == 2;
        BackButton.IsEnabled = _step > 0;
        NextButton.Content = _step == 2 ? Languages.Text_Welcome_Finish : Languages.Text_Welcome_Next;
        StepDescription.Text = _step switch
        {
            0 => Languages.Text_Welcome_LanguageStep,
            1 => Languages.Text_Welcome_ThemeStep,
            _ => Languages.Text_Welcome_AccentStep
        };
        StepIndicator.Text = string.Format(Languages.Text_Welcome_StepFormat, _step + 1, 3);
    }

    private void BackClicked(object? sender, RoutedEventArgs e)
    {
        if (_step > 0)
        {
            _step--;
            UpdateStep();
        }
    }

    private void NextClicked(object? sender, RoutedEventArgs e)
    {
        if (_step < 2)
        {
            _step++;
            UpdateStep();
            return;
        }

        Close();
    }

    private void SkipClicked(object? sender, RoutedEventArgs e) => Close();

    private void LanguageChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || LanguageBox.SelectedItem is not ComboBoxItem { Tag: string language })
        {
            return;
        }

        ConfigManager.UpdateConfig(config => config.Language = language);
        if (MainPageFrameViewModel.Instance is { } frame)
        {
            frame.NeedRestart = true;
        }
    }

    private void ThemeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || ThemeBox.SelectedItem is not ComboBoxItem { Tag: string theme })
        {
            return;
        }

        ConfigManager.UpdateConfig(config => config.Theme = theme);
        Application.Current!.RequestedThemeVariant = theme switch
        {
            "Dark" => ThemeVariant.Dark,
            "Light" => ThemeVariant.Light,
            _ => ThemeVariant.Default
        };
    }

    private void AccentColorClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string value } || !Color.TryParse(value, out var color))
        {
            return;
        }

        SystemAccentCheckBox.IsChecked = false;
        App.FATheme?.PreferUserAccentColor = false;
        App.FATheme?.CustomAccentColor = color;
        ConfigManager.UpdateConfig(config => config.AccentColor = color.ToString());
    }

    private void SystemAccentChanged(object? sender, RoutedEventArgs e)
    {
        if (!_initialized || SystemAccentCheckBox.IsChecked != true)
        {
            return;
        }

        App.FATheme?.CustomAccentColor = null;
        App.FATheme?.PreferUserAccentColor = true;
        ConfigManager.UpdateConfig(config => config.AccentColor = string.Empty);
    }
}
