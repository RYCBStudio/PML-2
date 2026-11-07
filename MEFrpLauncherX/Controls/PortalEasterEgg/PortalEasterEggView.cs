using System;
using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using MEFrpLauncherX.Core;

namespace MEFrpLauncherX.Controls.PortalEasterEgg;

/// <summary>
///     「传送门彩蛋」的动画视图。
/// </summary>
/// <remarks>
///     <para>
///         全部内容由 <see cref="DrawingContext" /> 直接绘制（圆环、光晕、星点、粒子），
///         不引入任何图形引擎，也不依赖第二套动画基础设施：每帧的重绘由
///         <see cref="RollingAnimationTicker" /> 这个仓库已有的共享 60fps 时钟驱动。
///     </para>
///     <para>
///         时间轴分四段：Opening（聚光成环）→ Active（多层环旋转 + 粒子沿轨道运动）→
///         Expansion（短暂扩张、粒子外溢）→ Closing（收缩淡出）。标准模式总时长 5.2s。
///         精简模式（<c>ConfigManager.CurrentConfig.AnimationLevel &lt;= 1</c>）只保留
///         「淡入 → 静置 → 淡出」，总时长 1.6s，不做旋转与粒子位移。
///     </para>
///     <para>
///         所有着色都只作用于本控件绘制的内容，不读取、也不覆盖应用主题资源；
///         深/浅色主题下使用两套略有色差的深空底色，保证在浅色界面里也不会突兀。
///     </para>
/// </remarks>
public sealed class PortalEasterEggView : Control
{
    private const string CaptionText = "Portal opened. Welcome to another world.";
    private const string HintText = "ESC";

    // ---- 标准模式时间轴（秒）：总时长 5.2s ----
    private const double OpeningEnd = 1.10;
    private const double ActiveEnd = 3.70;
    private const double ExpansionEnd = 4.50;
    private const double ClosingEnd = 5.20;

    // ---- 精简模式时间轴（秒）：只做透明度变化 ----
    private const double ReducedOpeningEnd = 0.30;
    private const double ReducedHoldEnd = 1.10;
    private const double ReducedClosingEnd = 1.60;

    private const int StarCount = 44;
    private const int OrbitParticleCount = 24;
    private const int FlowParticleCount = 12;

    private static readonly Color Cyan = Color.FromRgb(0x5F, 0xF3, 0xE4);
    private static readonly Color Teal = Color.FromRgb(0x2E, 0xD9, 0xC8);
    private static readonly Color Azure = Color.FromRgb(0x49, 0xB6, 0xFF);
    private static readonly Color Pale = Color.FromRgb(0xA8, 0xF7, 0xEF);

    private readonly Stopwatch _clock = new();
    private readonly Action _tick;
    private readonly bool _reduced;
    private readonly Star[] _stars;
    private readonly Particle[] _particles;
    private readonly RingStyle[] _rings;
    private readonly IBrush?[] _starBrushes;
    private readonly IBrush?[] _particleFields;
    private readonly IBrush?[] _particleGlows;
    private readonly Pen _hintBorderPen;

    private IBrush? _coreInner;
    private IBrush? _coreMid;
    private IBrush? _coreOuter;

    private bool _started;
    private bool _running;
    private bool _completed;

    private Size _cacheSize;
    private bool _cacheDark;
    private IBrush? _background;
    private FormattedText? _caption;
    private FormattedText? _hint;
    private Typeface _typeface = Typeface.Default;

