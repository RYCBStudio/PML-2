using System.Drawing;
using System.Globalization;
using System.Text;
using System.Xml;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Svg;
using Avalonia.Threading;
using SkiaSharp;
using Color = Avalonia.Media.Color;
using Point = Avalonia.Point;
using Size = Avalonia.Size;
using SKPath = SkiaSharp.SKPath;
using SKPoint = SkiaSharp.SKPoint;

namespace MarkdownAIRender.Controls.Images;

public class ImagesRender : UserControl
{
    private static readonly HttpClient HttpClient = new();

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<ImagesRender, string?>(nameof(Value));

    static ImagesRender()
    {
        HttpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36 Edg/151.0.0.0");
    }

    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (!string.IsNullOrEmpty(Value))
        {
            _ = LoadImageAsync(Value!);
        }
    }

    private static bool IsSvgFile(Stream fileStream)
    {
        try
        {
            var firstChr = fileStream.ReadByte();
            if (firstChr != ('<' & 0xFF))
            {
                return false;
            }

            fileStream.Seek(0, SeekOrigin.Begin);
            using var xmlReader = XmlReader.Create(fileStream);
            return xmlReader.MoveToContent() == XmlNodeType.Element &&
                   "svg".Equals(xmlReader.Name, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
        finally
        {
            fileStream.Seek(0, SeekOrigin.Begin);
        }
    }

    /// <summary>
    ///     Load image either from base64 string, local file path, or remote URL.
    /// </summary>
    /// <param name="input">Base64, file path, or URL to image.</param>
    public async Task LoadImageAsync(string input)
    {
        try
        {
            if (IsDataUri(input))
            {
                await LoadImageFromBase64(input);
            }
            else if (IsLocalFile(input))
            {
                await LoadImageFromLocalFile(input);
            }
            else
            {
                await LoadImageFromRemote(input);
            }
        }
        catch
        {
            // ignore
        }
    }

    private bool IsDataUri(string input)
    {
        return input.StartsWith("data:image", StringComparison.OrdinalIgnoreCase)
               && input.Contains("base64,");
    }

    private bool IsLocalFile(string input)
    {
        if (Uri.TryCreate(input, UriKind.RelativeOrAbsolute, out var uri))
        {
            if (uri.IsFile)
            {
                return true;
            }
        }

        return File.Exists(input);
    }

    private async Task LoadImageFromBase64(string dataUri)
    {
        var base64Data = dataUri[(dataUri.IndexOf(',') + 1)..];
        var bytes = Convert.FromBase64String(base64Data);

        using var stream = new MemoryStream(bytes);
        var bitmap = new Bitmap(stream);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var imageCtrl = CreateImageControl(bitmap);
            Content = imageCtrl;
        });
    }

    private async Task LoadImageFromLocalFile(string filePath)
    {
        await using var fileStream = File.OpenRead(filePath);

        if (filePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) || IsSvgFile(fileStream))
        {
            fileStream.Seek(0, SeekOrigin.Begin);
            string svgXml;
            using (var reader = new StreamReader(fileStream))
            {
                svgXml = await reader.ReadToEndAsync();
            }

            if (TryExtractAnimateMotionAndColor(svgXml, out var animInfo))
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var animatedCtrl = new AnimatedSvgTextControl(animInfo!);
                    Content = animatedCtrl;
                });
            }
            else
            {
                // 静态 SVG
                using var memStream = new MemoryStream(Encoding.UTF8.GetBytes(svgXml));
                var svgSource = SvgSource.Load(memStream);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Content = new Border
                    {
                        Child = new Image
                        {
                            Source = new SvgImage()
                            {
                                Source = svgSource
                            },
                            Stretch = Stretch.Uniform,
                            Margin = new Thickness(10)
                        }
                    };
                });
            }

            return;
        }

        fileStream.Seek(0, SeekOrigin.Begin);
        var bitmap = new Bitmap(fileStream);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var imageCtrl = CreateImageControl(bitmap);
            Content = imageCtrl;
        });
    }

    private async Task LoadImageFromRemote(string url)
    {
        var response = await HttpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            return;
        }

        var bytes = await response.Content.ReadAsByteArrayAsync();
        using var memStream = new MemoryStream(bytes);

        if (url.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) || IsSvgFile(memStream))
        {
            memStream.Seek(0, SeekOrigin.Begin);
            string svgXml;
            using (var reader = new StreamReader(memStream))
            {
                svgXml = await reader.ReadToEndAsync();
            }

            if (TryExtractAnimateMotionAndColor(svgXml, out var animInfo))
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var animatedCtrl = new AnimatedSvgTextControl(animInfo!);
                    Content = new Border { Child = animatedCtrl };
                });
            }
            else
            {
                // 静态 SVG
                memStream.Seek(0, SeekOrigin.Begin);
                var svgSource = SvgSource.Load(memStream);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Content = new Border
                    {
                        Child = new Image
                        {
                            Source = new SvgImage()
                            {
                                Source = svgSource
                            },
                            Stretch = Stretch.Uniform,
                            Margin = new Thickness(10)
                        }
                    };
                });
            }

            return;
        }

        memStream.Seek(0, SeekOrigin.Begin);
        var bitmap = new Bitmap(memStream);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var imageCtrl = CreateImageControl(bitmap);
            Content = imageCtrl;
        });
    }

    // protected override Size MeasureOverride(Size availableSize)
    // {
    //     base.MeasureOverride(availableSize);
    //     var desiredWidth = 600;
    //     var desiredHeight = 400;
    //     return new Size(
    //         Math.Min(desiredWidth, availableSize.Width),
    //         Math.Min(desiredHeight, availableSize.Height)
    //     );
    // }

    protected override Size ArrangeOverride(Size finalSize)
    {
        return base.ArrangeOverride(finalSize);
    }

    private static Image CreateImageControl(Bitmap bitmap)
    {
        var imageControl = new Image { Stretch = Stretch.Uniform, Source = bitmap, Margin = new Thickness(10) };
        imageControl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        imageControl.Arrange(new Rect(imageControl.DesiredSize));
        return imageControl;
    }

    private bool TryExtractAnimateMotionAndColor(string svgXml, out AnimateInfo? info)
    {
        info = null;
        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(svgXml);

            var animateMotionNode = doc.GetElementsByTagName("animateMotion")
                .Cast<XmlNode>()
                .FirstOrDefault();

            if (animateMotionNode == null)
            {
                return false;
            }

            var pathAttr = animateMotionNode.Attributes?["path"]?.Value;
            if (string.IsNullOrEmpty(pathAttr))
            {
                return false;
            }

            var pathNode = doc.GetElementsByTagName("path")
                .Cast<XmlNode>()
                .FirstOrDefault();

            var strokeAttr = pathNode?.Attributes?["stroke"]?.Value;
            double.TryParse(pathNode?.Attributes?["stroke-width"]?.Value, out var strokeWidth);
            var fillAttr = pathNode?.Attributes?["fill"]?.Value;
            var durAttr = animateMotionNode.Attributes?["dur"]?.Value ?? "3s";
            var durSeconds = ParseDurToSeconds(durAttr);
            var repeatCountAttr = animateMotionNode.Attributes?["repeatCount"]?.Value ?? "indefinite";

            var animateColorNode = doc.GetElementsByTagName("animate")
                .Cast<XmlNode>()
                .FirstOrDefault(n => n.Attributes?["attributeName"]?.Value == "fill");

            string? fromColor = null, toColor = null;
            var colorDurSeconds = durSeconds;
            var colorRepeat = repeatCountAttr;

            if (animateColorNode != null)
            {
                fromColor = animateColorNode.Attributes?["from"]?.Value;
                toColor = animateColorNode.Attributes?["to"]?.Value;

                var durColor = animateColorNode.Attributes?["dur"]?.Value;
                if (!string.IsNullOrEmpty(durColor))
                {
                    colorDurSeconds = ParseDurToSeconds(durColor);
                }

                var repColor = animateColorNode.Attributes?["repeatCount"]?.Value;
                if (!string.IsNullOrEmpty(repColor))
                {
                    colorRepeat = repColor;
                }
            }

            var textNode = animateMotionNode.ParentNode;
            var textContent = textNode?.InnerText?.Trim() ?? "SVG";

            info = new AnimateInfo
            {
                Text = textContent,
                PathData = pathAttr,
                MoveDuration = durSeconds,
                MoveRepeatCount = repeatCountAttr,
                FromColor = fromColor,
                ToColor = toColor,
                ColorDuration = colorDurSeconds,
                ColorRepeatCount = colorRepeat,
                PathStroke = strokeAttr,
                PathStrokeWidth = strokeWidth,
                PathFill = fillAttr
            };
            return true;
        }
        catch
        {
            return false;
        }
    }

    private double ParseDurToSeconds(string durValue)
    {
        if (durValue.EndsWith("s"))
        {
            var numPart = durValue[..^1];
            if (double.TryParse(numPart, out var seconds))
            {
                return seconds;
            }
        }

        return 3.0;
    }
}

