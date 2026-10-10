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

    /// <summary>本帧待应用的最新强调色（可能已被后续插值覆盖）。</summary>
    private Color? _pendingAccentColor;

    /// <summary>是否已有一帧合并后的更新排队，防止同一帧内重复触发主题重算。</summary>
    private bool _accentUpdateScheduled;

    private async Task AnimateAccentColorAsync(List<AccentMeta> colors, CancellationToken cancellationToken = default)
    {
        if (colors is null || colors.Count == 0)
        {
            return;
        }

        try
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

                    // 修复：原实现是「先计算、再 await 16ms、再重新取时间」，
                    // 而 Dispatcher.Post + 插值本身也要耗时，导致每帧实际间隔 ≈ 16ms + 计算耗时，
                    // 动画整体比 Duration 声明的时间更长。现在时间基准只取自 startTime，
                    // 并把「是否结束」作为循环条件，误差不再累积。
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var t = (DateTime.Now - startTime).TotalMilliseconds / duration.TotalMilliseconds;
                        if (t >= 1)
                        {
                            break;
                        }

                        QueueAccentColor(InterpolateColor(startColor, endColor, t));

                        await Task.Delay(16, cancellationToken);
                    }

                    // 确保最终颜色精确
                    QueueAccentColor(endColor);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 主题切换或窗口关闭时的正常取消路径，无需处理
        }
    }

    /// <summary>
    ///     把插值后的强调色排入 UI 线程，并保证同一帧内最多只应用一次。
    /// </summary>
    /// <remarks>
    ///     FluentAvaloniaTheme.CustomAccentColor 的 setter 在值变化时会调用 LoadCustomAccentColor()，
    ///     进而 UpdateAccentColors() 移除并重建包含 7 个 SystemAccentColor* 键的 ResourceDictionary，
    ///     再 Add/Remove 到 Resources.MergedDictionaries —— 这会让整棵可视树的 DynamicResource
    ///     全部失效并重新解析。原实现每个插值步都直接 Post 一次，60fps 下等于每秒 60 次全量主题失效。
    ///     现在改为「只保留最新颜色 + 每帧最多应用一次」，把开销从 O(60/秒) 降到 O(1/帧)。
    /// </remarks>
    private void QueueAccentColor(Color color)
    {
        _pendingAccentColor = color;

        if (_accentUpdateScheduled)
        {
            return;
        }

        _accentUpdateScheduled = true;
        Dispatcher.UIThread.Post(() =>
        {
            _accentUpdateScheduled = false;

            var pending = _pendingAccentColor;
            if (pending.HasValue)
            {
                App.FATheme?.CustomAccentColor = pending.Value;
            }
        });
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
            selectedTheme = (await File.ReadAllTextAsync(Core.AppPaths.SelectedThemeFile)).Trim();
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

        var themePath = Path.Combine(Core.AppPaths.ThemesDirectory, selectedTheme);
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
                        Path.Combine(Core.AppPaths.ThemesDirectory, selectedTheme));
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
            var brush = new SolidColorBrush(color)
            {
                Opacity = background.LayerOpacity // 应用透明度
            };
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