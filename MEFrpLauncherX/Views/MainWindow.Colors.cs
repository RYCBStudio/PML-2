using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using MEFrpLauncherX.Core.Styling;

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

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        App.FATheme?.CustomAccentColor = interpolatedColor;
                    });

                    await Task.Delay(16, cancellationToken); // ~60fps (1000ms/60 ≈ 16.67ms)
                    elapsed = DateTime.Now - startTime;
                }

                // 确保最终颜色精确
                if (!cancellationToken.IsCancellationRequested)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
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
}