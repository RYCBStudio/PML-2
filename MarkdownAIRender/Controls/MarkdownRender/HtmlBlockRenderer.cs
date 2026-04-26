using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using Markdig.Extensions.Alerts;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Inline = Avalonia.Controls.Documents.Inline;

namespace MarkdownAIRender.Controls.MarkdownRender;

/// <summary>
///     HTML 块渲染器，将 HTML 标签转换为 Avalonia 控件
/// </summary>
public static partial class HtmlBlockRenderer
{
    // 匹配 [!TYPE] 格式，支持可选的换行和后续内容
    private static readonly Regex AlertRegex = MyRegex();

    /// <summary>
    ///     尝试将 HTML 块渲染为警告框或其他控件
    /// </summary>
    public static Control? RenderHtmlBlock(HtmlBlock htmlBlock)
    {
        var htmlContent = htmlBlock.Lines.ToString().Trim();

        // 检查是否是 GitHub 风格的警告框
        if (TryParseAlert(htmlContent, out var alertType, out var alertContent))
        {
            return CreateAlertBox(alertType, alertContent);
        }

        // 处理其他 HTML 标签
        return ParseHtmlToControls(htmlContent);
    }

    public static Control? RenderAlertBlock(AlertBlock alertBlock)
    {
        return TryParseAlert(alertBlock.Kind.Text.Trim(), out var alertType, out var alertContent)
            ? CreateAlertBox(alertType, alertContent)
            :
            // 处理其他 HTML 标签
            ParseHtmlToControls(alertBlock.Kind.Text.Trim());
    }

    /// <summary>
    ///     尝试解析 GitHub 风格的警告框
    ///     支持格式：
    ///     > [!NOTE]
    ///     > 内容...
    /// </summary>
    private static bool TryParseAlert(string content, out string alertType, out string alertContent)
    {
        alertType = string.Empty;
        alertContent = string.Empty;

        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
        {
            return false;
        }

        // 检查第一行是否是 [!TYPE] 格式
        var firstLine = lines[0].Trim();
        // 移除可能的引用标记 ">"
        firstLine = firstLine.StartsWith(">") ? firstLine.Substring(1).Trim() : firstLine;

        var match = AlertRegex.Match(firstLine);
        if (!match.Success)
        {
            return false;
        }

        alertType = match.Groups[1].Value.ToLower();

        // 收集后续行作为内容
        var contentLines = new StringBuilder();
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            // 移除引用标记
            if (line.StartsWith(">"))
            {
                line = line.Substring(1).Trim();
            }

            if (contentLines.Length > 0)
            {
                contentLines.AppendLine();
            }

            contentLines.Append(line);
        }

