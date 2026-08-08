using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsyncImageLoader;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Styling;
using MEFrpLauncherX.Views.Appearance;

namespace MEFrpLauncherX.Views;

public partial class MainWindow
{
    private CancellationTokenSource _accentAnimationCts;

    private async Task AnimateAccentColorAsync(List<AccentMeta> colors, CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            for (var i = 0; i < colors.Count; i++)
            {
                var currentColorMeta = colors[i];
                var nextColorMeta = colors[(i + 1) % colors.Count];

                if (!Color.TryParse(currentColorMeta.Color, out var startColor) ||
                    !Color.TryParse(nextColorMeta.Color, out var endColor))
                {
                    continue;
                }

                var duration = TimeSpan.FromSeconds(currentColorMeta.Duration);
                var startTime = DateTime.Now;
                var elapsed = TimeSpan.Zero;

                while (elapsed < duration && !cancellationToken.IsCancellationRequested)
                {
                    var t = elapsed.TotalMilliseconds / duration.TotalMilliseconds;
                    var interpolatedColor = InterpolateColor(startColor, endColor, t);

                    Dispatcher.UIThread.Post(() =>
                    {
                        App.FATheme?.CustomAccentColor = interpolatedColor;
                    });

                    await Task.Delay(16, cancellationToken); // ~60fps (1000ms/60 ≈ 16.67ms)
                    elapsed = DateTime.Now - startTime;
                }

                // 确保最终颜色精确
                if (!cancellationToken.IsCancellationRequested)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        App.FATheme?.CustomAccentColor = endColor;
                    });
                }
            }
        }
    }

    private Color InterpolateColor(Color start, Color end, double t)
    {
        // 使用平滑曲线 (ease in-out) 让呼吸效果更自然
        t = Math.Max(0, Math.Min(1, t));
        t = t * t * (3 - 2 * t); // SmoothStep 缓动

        return new Color(
            (byte)(start.A + (end.A - start.A) * t),
            (byte)(start.R + (end.R - start.R) * t),
            (byte)(start.G + (end.G - start.G) * t),
            (byte)(start.B + (end.B - start.B) * t)
        );
    }


    internal async Task ApplyThemeAsync()
    {
        string selectedTheme;
        try
        {
            selectedTheme =
                (await File.ReadAllTextAsync(Path.Combine(Core.App.StartupPath, "Config", "Themes",
                    "selected")))
                .Trim();
        }
        catch (FileNotFoundException)
        {
            Core.App.CurrentLogger.Log("未找到主题配置文件，跳过主题加载");
            return;
        }
        catch (Exception ex)
        {
            Core.App.CurrentLogger.Error(ex, "加载主题配置文件时发生错误");
            return;
        }

        if (selectedTheme.IsNullOrEmpty())
        {
            return;
        }

        var themePath = Path.Combine(Core.App.StartupPath, "Config", "Themes", selectedTheme);
        var themeManifest =
            ThemeProcessor.LoadTheme(Path.Combine(themePath, "index.json"));
        if (themeManifest != null)
        {
            FooterButtonSettingsItem.ClearFile();
            await ConfigManager.UpdateConfigAsync(c =>
            {
                if (themeManifest.Background.Type == "Image")
                {
                    var fullImagePath = Path.GetFullPath(themeManifest.Background.Image,
                        Path.Combine(Core.App.StartupPath, "Config", "Themes", selectedTheme));
                    c.BackgroundSettings.BackgroundImage = fullImagePath;
                    c.BackgroundSettings.Stretch = themeManifest.Background.FillMode;
                }
                else
                {
#pragma warning disable CS8625 // 无法将 null 字面量转换为非 null 的引用类型。
                    c.BackgroundSettings.BackgroundImage = null;
#pragma warning restore CS8625 // 无法将 null 字面量转换为非 null 的引用类型。
                }

                c.BackgroundSettings.LayerOpacity = themeManifest.Background.LayerOpacity;
            });
            AppearanceSettings.UpdateBackground(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
                Background = CreateBackgroundBrush(themeManifest.Background));
            if (themeManifest.AccentColor.Count == 1)
            {
                if (themeManifest.AccentColor.FirstOrDefault()?.Color is "accent" or "system" or "" or null)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        App.FATheme?.CustomAccentColor = null;
                        App.FATheme?.PreferUserAccentColor = true;
                    });
                    await ConfigManager.UpdateConfigAsync(cfg =>
                        cfg.AccentColor = string.Empty);
                    try
                    {
                        await _accentAnimationCts?.CancelAsync();
                    }
                    catch (NullReferenceException e)
                    {
                        System.Console.WriteLine(e);
                    }
                }

                else
                {
                    await ConfigManager.UpdateConfigAsync(cfg =>
                        cfg.AccentColor = themeManifest.AccentColor.FirstOrDefault()?.Color!);
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        App.FATheme?.CustomAccentColor =
                            Color.TryParse(ConfigManager.CurrentConfig.AccentColor, out var color) ? color : null;
                    });
                }
            }
            else
            {
                try
                {
#pragma warning disable CS8602 // 解引用可能出现空引用。
                    await _accentAnimationCts?.CancelAsync();
#pragma warning restore CS8602 // 解引用可能出现空引用。
                }
                catch (NullReferenceException e)
                {
                    System.Console.WriteLine(e);
                }

                _accentAnimationCts = new CancellationTokenSource();
                _ = AnimateAccentColorAsync(themeManifest.AccentColor, _accentAnimationCts.Token);
            }

            if (themeManifest.FontFamily is not null)
            {
                var fontFamily = themeManifest.FontFamily;
                var ff = ThemeProcessor.IsFontFilePath(fontFamily)
                    ? new FontFamily(new Uri(Path.Combine(themePath, fontFamily)),
                        Path.GetFileNameWithoutExtension(fontFamily))
                    : new FontFamily(fontFamily);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Application.Current.Resources["GlobalFontFamily"] = ff;
                    Application.Current.Resources["ContentControlThemeFontFamily"] = ff;
                });
                //InvalidateVisual();
            }
        }
    }

    public static IBrush CreateBackgroundBrush(BackgroundMeta background)
    {
        if (background is { Type: "SolidColor", Color: not null })
        {
            var color = Color.Parse(background.Color); // 支持 #AARRGGBB 或 #RRGGBB
            var brush = new SolidColorBrush(color);
            brush.Opacity = background.LayerOpacity; // 应用透明度
            var baseColor = Color.Parse(background.Color);
            return background.FillMode switch
            {
                "Radiation" =>
                    // 径向渐变（中心亮色，边缘深色）
                    new RadialGradientBrush
                    {
                        GradientStops =
                        [
                            new GradientStop(baseColor, 0.0),
                            new GradientStop(new Color(0xCC, baseColor.R, baseColor.G, baseColor.B), 1.0)
                        ]
                    },
                "Gradient" =>
                    // 线性渐变（例如从上到下）
                    new LinearGradientBrush
                    {
                        GradientStops =
                        [
                            new GradientStop(baseColor, 0.0),
                            new GradientStop(new Color(0xCC, baseColor.R, baseColor.G, baseColor.B), 1.0)
                        ],
                        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative)
                    },
                _ => new SolidColorBrush(baseColor)
            };
        }

        var imageBrush = new ImageBrush
        {
            Stretch = background.FillMode switch
            {
                "Uniform" => Stretch.Uniform,
                "UniformToFill" => Stretch.UniformToFill,
                _ => Stretch.Fill
            }
        };
        ImageBrushLoader.SetSource(imageBrush, background.Image);
        //imageBrush.Opacity = background.LayerOpacity;
        return imageBrush;
    }
}