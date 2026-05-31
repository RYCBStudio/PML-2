using Avalonia.Controls;
using MEFrpLauncherX.Core.Controls;
using MEFrpLauncherX.Core.Styling;
using MEFrpLauncherX.Views;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.Json;
using System.Web;
using Avalonia.Controls.Converters;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using MEFrpLauncherX.Core;
using ReactiveUI;

namespace MEFrpLauncherX.ViewModels;

public class ThemeEditorViewModel : ViewModelBase, INotifyPropertyChanged
{
    // 主题基本信息
    public string Name
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "新主题";

    public string Author
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "未知作者";

    public string Description
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "";

    public string Version
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "1.0.0";

    public Bitmap PreviewImage
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }
    
    public string PreviewImagePath
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    // 背景设置
    public string BackgroundType
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            // this.RaisePropertyChanged(nameof(IsSolidColorBackground));
            // this.RaisePropertyChanged(nameof(IsImageBackground));
        }
    } = "SolidColor";

    public bool IsSolidColorBackground
    {
        get;
        set
        {
            BackgroundType = value ? "SolidColor" : "Image";
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(FillModes));
            this.RaisePropertyChanged(nameof(BackgroundFillMode));
        }
    }

    public bool IsImageBackground
    {
        get;
        set
        {
            BackgroundType = value ? "Image" : "SolidColor";
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(FillModes));
            this.RaisePropertyChanged(nameof(BackgroundFillMode));
        }
    }

    private bool _isUpdating;
    private Color? _latestAvailableColor;

    [ValidHexStringValidator(ErrorMessage = "请输入有效的十六进制颜色值，例如: #FF0000 或 #FFFF0000")]
    public string BackgroundColor
    {
        get;
        set
        {
            if (_isUpdating)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref field, value);
            _isUpdating = true;
            RealBackgroundColor = ColorToHexConverter.ParseHexString(BackgroundColor, AlphaComponentPosition.Leading) ??
                                  _latestAvailableColor;
            _latestAvailableColor = RealBackgroundColor;
            _isUpdating = false;
        }
    } = "#FF1A1A1A";

    public Color? RealBackgroundColor
    {
        get => field ?? ColorToHexConverter.ParseHexString(BackgroundColor, AlphaComponentPosition.Leading);
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            //_isUpdating = true;
            BackgroundColor = ColorToHexConverter.ToHexString(value ?? Color.Parse("#00000000"),
                AlphaComponentPosition.Leading, true, true);
            _latestAvailableColor = value;
            //_isUpdating = false;
        }
    }

    public string BackgroundImagePath
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(BackgroundImagePreview));
        }
    } = "";

    public Bitmap? BackgroundImagePreview
    {
        get
        {
            if (!string.IsNullOrEmpty(BackgroundImagePath) && File.Exists(BackgroundImagePath))
            {
                try
                {
                    return new Bitmap(BackgroundImagePath);
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }
    }

    public string BackgroundFillMode
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "Uniform";

    public ObservableCollection<string> FillModes => IsImageBackground
        ? ["None", "Stretch", "Uniform", "UniformToFill"]
        : ["None", "Radiation", "Gradient"];

    public double LayerOpacity
    {
        get;
        set
        {
            field = Math.Clamp(value, 0, 1);
            this.RaiseAndSetIfChanged(ref field, value);
        }
    } = 1.0;

    // 强调色动画
    public ObservableCollection<AccentColorItem> AccentColors
    {
        get;
    } = [];

    public AccentColorItem SelectedAccentColor
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(CanEdit));
            this.RaisePropertyChanged(nameof(CanMoveUp));
            this.RaisePropertyChanged(nameof(CanMoveDown));
        }
    }

    public bool CanEdit => SelectedAccentColor is not null;

    public bool CanMoveUp => SelectedAccentColor is not null && AccentColors.IndexOf(SelectedAccentColor) > 0;

    public bool CanMoveDown => SelectedAccentColor is not null &&
                               AccentColors.IndexOf(SelectedAccentColor) < AccentColors.Count - 1;

    // 预设颜色
    public ObservableCollection<string> PresetColors
    {
        get;
    } =
    [
        "#FF0000", "#FF7F00", "#FFFF00", "#00FF00", "#0000FF", "#4B0082", "#9400D3",
        "#FF3333", "#FF9933", "#FFFF33", "#33FF33", "#33FFFF", "#3333FF", "#FF33FF"
    ];

    // 命令
    public ReactiveCommand<Unit, Unit> AddColorCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> RemoveColorCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> MoveUpCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> MoveDownCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> SaveThemeCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> PreviewThemeCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> BrowseBackgroundCommand
    {
        get;
    }

    public ReactiveCommand<Unit, Unit> BrowsePreviewImageCommand
    {
        get;
    }

    public ReactiveCommand<string, Unit> SetColorCommand
    {
        get;
    }

    private readonly string? _editFilePath;

    public ThemeEditorViewModel() : this(null)
    {
        if (Design.IsDesignMode)
        {
            LoadTheme(@".\Config\Themes\彩虹呼吸灯 (带亮度呼吸)\index.json");
        }
    }

    public ThemeEditorViewModel(string? themeFilePath)
    {
        _editFilePath = themeFilePath;

        // 初始化默认颜色
        AddDefaultColors();

        // 初始化命令
        AddColorCommand = ReactiveCommand.Create(AddColor);
        RemoveColorCommand = ReactiveCommand.Create(RemoveColor,
            this.WhenAnyValue(x => x.SelectedAccentColor).Select(x => x != null));
        MoveUpCommand = ReactiveCommand.Create(MoveUp, this.WhenAnyValue(x => x.CanMoveUp));
        MoveDownCommand = ReactiveCommand.Create(MoveDown, this.WhenAnyValue(x => x.CanMoveDown));
        SaveThemeCommand = ReactiveCommand.Create(SaveTheme);
        PreviewThemeCommand = ReactiveCommand.Create(PreviewTheme);
        BrowseBackgroundCommand = ReactiveCommand.Create(BrowseBackground);
        SetColorCommand = ReactiveCommand.Create<string>(SetColor);
        BrowsePreviewImageCommand = ReactiveCommand.Create(BrowsePreviewImage);

        // 如果是编辑现有主题，加载数据
        if (!string.IsNullOrEmpty(themeFilePath) && File.Exists(Path.Combine(themeFilePath, "index.json")))
        {
            LoadTheme(Path.Combine(themeFilePath, "index.json"));
        }
    }

    private async void BrowsePreviewImage()
    {
        var res = await MainWindow.Instance.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择预览图片",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("图片文件")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif"]
                }
            ]
        });
        if (res.Count > 0)
        {
            try
            {
                PreviewImagePath = res[0].Path.IsFile ? res[0].Path.AbsolutePath : res[0].Path.AbsoluteUri;
                PreviewImage = new Bitmap(PreviewImagePath);
            }
            catch (Exception ex)
            {
                Growl.Error($"加载预览图片失败: {ex.Message}");
            }
        }
    }

    private void SetColor(string color)
    {
        SelectedAccentColor.Color = color;
    }

    private void AddDefaultColors()
    {
        var defaultColors = new[]
        {
            new AccentColorItem { Color = "#FF0000", Duration = 1.0 },
            new AccentColorItem { Color = "#FFFF00", Duration = 1.0 },
            new AccentColorItem { Color = "#00FF00", Duration = 1.0 },
            new AccentColorItem { Color = "#00FFFF", Duration = 1.0 },
            new AccentColorItem { Color = "#0000FF", Duration = 1.0 },
            new AccentColorItem { Color = "#FF00FF", Duration = 1.0 }
        };

        foreach (var color in defaultColors)
        {
            AccentColors.Add(color);
        }
    }

    private void AddColor()
    {
        var newColor = new AccentColorItem { Color = "#FF0000", Duration = 1.0 };
        AccentColors.Add(newColor);
        SelectedAccentColor = newColor;
    }

    private void RemoveColor()
    {
        if (SelectedAccentColor != null)
        {
            var index = AccentColors.IndexOf(SelectedAccentColor);
            AccentColors.Remove(SelectedAccentColor);

            if (AccentColors.Count > 0)
            {
                SelectedAccentColor = index < AccentColors.Count ? AccentColors[index] : AccentColors.Last();
            }
        }
    }

    private void MoveUp()
    {
        if (SelectedAccentColor != null)
        {
            var index = AccentColors.IndexOf(SelectedAccentColor);
            if (index > 0)
            {
                AccentColors.Move(index, index - 1);
                this.RaisePropertyChanged(nameof(CanMoveUp));
                this.RaisePropertyChanged(nameof(CanMoveDown));
                SelectedAccentColor = AccentColors[index - 1];
            }
        }
    }

    private void MoveDown()
    {
        if (SelectedAccentColor != null)
        {
            var index = AccentColors.IndexOf(SelectedAccentColor);
            if (index < AccentColors.Count - 1)
            {
                AccentColors.Move(index, index + 1);
                this.RaisePropertyChanged(nameof(CanMoveUp));
                this.RaisePropertyChanged(nameof(CanMoveDown));
                SelectedAccentColor = AccentColors[index + 1];
            }
        }
    }

    private void SaveTheme()
    {
        try
        {
            var manifest = new ThemeManifest
            {
                Name = Name,
                Author = Author,
                Description = Description,
                Version = Version,
                Background = new BackgroundMeta
                {
                    Type = BackgroundType,
                    Color = BackgroundType == "SolidColor" ? BackgroundColor : null,
                    Image = BackgroundType == "Image" ? BackgroundImagePath : null,
                    FillMode = BackgroundFillMode,
                    LayerOpacity = LayerOpacity
                },
                AccentColor = AccentColors.Select(c => new AccentMeta
                {
                    Color = c.Color,
                    Duration = c.Duration
                }).ToList(),
                FontFamily = null,
                PreviewImage = null
            };

            // 确定保存路径
            string savePath;
            if (!string.IsNullOrEmpty(_editFilePath) &&
                Path.GetExtension(_editFilePath).Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                savePath = _editFilePath;
            }
            else
            {
                var themesDir = Path.Combine(Core.App.StartupPath, "Config", "Themes", SanitizeFileName(Name));
                Directory.CreateDirectory(themesDir);
                savePath = Path.Combine(themesDir, "index.json");
            }
            
            manifest.PreviewImage = PreviewImage != null ? Path.GetRelativePath(Path.GetDirectoryName(savePath), PreviewImagePath) : null;

            var json = JsonSerializer.Serialize(manifest, App.AppJsonSerializerContext.ThemeManifest);
            File.WriteAllText(savePath, json);

            Growl.Success($"主题已保存到: {savePath}");
        }
        catch (Exception ex)
        {
            Growl.Error($"保存失败: {ex.Message}");
        }
    }

    private void PreviewTheme()
    {
        // 临时保存当前编辑的主题并预览
        var tempPath = Path.Combine(Path.GetTempPath(), "preview_theme.json");
        try
        {
            var manifest = new ThemeManifest
            {
                Name = Name,
                Author = Author,
                Description = Description,
                Version = Version,
                Background = new BackgroundMeta
                {
                    Type = BackgroundType,
                    Color = BackgroundType == "SolidColor" ? BackgroundColor : null,
                    Image = BackgroundType == "Image" ? BackgroundImagePath : null,
                    FillMode = BackgroundFillMode,
                    LayerOpacity = LayerOpacity
                },
                AccentColor = AccentColors.Select(c => new AccentMeta
                {
                    Color = c.Color,
                    Duration = c.Duration
                }).ToList(),
                FontFamily = null,
                PreviewImage = null
            };

            var json = JsonSerializer.Serialize(manifest, App.AppJsonSerializerContext.ThemeManifest);
            File.WriteAllText(tempPath, json);

            // 保存当前选中的主题路径，以便恢复
            var selectedThemePath = Path.Combine(Core.App.StartupPath, "Config", "Themes", "selected");
            string? originalTheme = null;
            if (File.Exists(selectedThemePath))
            {
                originalTheme = File.ReadAllText(selectedThemePath).Trim();
            }

            // 创建临时主题文件夹
            var tempThemeDir = Path.Combine(Path.GetTempPath(), "preview_theme");
            if (Directory.Exists(tempThemeDir))
            {
                Directory.Delete(tempThemeDir, true);
            }

            Directory.CreateDirectory(tempThemeDir);
            File.Move(tempPath, Path.Combine(tempThemeDir, "index.json"));

            // 设置临时主题为当前主题
            File.WriteAllText(selectedThemePath, "preview_theme");

            Growl.Info("预览模式已启用，重启应用后恢复原主题");

            // 提示用户重启应用以查看效果
            var messageBox = MessageBoxManager.GetMessageBoxStandard("预览主题",
                "主题已应用预览。\n重启应用后会自动恢复原主题。\n\n是否立即重启？",
                ButtonEnum.YesNo, Icon.Question);

            messageBox.ShowAsync().ContinueWith(async t =>
            {
                if (t.Result == ButtonResult.Yes)
                {
                    // 重启应用
                    System.Diagnostics.Process.Start(Core.App.StartupPath, "MEFrpLauncherX.exe");
                    Environment.Exit(0);
                }
            });
        }
        catch (Exception ex)
        {
            Growl.Error($"预览失败: {ex.Message}");
        }
    }

    private async void BrowseBackground()
    {
        var dialog = await MainWindow.Instance.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择背景图片",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("图片文件")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif"]
                }
            ]
        });

        if (dialog.Count > 0)
        {
            BackgroundImagePath = HttpUtility.UrlDecode(
                dialog[0].Path.IsFile ? dialog[0].Path.AbsolutePath : dialog[0].Path.AbsoluteUri);
        }
    }

    private void LoadTheme(string themeFilePath)
    {
        try
        {
            var manifest = ThemeProcessor.LoadTheme(themeFilePath);
            if (manifest == null) return;

            Name = manifest.Name;
            Author = manifest.Author;
            Description = manifest.Description;
            Version = manifest.Version;
            PreviewImage = new Bitmap(Path.Combine(Path.GetDirectoryName(themeFilePath) ?? "",
                manifest.PreviewImage ?? "preview.png"));

            BackgroundType = manifest.Background.Type;
            IsSolidColorBackground = manifest.Background.Type == "SolidColor";
            IsImageBackground = manifest.Background.Type == "Image";
            BackgroundColor = manifest.Background.Color ?? "#FF1A1A1A";
            RealBackgroundColor = ColorToHexConverter.ParseHexString(BackgroundColor, AlphaComponentPosition.Leading);
            BackgroundImagePath = manifest.Background.Image ?? "";
            BackgroundFillMode = manifest.Background.FillMode;
            LayerOpacity = manifest.Background.LayerOpacity;

            AccentColors.Clear();
            foreach (var accent in manifest.AccentColor)
            {
                AccentColors.Add(new AccentColorItem { Color = accent.Color, Duration = accent.Duration });
            }

            if (AccentColors.Count == 0)
            {
                AddDefaultColors();
            }
        }
        catch (Exception ex)
        {
            Growl.Error($"加载主题失败: {ex.Message}");
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
    }
}

