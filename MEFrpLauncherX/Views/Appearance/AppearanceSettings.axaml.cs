using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using MEFrpLauncherX.Core;
using ReactiveUI;

namespace MEFrpLauncherX.Views.Appearance;

public partial class AppearanceSettings : Window
{
    private bool _init;

    public AppearanceSettings()
    {
        InitializeComponent();
        OpacitySlider.Value = ConfigManager.CurrentConfig.BackgroundSettings.LayerOpacity * 100;
        FillModeBox.SelectedIndex = ConfigManager.CurrentConfig.BackgroundSettings.ShouldFillTitleBar ? 0 : 1;
    }

    private void ColorView_OnColorChanged(object? sender, ColorChangedEventArgs e)
    {
        App.FATheme?.CustomAccentColor = e.NewColor;
        ConfigManager.UpdateConfig(cfg => cfg.AccentColor = e.NewColor.ToString());
    }

    private void UpdateOpacity(object? sender, RangeBaseValueChangedEventArgs e)
    {
        var o = e.NewValue / 100;
        ConfigManager.UpdateConfig(cfg => cfg.BackgroundSettings.LayerOpacity = o);
        MainWindow.Instance.MainLayer.Opacity = o;
    }

    private void UpdateBackgroundFillMode(object? sender, SelectionChangedEventArgs e)
    {
        if (!_init || sender == null)
        {
            return;
        }

        var sft = FillModeBox.SelectedIndex == 0;
        ConfigManager.UpdateConfig(cfg => cfg.BackgroundSettings.ShouldFillTitleBar = sft);
        UpdateBackground(sft);
    }

    public static void UpdateBackground(bool sft)
    {
        if (!sft)
        {
            MainWindow.Instance.MainBackground.Show();
            MainWindow.Instance.MainBackground.Source =
                new Bitmap(ConfigManager.CurrentConfig.BackgroundSettings.BackgroundImage);
            MainWindow.Instance.MainBackground.Stretch = ConfigManager.CurrentConfig.BackgroundSettings.Stretch switch
            {
                "None" => Stretch.None,
                "Stretch" => Stretch.Fill,
                "Uniform" => Stretch.Uniform,
                "UniformToFill" => Stretch.UniformToFill,
                _ => Stretch.None
            };
        }
        else
        {
            MainWindow.Instance.MainBackground.Hide();
            MainWindow.Instance.Background =
                new ImageBrush(new Bitmap(ConfigManager.CurrentConfig.BackgroundSettings.BackgroundImage))
                {
                    Stretch = ConfigManager.CurrentConfig.BackgroundSettings.Stretch switch
                    {
                        "None" => Stretch.None,
                        "Stretch" => Stretch.Fill,
                        "Uniform" => Stretch.Uniform,
                        "UniformToFill" => Stretch.UniformToFill,
                        _ => Stretch.None
                    },
                };
            MainWindow.Instance.InvalidateVisual();
        }
    }
}

public class AppearanceSettingsViewModel : ViewModelBase
{
    public AppearanceSettingsViewModel()
    {
        Items =
        [
            new RecentImagesSettingsItem
            {
                Header = "最近的图片",
            },

            new FooterButtonSettingsItem
            {
                Header = "Choose a photo"
            }
        ];
    }

    public List<SettingsItemBase> Items
    {
        get;
    }
}

public class SettingsItemBase : ViewModelBase
{
    public string Header
    {
        get;
        set;
    }
}

public class RecentImagesSettingsItem : SettingsItemBase
{
    public static RecentImagesSettingsItem Instance
    {
        get;
        private set;
    }

    public IImage SelectedImage
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            if (Design.IsDesignMode)
            {
                return;
            }

            if (field == null)
            {
                return;
            }