    public PortalEasterEggView()
    {
        _tick = OnTick;
        _reduced = ResolveReducedMotion();
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
        // 点击统一由宿主 overlay 处理，视图本身不参与命中测试
        IsHitTestVisible = false;

        // 固定随机种子：每次彩蛋的星点 / 粒子布局一致，避免「每次都不一样」影响观感对比
        var rng = new Random(0x5EED_2026);

        _stars = new Star[StarCount];
        for (var i = 0; i < _stars.Length; i++)
        {
            _stars[i] = new Star
            {
                X = rng.NextDouble(),
                Y = rng.NextDouble(),
                Size = 0.5 + rng.NextDouble() * 1.1,
                Phase = rng.NextDouble() * Math.PI * 2,
            };
        }

        _particles = new Particle[OrbitParticleCount + FlowParticleCount];
        for (var i = 0; i < OrbitParticleCount; i++)
        {
            _particles[i] = new Particle
            {
                Flow = false,
                Angle = rng.NextDouble() * Math.PI * 2,
                Orbit = rng.NextDouble(),
                Speed = (0.25 + rng.NextDouble() * 0.55) * (rng.Next(2) == 0 ? -1 : 1),
                Phase = rng.NextDouble() * Math.PI * 2,
                Size = 0.9 + rng.NextDouble() * 1.6,
                Tint = rng.Next(3),
            };
        }

        for (var i = OrbitParticleCount; i < _particles.Length; i++)
        {
            _particles[i] = new Particle
            {
                Flow = true,
                Angle = rng.NextDouble() * Math.PI * 2,
                Orbit = 0,
                Speed = 0.10 + rng.NextDouble() * 0.16,
                Phase = rng.NextDouble(),
                Size = 1.0 + rng.NextDouble() * 1.8,
                Tint = rng.Next(3),
            };
        }

        _rings =
        [
            new RingStyle(0.30, 0.34, BuildPens(Cyan, 1.7, null)),
            new RingStyle(0.42, -0.21, BuildPens(Teal, 1.4, new DashStyle([6, 9], 0))),
            new RingStyle(0.55, 0.13, BuildPens(Azure, 1.2, new DashStyle([3, 13], 0.5))),
            new RingStyle(0.70, -0.08, BuildPens(Pale, 1.0, new DashStyle([1.5, 22], 0))),
        ];

        _starBrushes =
        [
            Brush(0x24, Pale),
            Brush(0x48, Pale),
            Brush(0x86, Pale),
        ];

        _particleFields =
        [
            Brush(0xC8, Cyan),
            Brush(0xC8, Teal),
            Brush(0xC8, Azure),
        ];
        _particleGlows =
        [
            Brush(0x30, Cyan),
            Brush(0x30, Teal),
            Brush(0x30, Azure),
        ];

        _hintBorderPen = new Pen(Brush(0x40, Pale), 1);

        _coreInner = BuildCoreBrush(0xFA, 0xF2, 0xFF, 0xFD, 0xB4, 0x8F, 0xF7, 0xEC);
        _coreMid = BuildCoreBrush(0x96, 0x8F, 0xF7, 0xEC, 0x28, 0x4F, 0xE3, 0xE0);
        _coreOuter = BuildCoreBrush(0x3C, 0x4F, 0xE3, 0xE0, 0x00, 0x2E, 0xD9, 0xC8);
    }

    /// <summary>动画播放完毕（自然走完 Closing）。主动 <see cref="Stop" /> 不会触发该事件。</summary>
    public event EventHandler? Completed;

    /// <summary>标准 / 精简模式下的总时长。</summary>
    public TimeSpan Duration => TimeSpan.FromSeconds(_reduced ? ReducedClosingEnd : ClosingEnd);

    /// <summary>本次播放是否走了精简（无运动）路径。</summary>
    public bool IsReducedMotion => _reduced;

    /// <summary>开始播放。重复调用无副作用。</summary>
    public void Start()
    {
        if (_running)
        {
            return;
        }

        _started = true;
        _running = true;
        _completed = false;
        _clock.Restart();
        RollingAnimationTicker.Register(_tick);
        InvalidateVisual();
    }

    /// <summary>立即停止并释放每帧回调；不会触发 <see cref="Completed" />。</summary>
    public void Stop()
    {
        _running = false;
        _clock.Stop();
        RollingAnimationTicker.Unregister(_tick);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // 防御性清理：即使宿主忘了 Stop，也不会让共享时钟继续持有本控件（闭包会阻止回收）
        Stop();
        base.OnDetachedFromVisualTree(e);
    }

