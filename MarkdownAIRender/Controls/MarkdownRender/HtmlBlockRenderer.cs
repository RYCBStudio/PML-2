using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using MarkdownAIRender.i18n;
using Markdig.Extensions.Alerts;
using Markdig.Syntax;
using Inline = Avalonia.Controls.Documents.Inline;

namespace MarkdownAIRender.Controls.MarkdownRender;

/// <summary>
///     HTML 块渲染器，将 HTML 标签转换为 Avalonia 控件。
///     支持块级标签（h1-h6、p、div、hr、br、ul/ol、blockquote、pre 等）
///     以及内联标签（b/strong、i/em、u/ins、s/del、code、a、span+style 等，且支持嵌套）。
/// </summary>
public static partial class HtmlBlockRenderer
{
    // 匹配 [!TYPE] 格式，支持可选的换行和后续内容
    private static readonly Regex AlertRegex = MyRegex();

    // 块级标签
    private static readonly Regex BlockTagRegex = new(
        @"<(h[1-6]|p|div|hr|br|section|article|blockquote|pre|ul|ol)(\s[^<>]*?)?/?>|</(h[1-6]|p|div|section|article|blockquote|pre|ul|ol)\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // 列表项
    private static readonly Regex ListItemRegex = new(@"<li(?:\s[^<>]*?)?>(.*?)</li>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    // 通用标签（含属性、自闭合）
    private static readonly Regex TagRegex =
        new(@"<(?<close>/)?\s*(?<name>[a-zA-Z][a-zA-Z0-9]*)(?<attrs>[^<>]*?)(?<self>/)?>", RegexOptions.Compiled);

    // 属性解析
    private static readonly Regex AttrRegex =
        new(@"([a-zA-Z-]+)\s*=\s*(?:""([^""]*)""|'([^']*)')", RegexOptions.Compiled);

    private static readonly HashSet<string> VoidTags =
        new(StringComparer.OrdinalIgnoreCase) { "br", "hr", "img", "input", "wbr" };

    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    #region Public Methods

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
    ///     解析 HTML 内联内容（支持嵌套标签）并追加到给定 InlineCollection
    /// </summary>
    public static void ParseHtmlInline(string html, ref InlineCollection inlines)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return;
        }

        ParseHtmlInlines(html, inlines);
    }

    /// <summary>
    ///     将内联内容按指定 HTML 标签包装（供 Markdown 内联 HTML 渲染使用）。
    ///     返回 null 表示该标签无需包装（内容保持原样）。
    /// </summary>
    public static object? WrapInlineHtmlContent(string tagName, string attributeText, IEnumerable<object> content)
    {
        var name = tagName.ToLowerInvariant();
        var attrs = ParseAttributes(attributeText ?? string.Empty);

        switch (name)
        {
            case "br":
                return new LineBreak();

            case "hr":
            case "img":
            case "input":
                return null;

            case "a":
            {
                attrs.TryGetValue("href", out var href);
                var textBlock = new SelectableTextBlock { TextWrapping = TextWrapping.Wrap };
                AddContentToInlines(content, textBlock.Inlines!);
                return new MarkdownLink { Url = href, LinkContent = textBlock };
            }

            case "code" or "kbd" or "samp" or "tt":
            {
                var codeBlock = new SelectableTextBlock
                {
                    FontFamily = new FontFamily("Consolas"), Classes = { "MdCode" }
                };
                AddContentToInlines(content, codeBlock.Inlines!);
                return new Border { Classes = { "MdCodeBorder" }, Child = codeBlock };
            }
        }

        var span = new Span();
        switch (name)
        {
            case "b" or "strong":
                span.FontWeight = FontWeight.Bold;
                break;
            case "i" or "em" or "cite" or "dfn" or "var":
                span.FontStyle = FontStyle.Italic;
                break;
            case "u" or "ins":
                span.TextDecorations = TextDecorations.Underline;
                break;
            case "s" or "del" or "strike":
                span.TextDecorations = TextDecorations.Strikethrough;
                break;
            case "sub" or "sup" or "mark" or "small":
                // 无直接等价样式，仅保留内容
                break;
            case "span":
                attrs.TryGetValue("style", out var style);
                ApplyCssStyle(span, style ?? string.Empty);
                break;
            default:
                // 未知标签：不包装
                return null;
        }