public class AnimatedSvgTextControl : Control
{
    private readonly double _colorDuration;
    private readonly string? _colorRepeatCount;
    private readonly string? _fromColor;
    private readonly double _moveDuration;
    private readonly string? _moveRepeatCount;
    private readonly string? _pathData;
    private readonly IBrush? _pathFillBrush;
    private readonly IBrush? _pathStrokeBrush;
    private readonly double _pathStrokeThickness = 1.0;
    private readonly string? _text;
    private readonly string? _toColor;

    private PathGeometry? _avaloniaPathGeo;
    private double _colorProgress;
    private DispatcherTimer? _colorTimer;
    private SolidColorBrush _currentBrush = new(Colors.Red);
    private FormattedText? _formattedText;
    private double _moveProgress;
    private DispatcherTimer? _moveTimer;
    private SKPath? _skPath;
    private float _totalLength;

    public AnimatedSvgTextControl(AnimateInfo info)
    {
        _text = info.Text;
        _pathData = info.PathData;
        _moveDuration = info.MoveDuration;
        _moveRepeatCount = info.MoveRepeatCount;
        _fromColor = info.FromColor;
        _toColor = info.ToColor;
        _colorDuration = info.ColorDuration;
        _colorRepeatCount = info.ColorRepeatCount;

        if (!string.IsNullOrEmpty(info.PathStroke))
        {
            var c = ParseColor(info.PathStroke);
            if (c is not null)
            {
                _pathStrokeBrush = new SolidColorBrush(c.Value);
            }
        }

        _pathStrokeThickness = info.PathStrokeWidth;
        if (!string.IsNullOrEmpty(info.PathFill) && info.PathFill != "none")
        {
            var fc = ParseColor(info.PathFill);
            if (fc is not null)
            {
                _pathFillBrush = new SolidColorBrush(fc.Value);
            }
        }
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();

        if (!string.IsNullOrEmpty(_pathData))
        {
            try
            {
                _avaloniaPathGeo = PathGeometry.Parse(_pathData);
            }
            catch
            {
            }

            try
            {
                _skPath = SKPath.ParseSvgPathData(_pathData);
                if (_skPath != null)
                {
                    using var measure = new SKPathMeasure(_skPath);
                    _totalLength = measure.Length;
                }
            }
            catch
            {
            }
        }

        if (!string.IsNullOrEmpty(_text))
        {
            _formattedText = new FormattedText(
                _text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Microsoft YaHei"),
                40,
                _currentBrush
            );
        }

        if (_skPath != null && _totalLength > 0 && _moveDuration > 0)
        {
            _moveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16.7) };
            _moveTimer.Tick += MoveTimerTick;
            _moveTimer.Start();
        }

        if (!string.IsNullOrEmpty(_fromColor) && !string.IsNullOrEmpty(_toColor))
        {
            _currentBrush = new SolidColorBrush(ParseColor(_fromColor) ?? Colors.Red);

            _colorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16.7) };
            _colorTimer.Tick += ColorTimerTick;
            _colorTimer.Start();
        }
    }

    private void MoveTimerTick(object? sender, EventArgs e)
    {
        _moveProgress += 0.0167 / _moveDuration;
        if (_moveProgress > 1.0)
        {
            if (string.Equals(_moveRepeatCount, "indefinite", StringComparison.OrdinalIgnoreCase))
            {
                _moveProgress = 0.0;
            }
            else
            {
                _moveProgress = 1.0;
                _moveTimer?.Stop();
            }
        }

        InvalidateVisual();
    }

    private void ColorTimerTick(object? sender, EventArgs e)
    {
        _colorProgress += 0.0167 / _colorDuration;
        if (_colorProgress > 1.0)
        {
            if (string.Equals(_colorRepeatCount, "indefinite", StringComparison.OrdinalIgnoreCase))
            {
                _colorProgress = 0.0;
            }
            else
            {
                _colorProgress = 1.0;
                _colorTimer?.Stop();
            }
        }

        if (!string.IsNullOrEmpty(_fromColor) && !string.IsNullOrEmpty(_toColor))
        {
            var c1 = ParseColor(_fromColor) ?? Colors.Red;
            var c2 = ParseColor(_toColor) ?? Colors.Blue;

            var lerped = LerpColor(c1, c2, (float)_colorProgress);
            _currentBrush.Color = lerped;
        }

        if (_formattedText != null)
        {
            _formattedText = new FormattedText(
                _text ?? "SVG",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Microsoft YaHei"),
                40,
                _currentBrush
            );
        }

        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (_avaloniaPathGeo != null)
        {
            var pen = new Pen(Brushes.Gray, 2);
            context.DrawGeometry(null, pen, _avaloniaPathGeo);
        }

        if (_formattedText == null || _skPath == null || _totalLength <= 0)
        {
            return;
        }

        var distance = (float)(_moveProgress * _totalLength);

        using var measure = new SKPathMeasure(_skPath);
        SKPoint position = default;
        SKPoint tangent = default;

        var currentLength = 0f;
        var foundPos = false;
        do
        {
            var len = measure.Length;
            if (distance <= currentLength + len)
            {
                var distInThisContour = distance - currentLength;
                foundPos = measure.GetPositionAndTangent(distInThisContour, out position, out tangent);
                break;
            }

            currentLength += len;
        } while (measure.NextContour());

        if (!foundPos)
        {
            return;
        }

        var avaloniaPoint = new Point(position.X, position.Y);

        var offsetY = _formattedText.Height / 2;
        var correctedPoint = new Point(avaloniaPoint.X, avaloniaPoint.Y - offsetY);

        context.DrawText(_formattedText, correctedPoint);

        if (_avaloniaPathGeo != null)
        {
            var pen = new Pen(_pathStrokeBrush ?? Brushes.Gray, _pathStrokeThickness);
            context.DrawGeometry(_pathFillBrush, pen, _avaloniaPathGeo);
        }
    }

    private Color? ParseColor(string colorStr)
    {
        try
        {
            if (Color.TryParse(colorStr, out var c))
            {
                return c;
            }

            return (Color)new ColorConverter().ConvertFromString(colorStr)!;
        }
        catch
        {
            return null;
        }
    }

    private static Color LerpColor(Color c1, Color c2, float t)
    {
        var a = (byte)(c1.A + (c2.A - c1.A) * t);
        var r = (byte)(c1.R + (c2.R - c1.R) * t);
        var g = (byte)(c1.G + (c2.G - c1.G) * t);
        var b = (byte)(c1.B + (c2.B - c1.B) * t);
        return Color.FromArgb(a, r, g, b);
    }
}