    private static bool ResolveReducedMotion()
    {
        try
        {
            // 0=关闭动画 1=精简 2=标准：前两者都按「减少动态效果」处理
            return ConfigManager.CurrentConfig.AnimationLevel <= 1;
        }
        catch
        {
            // 配置尚未就绪时按标准模式处理，与 TabContentFadeScaleTransition 的降级策略一致
            return false;
        }
    }

    private void OnTick()
    {
        if (!_running)
        {
            return;
        }

        if (_clock.Elapsed.TotalSeconds >= Duration.TotalSeconds)
        {
            Finish();
            return;
        }

        InvalidateVisual();
    }

    private void Finish()
    {
        Stop();
        if (_completed)
        {
            return;
        }

        _completed = true;
        Completed?.Invoke(this, EventArgs.Empty);
    }

    public override void Render(DrawingContext context)
    {
        if (!_started)
        {
            return;
        }

        var bounds = new Rect(Bounds.Size);
        if (bounds.Width < 4 || bounds.Height < 4)
        {
            return;
        }

        var dark = ActualThemeVariant == ThemeVariant.Dark;
        EnsureResources(bounds.Size, dark);

        var t = Math.Min(_clock.Elapsed.TotalSeconds, Duration.TotalSeconds);
        var center = bounds.Center;
        var min = Math.Min(bounds.Width, bounds.Height);
        var unit = min / 2.0;

        double opacity;
        double open;
        double expand;
        double close;
        double rotation;
        double captionAlpha;
        double hintAlpha;

        if (_reduced)
        {
            open = SmoothStep(0, ReducedOpeningEnd, t);
            expand = 0;
            close = SmoothStep(ReducedHoldEnd, ReducedClosingEnd, t);
            opacity = open * (1 - close);
            rotation = 0;
            captionAlpha = 0.55 * SmoothStep(0.10, 0.35, t) * (1 - close);
            hintAlpha = 0.26 * (1 - close);
        }
        else
        {
            open = EaseOutCubic(Progress(t, 0, OpeningEnd));
            var active = Progress(t, OpeningEnd, ActiveEnd);
            expand = EaseInOut(Progress(t, ActiveEnd, ExpansionEnd));
            close = Progress(t, ExpansionEnd, ClosingEnd);
            opacity = open * (1 - EaseInCubic(close));
            rotation = t * 0.35;
            captionAlpha = 0.55 * SmoothStep(0.20, 0.70, active) * (1 - close);
            hintAlpha = 0.26 * SmoothStep(0, 0.35, active) * (1 - close);
        }

        using (context.PushOpacity(opacity))
        {
            context.DrawRectangle(_background, null, bounds);

            DrawStars(context, bounds, t);

            // 开场时环从 0.86 倍「长」到 1 倍；扩张阶段放大，收尾阶段收缩
            var ringScale = (0.86 + 0.14 * open)
                            * (1 + 0.26 * expand)
                            * (1 - 0.38 * EaseInCubic(close));

            foreach (var ring in _rings)
            {
                var radius = unit * ring.RadiusFactor * ringScale;
                if (radius <= 1)
                {
                    continue;
                }

                var spin = _reduced ? 0 : rotation * ring.SpeedFactor;
                using (context.PushTransform(
                           Matrix.CreateRotation(spin) * Matrix.CreateTranslation(center.X, center.Y)))
                {
                    context.DrawEllipse(null, ring.Glow, default, radius, radius);
                    context.DrawEllipse(null, ring.Halo, default, radius, radius);
                    context.DrawEllipse(null, ring.Core, default, radius, radius);
                }
            }

            DrawCore(context, center, min, t, open, expand, close);

            if (!_reduced)
            {
                DrawParticles(context, center, unit, t, expand);
            }
        }

        // 文案与 Esc 提示单独控制透明度：不希望它们跟着背景一起在收尾阶段被压到全透明
        if (captionAlpha > 0.01 && _caption is not null)
        {
            using (context.PushOpacity(captionAlpha))
            {
                context.DrawText(_caption,
                    new Point((bounds.Width - _caption.Width) / 2, bounds.Height * 0.80));
            }
        }

        if (hintAlpha > 0.01 && _hint is not null)
        {
            using (context.PushOpacity(hintAlpha))
            {
                var pad = new Size(9, 5);
                var rect = new Rect(
                    bounds.Width - _hint.Width - 26 - pad.Width * 2,
                    bounds.Height - _hint.Height - 26 - pad.Height * 2,
                    _hint.Width + pad.Width * 2,
                    _hint.Height + pad.Height * 2);

                context.DrawRectangle(null, _hintBorderPen, rect, 4, 4);
                context.DrawText(_hint, new Point(rect.X + pad.Width, rect.Y + pad.Height));
            }
        }
    }