        AddContentToInlines(content, span.Inlines);
        return span;
    }

    #endregion

    #region Alert Parsing

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

        var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
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
            "tip" => MdStrings.AlertTip,
            "note" => MdStrings.AlertNote,
            "warning" => MdStrings.AlertWarning,
            "caution" => MdStrings.AlertCaution,
            _ => MdStrings.AlertInfo
        };
    }

    #endregion

    #region Block Level Parsing

    /// <summary>
    ///     将 HTML 内容解析为 Avalonia 控件（块级入口）
    /// </summary>
    private static Control ParseHtmlToControls(string html)
    {
        var container = new StackPanel
        {
            Orientation = Orientation.Vertical, Spacing = 4, Margin = new Thickness(0, 5, 0, 5)
        };

        ParseHtmlBlocks(html, container);

        if (container.Children.Count == 0)
        {
            container.Children.Add(new SelectableTextBlock { Text = html, TextWrapping = TextWrapping.Wrap });
        }

        return container;
    }

    /// <summary>
    ///     块级解析：按块级标签切分并递归处理
    /// </summary>
    private static void ParseHtmlBlocks(string html, StackPanel container)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return;
        }

        var pos = 0;
        while (pos < html.Length)
        {
            var match = BlockTagRegex.Match(html, pos);
            if (!match.Success)
            {
                AddInlineParagraph(html[pos..], container);
                break;
            }

            if (match.Index > pos)
            {
                AddInlineParagraph(html[pos..match.Index], container);
            }

            var openName = match.Groups[1].Value.ToLowerInvariant();
            var closeName = match.Groups[3].Value.ToLowerInvariant();
            var tagEnd = match.Index + match.Length;

            // 孤立的闭合标签：跳过
            if (string.IsNullOrEmpty(openName))
            {
                pos = tagEnd;
                continue;
            }

            if (openName == "br")
            {
                pos = tagEnd;
                continue;
            }

            if (openName == "hr")
            {
                container.Children.Add(new Border
                {
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Margin = new Thickness(0, 5, 0, 5)
                });
                pos = tagEnd;
                continue;
            }

            if (match.Value.EndsWith("/>"))
            {
                pos = tagEnd;
                continue;
            }

            if (!FindTagEnd(html, tagEnd, openName, out var content, out var endPos))
            {
                // 未闭合标签：剩余部分按内联内容处理
                AddInlineParagraph(html[tagEnd..], container);
                break;
            }

            switch (openName)
            {
                case "h1":
                case "h2":
                case "h3":
                case "h4":
                case "h5":
                case "h6":
                    container.Children.Add(CreateHtmlHeading(openName[1] - '0', content));
                    break;

                case "p":
                {
                    var textBlock = new SelectableTextBlock
                    {
                        TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2)
                    };
                    ParseHtmlInlines(content, textBlock.Inlines!);
                    container.Children.Add(textBlock);
                    break;
                }

                case "pre":
                    container.Children.Add(CreatePreBlock(content));
                    break;

                case "ul" or "ol":
                    container.Children.Add(CreateHtmlList(content, openName == "ol"));
                    break;

                case "blockquote":
                {
                    var innerPanel = new StackPanel { Orientation = Orientation.Vertical };
                    ParseHtmlBlocks(content, innerPanel);
                    var border = new Border { Child = innerPanel };
                    border.AddMdClass(MarkdownClassConst.MdQuoteBorder);
                    container.Children.Add(border);
                    break;
                }

                default:
                    // div / section / article 等容器标签：递归处理
                    ParseHtmlBlocks(content, container);
                    break;
            }

            pos = endPos;
        }
    }

    /// <summary>
    ///     非块级标签包裹的文本按一个段落渲染
    /// </summary>
    private static void AddInlineParagraph(string html, StackPanel container)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return;
        }

        var textBlock = new SelectableTextBlock
        {
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2)
        };
        ParseHtmlInlines(html, textBlock.Inlines!);

        if (textBlock.Inlines.Count > 0)
        {
            container.Children.Add(textBlock);
        }
    }

    /// <summary>
    ///     渲染 HTML 标题（h1-h6），复用 Markdown 标题样式类
    /// </summary>
    private static Control CreateHtmlHeading(int level, string content)
    {
        var mdClassName = level is >= 1 and <= 6 ? $"MdH{level}" : "MdHn";
        var textBlock = new SelectableTextBlock { TextWrapping = TextWrapping.WrapWithOverflow };
        textBlock.AddMdClass(mdClassName);
        ParseHtmlInlines(content, textBlock.Inlines!);
        return textBlock;
    }

    /// <summary>
    ///     渲染 pre 块（等宽字体）
    /// </summary>
    private static Control CreatePreBlock(string content)
    {
        // 去掉内部标签并解码实体，保留换行
        var text = WebUtility.HtmlDecode(Regex.Replace(content, @"<[^>]+>", ""));
        return new Border
        {
            Classes = { "MdCodeBorder" },
            Padding = new Thickness(6),
            Margin = new Thickness(0, 4, 0, 4),
            Child = new SelectableTextBlock
            {
                Text = text, FontFamily = new FontFamily("Consolas"), TextWrapping = TextWrapping.Wrap
            }
        };
    }

    /// <summary>
    ///     渲染 HTML 列表
    /// </summary>
    private static Control CreateHtmlList(string html, bool ordered)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Vertical, Spacing = 2, Margin = new Thickness(8, 2, 0, 2)
        };

        var index = 1;
        foreach (Match li in ListItemRegex.Matches(html))
        {
            var itemPanel = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 5 };

            itemPanel.Children.Add(new SelectableTextBlock
            {
                Text = ordered ? $"{index++}." : "•", FontWeight = FontWeight.Bold
            });

            var contentBlock = new SelectableTextBlock { TextWrapping = TextWrapping.Wrap };
            ParseHtmlInlines(li.Groups[1].Value, contentBlock.Inlines!);
            itemPanel.Children.Add(contentBlock);

            panel.Children.Add(itemPanel);
        }

        return panel;
    }

    #endregion

    #region Inline Level Parsing

    /// <summary>
    ///     递归解析内联 HTML（支持任意嵌套）
    /// </summary>
    private static void ParseHtmlInlines(string html, InlineCollection inlines)
    {
        if (string.IsNullOrEmpty(html))
        {
            return;
        }

        var pos = 0;
        while (pos < html.Length)
        {
            var match = TagRegex.Match(html, pos);
            if (!match.Success)
            {
                AppendTextRun(html, pos, html.Length - pos, inlines);
                break;
            }

            AppendTextRun(html, pos, match.Index - pos, inlines);

            var name = match.Groups["name"].Value.ToLowerInvariant();
            var isClose = match.Groups["close"].Success;
            var selfClosing = match.Groups["self"].Success;
            var attrs = match.Groups["attrs"].Value;
            pos = match.Index + match.Length;

            // 孤立闭合标签：忽略
            if (isClose)
            {
                continue;
            }

            // 空元素标签
            if (VoidTags.Contains(name))
            {
                if (name is "br" or "hr")
                {
                    inlines.Add(new LineBreak());
                }

                continue;
            }

            if (selfClosing)
            {
                continue;
            }

            // 找到匹配的闭合标签（支持同名嵌套）
            string inner;
            if (FindTagEnd(html, pos, name, out var foundInner, out var endPos))
            {
                inner = foundInner;
            }
            else
            {
                // 未闭合：把剩余内容作为该标签的内容
                inner = html[pos..];
                endPos = html.Length;
            }

            var content = new InlineCollection();
            ParseHtmlInlines(inner, content);

            var items = content.Cast<object>().ToList();
            var wrapped = WrapInlineHtmlContent(name, attrs, items);
            switch (wrapped)
            {
                case Inline wrappedInline:
                    inlines.Add(wrappedInline);
                    break;
                case Control wrappedControl:
                    inlines.Add(wrappedControl);
                    break;
                default:
                    // 不包装的标签：内容原样追加
                    foreach (var item in items)
                    {
                        if (item is Inline inline)
                        {
                            inlines.Add(inline);
                        }
                        else if (item is Control control)
                        {
                            inlines.Add(control);
                        }
                    }

                    break;
            }

            pos = endPos;
        }
    }

    /// <summary>
    ///     追加一段文本（解码 HTML 实体，折叠空白）
    /// </summary>
    private static void AppendTextRun(string html, int index, int length, InlineCollection inlines)
    {
        if (length <= 0)
        {
            return;
        }

        var text = WebUtility.HtmlDecode(WhitespaceRegex.Replace(html.Substring(index, length), " "));
        if (!string.IsNullOrWhiteSpace(text))
        {
            inlines.Add(new Run(text));
        }
    }

    /// <summary>
    ///     从 startPos 开始查找与 tagName 匹配的闭合标签（处理同名嵌套）
    /// </summary>
    private static bool FindTagEnd(string html, int startPos, string tagName, out string inner, out int endPos)
    {
        inner = string.Empty;
        endPos = html.Length;

        var depth = 1;
        var pos = startPos;
        while (pos < html.Length)
        {
            var match = TagRegex.Match(html, pos);
            if (!match.Success)
            {
                return false;
            }

            if (string.Equals(match.Groups["name"].Value, tagName, StringComparison.OrdinalIgnoreCase))
            {
                if (match.Groups["close"].Success)
                {
                    depth--;
                    if (depth == 0)
                    {
                        inner = html[startPos..match.Index];
                        endPos = match.Index + match.Length;
                        return true;
                    }
                }
                else if (!match.Groups["self"].Success && !VoidTags.Contains(tagName))
                {
                    depth++;
                }
            }

            pos = match.Index + match.Length;
        }

        return false;
    }

    /// <summary>
    ///     将内容项（Inline / Control）追加到 InlineCollection
    /// </summary>
    private static void AddContentToInlines(IEnumerable<object> content, InlineCollection inlines)
    {
        foreach (var item in content)
        {
            switch (item)
            {
                case Inline inline:
                    inlines.Add(inline);
                    break;
                case Control control:
                    inlines.Add(control);
                    break;
            }
        }
    }

    #endregion

    #region CSS Style Parsing

    /// <summary>
    ///     将 CSS style 字符串应用到 Span
    /// </summary>
    private static void ApplyCssStyle(Span span, string style)
    {
        if (string.IsNullOrWhiteSpace(style))
        {
            return;
        }

        foreach (var styleItem in style.Split(';'))
        {
            var parts = styleItem.Split(':', 2);
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
                    else if (propertyValue.ToLower().Contains("line-through"))
                    {
                        span.TextDecorations = TextDecorations.Strikethrough;
                    }

                    break;

                case "background" or "background-color":
                    if (TryParseColor(propertyValue, out var background))
                    {
                        span.Background = new SolidColorBrush(background);
                    }

                    break;
            }
        }
    }

    /// <summary>
    ///     解析标签属性
    /// </summary>
    private static Dictionary<string, string> ParseAttributes(string attributeText)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(attributeText))
        {
            return result;
        }

        foreach (Match match in AttrRegex.Matches(attributeText))
        {
            var key = match.Groups[1].Value.ToLowerInvariant();
            var value = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;
            result[key] = WebUtility.HtmlDecode(value);
        }

        return result;
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

    #endregion

    [GeneratedRegex(@"^\[!(tip|caution|warning|note)\]\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled,
        "zh-CN")]
    private static partial Regex MyRegex();
}
