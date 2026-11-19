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
        "Guten Tag", "Ciao", "Olá", "Привет", "مرحبا", "สวัสดี", "Hej",
        "Zdravo", "Ahoj", "Xin chào", "Merhaba", "Здравствуйте", "Witaj",
        "Dzień dobry", "Dobrý den", "Доброе утро", "Здравствуйте", "Утро доброе", "Доброе вечер", "Добрый вечер",
        "Bună", "Dobră", "Cześć", "Bok", "Привіт", "Сайн байна уу"
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
    private bool _finishRequested;

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
            if (!_initialized || _finishRequested)
            {
                return;
            }

            // 收尾页尚未确认就关窗（例如任务管理器强杀、Alt+F4），也标记完成，避免重复打扰。
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
        FinishPanel.IsVisible = _step == 3;
        BackButton.IsEnabled = _step > 0;
        NextButton.Content = _step == 3 ? Languages.Text_Welcome_Start : Languages.Text_Welcome_Next;
        StepDescription.Text = _step switch
        {
            0 => Languages.Text_Welcome_LanguageStep,
            1 => Languages.Text_Welcome_ThemeStep,
            2 => Languages.Text_Welcome_AccentStep,
            _ => Languages.Text_Welcome_FinishStep
        };
        StepIndicator.Text = string.Format(Languages.Text_Welcome_StepFormat, _step + 1, 4);
        if (_step == 3)
        {
            UpdateFinishSummary();
        }
    }

    private void UpdateFinishSummary()
    {
        FinishLanguage.Text = ConfigManager.CurrentConfig.Language switch
        {
            "en-US" => "English (United States)",
            "zh-Hant" => "繁體中文（台灣）",
            _ => "简体中文（中国）"
        };
        FinishTheme.Text = ConfigManager.CurrentConfig.Theme switch
        {
            "Light" => Languages.Text_Settings_Theme_Light,
            "Dark" => Languages.Text_Settings_Theme_Dark,
            _ => Languages.Text_Settings_Theme_System
        };
        FinishAccentPreview.IsVisible = Color.TryParse(ConfigManager.CurrentConfig.AccentColor, out var color);
        if (FinishAccentPreview.IsVisible)
        {
            FinishAccentPreview.Fill = new SolidColorBrush(color);
        }

        FinishAccent.Text = FinishAccentPreview.IsVisible
            ? ConfigManager.CurrentConfig.AccentColor
            : Languages.Text_Welcome_UseSystemAccent;
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
        if (_step < 3)
        {
            _step++;
            UpdateStep();
            return;
        }

        Close();
    }

    private void SkipClicked(object? sender, RoutedEventArgs e)
    {
        _finishRequested = true;
        _step = 3;
        UpdateStep();
    }

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