    private void DrawStars(DrawingContext context, Rect bounds, double t)
    {
        foreach (var star in _stars)
        {
            var twinkle = (Math.Sin(t * 1.35 + star.Phase) + 1) * 0.5;
            var brush = twinkle > 0.72
                ? _starBrushes[2]
                : twinkle > 0.38
                    ? _starBrushes[1]
                    : _starBrushes[0];

            var point = new Point(star.X * bounds.Width, star.Y * bounds.Height);
            context.DrawEllipse(brush, null, point, star.Size, star.Size);
        }
    }

    private void DrawCore(
        DrawingContext context,
        Point center,
        double min,
        double t,
        double open,
        double expand,
        double close)
    {
        if (open <= 0)
        {
            return;
        }

        var pulse = 1 + 0.05 * Math.Sin(t * 3.4);
        var radius = min * 0.032 * open * pulse * (1 + 0.5 * expand) * (1 - 0.55 * EaseInCubic(close));
        if (radius <= 0.5)
        {
            return;
        }

        context.DrawEllipse(_coreOuter, null, center, radius * 5.2, radius * 5.2);
        context.DrawEllipse(_coreMid, null, center, radius * 2.2, radius * 2.2);
        context.DrawEllipse(_coreInner, null, center, radius, radius);
    }

    private void DrawParticles(DrawingContext context, Point center, double unit, double t, double expand)
    {
        foreach (var particle in _particles)
        {
            double angle;
            double radius;

            if (particle.Flow)
            {
                // 「穿越中心」的粒子：沿半径方向反复流过传送门
                var k = Repeat01(t * particle.Speed + particle.Phase);
                radius = unit * (0.06 + k * (0.52 + 0.50 * expand));
                angle = particle.Angle + t * 0.18;
            }
            else
            {
                // 沿轨道运动的粒子
                radius = unit * (0.34 + particle.Orbit * 0.30) * (1 + 0.30 * expand);
                angle = particle.Angle + t * particle.Speed;
            }

            var position = new Point(
                center.X + Math.Cos(angle) * radius,
                center.Y + Math.Sin(angle) * radius);

            var bright = Math.Sin(t * 2.1 + particle.Phase) > -0.2;
            var size = particle.Size * (bright ? 1 : 0.62) * (1 + 0.5 * expand);

            context.DrawEllipse(_particleGlows[particle.Tint], null, position, size * 3, size * 3);
            context.DrawEllipse(_particleFields[particle.Tint], null, position, size, size);
        }
    }