            var img = Imgs.FirstOrDefault(x => x.Value == field);
            var file = HttpUtility.UrlDecode(img.Key);
            ConfigManager.UpdateConfig(config => config.BackgroundSettings.BackgroundImage = file);
            AppearanceSettings.UpdateBackground(ConfigManager.CurrentConfig.BackgroundSettings.ShouldFillTitleBar);
            Core.App.MainWindow.InvalidateVisual();
        }
    }

    public List<string> ImagePaths
    {
        get;
        private set;
    } = [];

    public Dictionary<string, IImage> Imgs
    {
        get;
        private set;
    } = [];

    public RecentImagesSettingsItem()
    {
        if (File.Exists(Path.Combine(Core.App.StartupPath, "Cache", ".photos")))
        {
            var cnt = File.ReadAllLines(Path.Combine(Core.App.StartupPath, "Cache", ".photos"));
            ImagePaths = cnt.ToList();

            if (ImagePaths.Count <= 1)
            {
                goto FINAL;
            }

            foreach (var image in ImagePaths.ToList())
            {
                if (!File.Exists(image))
                {
                    ImagePaths.Remove(image);
                    continue;
                }

                var img = new Bitmap(image);
                Images.Add(img);
                Imgs.Add(image, img);
            }

            if (ConfigManager.CurrentConfig.BackgroundSettings.BackgroundImage.IsNullOrEmpty())
            {
                goto FINAL;
            }

            SelectedImage = Imgs[ConfigManager.CurrentConfig.BackgroundSettings.BackgroundImage];
        }

        FINAL:
        Instance = this;
    }

    public AvaloniaList<IImage> Images
    {
        get;
    } = [];
}

public class FooterButtonSettingsItem : SettingsItemBase
{
    public FooterButtonSettingsItem()
    {
        Footer = "选择";
    }

    public object Footer
    {
        get;
        set;
    }

    public void SelectFile()
    {
        SelectBackgroundImpl();
    }

    public void ClearFile()
    {
        File.WriteAllText(Path.Combine(Core.App.StartupPath, "Cache", ".photos"), string.Empty);
        ConfigManager.UpdateConfig(config => config.BackgroundSettings.BackgroundImage = string.Empty);
        Core.App.MainWindow?.Background = null;
        MainWindow.Instance.MainLayer.Background = null;
        RecentImagesSettingsItem.Instance?.Images?.Clear();
        RecentImagesSettingsItem.Instance?.Imgs?.Clear();
    }

    public static async void SelectBackgroundImpl()
    {
        var files = await Core.App.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "请选择一个背景",
            SuggestedStartLocation =
                await Core.App.StorageProvider.TryGetWellKnownFolderAsync(WellKnownFolder.Pictures),
            SuggestedFileName = null,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("图片文件") { Patterns = ["*.jpg", "*.jpeg", "*.png", "*.bmp", "*.gif", "*.tiff"] }
            ]
        });
        if (files.Count > 0)
        {
            var file = HttpUtility.UrlDecode(files[0].Path.AbsolutePath);
            if (RecentImagesSettingsItem.Instance.ImagePaths.Any(x =>
                    !x.Equals(file, StringComparison.OrdinalIgnoreCase)) ||
                RecentImagesSettingsItem.Instance.ImagePaths.Count == 0)
            {
                await File.AppendAllLinesAsync(Path.Combine(Core.App.StartupPath, "Cache", ".photos"), [file]);
            }

            await ConfigManager.UpdateConfigAsync(config => config.BackgroundSettings.BackgroundImage = file);
            var img = new Bitmap(file);
            RecentImagesSettingsItem.Instance?.Images?.Add(img);
            RecentImagesSettingsItem.Instance?.Imgs?.Add(file, img);
            RecentImagesSettingsItem.Instance?.ImagePaths?.Add(file);
            RecentImagesSettingsItem.Instance?.SelectedImage = img;
            
            AppearanceSettings.UpdateBackground(ConfigManager.CurrentConfig.BackgroundSettings.ShouldFillTitleBar);
        }

        Core.App.MainWindow.InvalidateVisual();
    }
}