        alertContent = contentLines.ToString().Trim();
        return !string.IsNullOrWhiteSpace(alertContent);
    }

    /// <summary>
    ///     创建警告框
    /// </summary>
    private static Control CreateAlertBox(string type, string content)
    {
        var infoCallout = new InfoBar
        {
            IsOpen = true,
            IsClosable = false,
            Severity = GetInfoBarSeverity(type),
            Title = GetAlertTitle(type),
            Message = content,
            Margin = new Thickness(0, 5, 0, 5)
        };

        return infoCallout;
    }

    /// <summary>
    ///     获取 InfoBar 的严重性级别
    /// </summary>
    private static InfoBarSeverity GetInfoBarSeverity(string type)
    {
        return type.ToLower() switch
        {
            "tip" => InfoBarSeverity.Success,
            "note" => InfoBarSeverity.Informational,
            "warning" or "important" => InfoBarSeverity.Warning,
            "caution" => InfoBarSeverity.Error,
            _ => InfoBarSeverity.Informational
        };
    }

    /// <summary>
    ///     获取警告框标题
    /// </summary>
    private static string GetAlertTitle(string type)
    {
        return type.ToLower() switch
        {
            "tip" => "提示",
            "note" => "注意",
            "warning" => "警告",
            "caution" => "重要",
            _ => "信息"
        };
    }

    /// <summary>
    ///     将 HTML 内容解析为 Avalonia 控件
    /// </summary>
    private static Control ParseHtmlToControls(string html)
    {
        var container = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(0, 5, 0, 5)
        };

        var inline = new InlineCollection();

        ParseHtmlInline(html, ref inline);
        // 简单的 HTML 解析 - 处理常见的块级元素
        var textBlock = new SelectableTextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Inlines = inline
        };
        container.Children.Add(textBlock);

        return container;
    }

    /// <summary>
    ///     解析 HTML 内联内容为 Run/Span 等
    /// </summary>
    public static void ParseHtmlInline(string html, ref InlineCollection inlines)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return;
        }

        // 移除块级标签，保留内联标签
        var cleaned = html
            .Replace("<div>", "").Replace("</div>", "\n")
            .Replace("<p>", "").Replace("</p>", "\n\n")
            .Replace("<br>", "\n").Replace("<br/>", "\n").Replace("<br />", "\n");

        // 处理带样式的 span 标签
        cleaned = ProcessSpanTags(cleaned, ref inlines);

        // 处理粗体 <strong> 或 <b>
        cleaned = ProcessBoldTags(cleaned, ref inlines);
    }

    public static Inline ParseHtmlInline(HtmlInline htmlInline, string endTag)
    {
        var fullSpan = htmlInline.Tag + htmlInline.NextSibling + endTag;
        var inlines = new InlineCollection();
        ProcessSpanTags(fullSpan, ref inlines);
        ProcessBoldTags(fullSpan, ref inlines);
        return inlines.Count > 0 ? inlines[0] : new Run();
    }

    /// <summary>
    ///     处理带样式的 span 标签
    /// </summary>
    private static string ProcessSpanTags(string html, ref InlineCollection inlines)
    {
        // 匹配 <span style="...">content</span>
        var spanPattern = "<span\\s+style=\"([^\"]*)\"[^>]*>(.*?)</span>";
        var matches = Regex.Matches(html, spanPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

        var lastIndex = 0;
        foreach (Match match in matches)
        {
            // 添加普通文本
            if (match.Index > lastIndex)
            {
                var text = html.Substring(lastIndex, match.Index - lastIndex);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    inlines.Add(new Run(text));
                }
            }

            // 解析样式
            var styleAttr = match.Groups[1].Value;
            var content = match.Groups[2].Value;

            var span = CreateStyledSpan(styleAttr, content);
            inlines.Add(span);

            lastIndex = match.Index + match.Length;
        }

        // 添加剩余文本
        if (lastIndex < html.Length)
        {
            var remainingText = html.Substring(lastIndex);
            if (!string.IsNullOrWhiteSpace(remainingText))
            {
                inlines.Add(new Run(remainingText));
            }
        }

        return string.Empty;
    }

    /// <summary>
    ///     根据样式字符串创建带样式的 Span
    /// </summary>
    private static Span CreateStyledSpan(string style, string content)
    {
        var span = new Span();

        // 解析样式属性
        var styles = style.Split(';');
        foreach (var styleItem in styles)
        {
            var parts = styleItem.Split(':');
            if (parts.Length != 2)
            {
                continue;
            }

            var propertyName = parts[0].Trim().ToLower();
            var propertyValue = parts[1].Trim();

            switch (propertyName)
            {
                case "font-size":
                    if (TryParseFontSize(propertyValue, out var fontSize))
                    {
                        span.FontSize = fontSize;
                    }

                    break;

                case "font-weight":
                    span.FontWeight = ParseFontWeight(propertyValue);
                    break;

                case "color":
                    if (TryParseColor(propertyValue, out var color))
                    {
                        span.Foreground = new SolidColorBrush(color);
                    }

                    break;

                case "font-style":
                    span.FontStyle = propertyValue.ToLower() == "italic" ? FontStyle.Italic : FontStyle.Normal;
                    break;

                case "text-decoration":
                    if (propertyValue.ToLower().Contains("underline"))
                    {
                        span.TextDecorations = TextDecorations.Underline;
                    }

                    break;
            }
        }

        span.Inlines.Add(new Run(content));
        return span;
    }

    /// <summary>
    ///     尝试解析字体大小
    /// </summary>
    private static bool TryParseFontSize(string value, out double size)
    {
        size = 14; // 默认大小

        // 移除 px 单位
        if (value.EndsWith("px", StringComparison.OrdinalIgnoreCase))
        {
            value = value[..^2];
        }

        return double.TryParse(value, out size);
    }

    /// <summary>
    ///     解析字体粗细
    /// </summary>
    private static FontWeight ParseFontWeight(string value)
    {
        return value.ToLower() switch
        {
            "bold" or "700" => FontWeight.Bold,
            "normal" or "400" => FontWeight.Normal,
            "light" or "300" => FontWeight.Light,
            "medium" or "500" => FontWeight.Medium,
            "semibold" or "600" => FontWeight.SemiBold,
            "extrabold" or "800" => FontWeight.ExtraBold,
            "black" or "900" => FontWeight.Black,
            _ => FontWeight.Normal
        };
    }

    /// <summary>
    ///     尝试解析颜色
    /// </summary>
    private static bool TryParseColor(string value, out Color color)
    {
        color = Colors.Black;

        try
        {
            // 支持命名颜色（如 gold, red, blue）
            if (Color.TryParse(value, out var parsedColor))
            {
                color = parsedColor;
                return true;
            }

            // 支持十六进制颜色
            if (value.StartsWith('#'))
            {
                value = value.TrimStart('#');
                if (value.Length == 6 || value.Length == 8)
                {
                    var r = Convert.ToByte(value.Substring(0, 2), 16);
                    var g = Convert.ToByte(value.Substring(2, 2), 16);
                    var b = Convert.ToByte(value.Substring(4, 2), 16);
                    var a = value.Length == 8 ? Convert.ToByte(value.Substring(6, 2), 16) : (byte)255;
                    color = Color.FromArgb(a, r, g, b);
                    return true;
                }
            }
        }
        catch
        {
            // 解析失败，使用默认颜色
        }

        return false;
    }

    /// <summary>
    ///     处理粗体标签
    /// </summary>
    private static string ProcessBoldTags(string html, ref InlineCollection inlines)
    {
        const string strongPattern = @"<(strong|b)>(.*?)</\1>";
        var matches = Regex.Matches(html, strongPattern, RegexOptions.IgnoreCase);

        var lastIndex = 0;
        foreach (Match match in matches)
        {
            // 添加普通文本
            if (match.Index > lastIndex)
            {
                var text = html.Substring(lastIndex, match.Index - lastIndex);
                inlines.Add(new Run(text));
            }

            // 添加粗体文本
            var boldText = match.Groups[2].Value;
            var span = new Span
            {
                FontWeight = FontWeight.Bold,
                Inlines = { new Run(boldText) }
            };
            inlines.Add(span);

            lastIndex = match.Index + match.Length;
        }

        // 添加剩余文本
        if (lastIndex < html.Length)
        {
            var remainingText = html.Substring(lastIndex);
            // 移除剩余的 HTML 标签
            remainingText = Regex.Replace(remainingText, @"<[^>]+>", "");
            if (!string.IsNullOrWhiteSpace(remainingText))
            {
                inlines.Add(new Run(remainingText));
            }
        }

        return string.Empty;
    }

    [GeneratedRegex(@"^\[!(tip|caution|warning|note)\]\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled,
        "zh-CN")]
    private static partial Regex MyRegex();
}