    private void EnsureResources(Size size, bool dark)
    {
        if (_background is not null && _cacheSize == size && _cacheDark == dark)
        {
            return;
        }

        _cacheSize = size;
        _cacheDark = dark;
        _typeface = ResolveTypeface();
        _background = BuildBackground(dark);

        var min = Math.Min(size.Width, size.Height);
        var captionSize = Math.Clamp(min * 0.026, 11, 16);
        var hintSize = Math.Clamp(min * 0.018, 10, 12);
        var textBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xD8, 0xFA, 0xF7));

        _caption = new FormattedText(
            CaptionText,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            _typeface,
            captionSize,
            textBrush);

        _hint = new FormattedText(
            HintText,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            _typeface,
            hintSize,
            textBrush);
    }

    private static Typeface ResolveTypeface()
    {
        // 与全局字体保持一致：取不到时退回默认字体，不影响动画本身
        if (Application.Current?.FindResource("GlobalFontFamily") is FontFamily family)
        {
            return new Typeface(family);
        }

        return Typeface.Default;
    }

    private static IBrush BuildBackground(bool dark)
    {
        // 深海蓝 → 近黑的径向渐变：中心略亮，边缘彻底沉下去，作为传送门的「空间」
        return new RadialGradientBrush
        {
            Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            GradientOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            RadiusX = new RelativeScalar(0.9, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(0.9, RelativeUnit.Relative),
            GradientStops =
            {
                // 浅色主题下略微提亮中心，避免纯黑遮罩在白色界面上显得像「渲染失败」
                new GradientStop(dark ? Color.Parse("#0A2236") : Color.Parse("#103149"), 0),
                new GradientStop(dark ? Color.Parse("#061320") : Color.Parse("#0A2434"), 0.55),
                new GradientStop(dark ? Color.Parse("#01040A") : Color.Parse("#02090F"), 1),
            },
        };
    }

    private static IBrush BuildCoreBrush(
        byte a1, byte r1, byte g1, byte b1,
        byte a2, byte r2, byte g2, byte b2)
    {
        return new RadialGradientBrush
        {
            Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            GradientOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            RadiusX = new RelativeScalar(0.5, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(0.5, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(a1, r1, g1, b1), 0),
                new GradientStop(Color.FromArgb(a2, r2, g2, b2), 0.55),
                new GradientStop(Color.FromArgb(0, r2, g2, b2), 1),
            },
        };
    }

    private static RingPens BuildPens(Color color, double thickness, IDashStyle? dash)
    {
        return new RingPens(
            new Pen(Brush(0x1C, color), thickness * 4.2),
            new Pen(Brush(0x34, color), thickness * 2.1),
            new Pen(Brush(0xF0, color), thickness, dash));
    }

    private static IBrush Brush(byte alpha, Color color) =>
        new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));

    /// <summary>把 [0,∞) 的输入折回 [0,1)，供循环流动的粒子使用。</summary>
    private static double Repeat01(double value)
    {
        var result = value % 1.0;
        return result < 0 ? result + 1 : result;
    }

    private static double Progress(double t, double start, double end) =>
        end <= start ? 1 : Math.Clamp((t - start) / (end - start), 0, 1);

    private static double EaseOutCubic(double x) => 1 - Math.Pow(1 - x, 3);

    private static double EaseInCubic(double x) => x * x * x;

    private static double EaseInOut(double x) => x * x * (3 - 2 * x);

    private static double SmoothStep(double edge0, double edge1, double x) =>
        EaseInOut(Progress(x, edge0, edge1));

    private readonly record struct RingPens(Pen Glow, Pen Halo, Pen Core);

    private sealed class RingStyle(double radiusFactor, double speedFactor, RingPens pens)
    {
        public double RadiusFactor { get; } = radiusFactor;

        public double SpeedFactor { get; } = speedFactor;

        public Pen Glow { get; } = pens.Glow;

        public Pen Halo { get; } = pens.Halo;

        public Pen Core { get; } = pens.Core;
    }

    private struct Star
    {
        public double X;
        public double Y;
        public double Size;
        public double Phase;
    }

    private struct Particle
    {
        public bool Flow;
        public double Angle;
        public double Orbit;
        public double Speed;
        public double Phase;
        public double Size;
        public int Tint;
    }
}