public class ValidHexStringValidator : ValidationAttribute
{
    public override bool IsValid(object? value) => value is string name && Color.TryParse(name, out _);
}

public class AccentColorItem : ReactiveObject
{
    private bool _isUpdating; // 防止递归

    public string Color
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            RealColor = ColorToHexConverter.ParseHexString(Color, AlphaComponentPosition.Leading);
        }
    } = "#FFFF0000";

    public Color? RealColor
    {
        get => field ?? ColorToHexConverter.ParseHexString(Color, AlphaComponentPosition.Leading);
        set
        {
            if (_isUpdating) return;
            this.RaiseAndSetIfChanged(ref field, value);
            _isUpdating = true;
            Color = ColorToHexConverter.ToHexString(value ?? Avalonia.Media.Color.Parse("#00000000"),
                AlphaComponentPosition.Leading, true, true);
            _isUpdating = false;
        }
    }

    public double Duration
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, Math.Max(0.1, value));
    } = 1.0;
}

public class RadioButtonConverter : IValueConverter
{
    public static RadioButtonConverter Instance
    {
        get;
    } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.Equals(parameter);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true)
            return parameter;
        return null;
    }
}

public class StringColorConverter : IValueConverter
{
    public static StringColorConverter Instance
    {
        get;
    } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Color.TryParse(value?.ToString() ?? "", out var color) ? color : null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}