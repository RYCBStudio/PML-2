using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace MEFrpLauncherX.Console;

public class AnsiColorizingTransformer : DocumentColorizingTransformer
{
    private static readonly Regex AnsiRegex = new(
        @"\x1B\[([0-9;]*)m",
        RegexOptions.Compiled
    );

    protected override void ColorizeLine(DocumentLine line)
    {
        try
        {
            var text = CurrentContext.Document.GetText(line);
            var matches = AnsiRegex.Matches(text);

            if (matches.Count == 0)
            {
                return;
            }

            var currentOffset = line.Offset;
            IBrush currentColor = Brushes.White;
            var ansiPositions = new List<(int start, int end, string code)>();

            // 收集所有ANSI代码的位置
            foreach (Match match in matches)
            {
                if (match.Success)
                {
                    ansiPositions.Add((
                        line.Offset + match.Index,
                        line.Offset + match.Index + match.Length,
                        match.Groups[1].Value
                    ));
                }
            }

            // 处理每个文本段
            for (var i = 0; i < ansiPositions.Count; i++)
            {
                var (ansiStart, ansiEnd, ansiCode) = ansiPositions[i];

                // 处理ANSI代码之前的文本
                if (ansiStart > currentOffset)
                {
                    ApplyColor(currentOffset, ansiStart, currentColor);
                }

                // 更新当前颜色
                (currentColor, var currentFontWeight) = GetBrushFromAnsiCode(ansiCode, currentColor);
                ChangeLinePart(ansiStart, ansiEnd, element =>
                {
                    element.TextRunProperties.SetForegroundBrush(Brushes.Transparent);
                    element.TextRunProperties.SetFontRenderingEmSize(0.01);
                });
                // 跳过ANSI代码本身
                currentOffset = ansiEnd;

                // 如果是最后一个ANSI代码，处理之后的文本
                if (i == ansiPositions.Count - 1 && ansiEnd < line.EndOffset)
                {
                    ApplyColor(ansiEnd, line.EndOffset, currentColor, currentFontWeight);
                }
            }

            // 如果没有ANSI代码，处理整行
            if (ansiPositions.Count == 0 && line.Length > 0)
            {
                ApplyColor(line.Offset, line.EndOffset, Brushes.White);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Colorizing error: {ex.Message}");
        }
    }

    private void ApplyColor(int startOffset, int endOffset, IBrush brush, FontWeight weight = FontWeight.Normal)
    {
        if (startOffset < endOffset)
        {
            ChangeLinePart(startOffset, endOffset, element =>
            {
                element.TextRunProperties.SetForegroundBrush(brush);
                element.TextRunProperties.SetTypeface(new Typeface(element.TextRunProperties.Typeface.FontFamily,
                    element.TextRunProperties.Typeface.Style, weight));
            });
        }
    }

    private (IBrush, FontWeight) GetBrushFromAnsiCode(string ansiCode, IBrush currentColor)
    {
        if (string.IsNullOrEmpty(ansiCode))
        {
            return (currentColor, FontWeight.Normal);
        }

        var codes = ansiCode.Split(';');
        var foreground = currentColor;
        var isBold = false;

        foreach (var code in codes)
        {
            if (int.TryParse(code, out var codeValue))
            {
                switch (codeValue)
                {
                    case 0: // Reset
                        foreground = Brushes.White;
                        break;
                    case 1: // Bold - 我们暂时忽略粗体，专注于颜色
                        isBold = true;
                        break;
                    case 30: foreground = Brushes.Black; break;
                    case 31: foreground = Brushes.OrangeRed; break;
                    case 32: foreground = Brushes.GreenYellow; break;
                    case 33: foreground = Brushes.Yellow; break;
                    case 34: foreground = Brushes.DodgerBlue; break;
                    case 35: foreground = Brushes.Magenta; break;
                    case 36: foreground = Brushes.Cyan; break;
                    case 37:
                    case 39: foreground = Brushes.White; break;
                    case 90: foreground = Brushes.DarkGray; break;
                    case 91: foreground = Brushes.LightCoral; break;
                    case 92: foreground = Brushes.LightGreen; break;
                    case 93: foreground = Brushes.LightYellow; break;
                    case 94: foreground = Brushes.LightBlue; break;
                    case 95: foreground = Brushes.LightPink; break;
                    case 96: foreground = Brushes.LightCyan; break;
                    case 97: foreground = Brushes.White; break;
                }
            }
        }

        return (foreground, isBold ? FontWeight.Bold : FontWeight.Normal);
    }
}

public class PreprocessedAnsiColorizer : DocumentColorizingTransformer
{
    private readonly Dictionary<int, IBrush> _colorMap = new();

    public void PreprocessText(string text, int insertionOffset)
    {
        var regex = new Regex(@"\x1B\[([0-9;]*)m");
        var matches = regex.Matches(text);

        IBrush currentColor = Brushes.White;
        var cleanLength = 0;
        var ansiLength = 0;

        foreach (Match match in matches)
        {
            // 计算净文本长度（去除ANSI代码）
            cleanLength += match.Index - ansiLength;
            ansiLength = match.Index + match.Length;

            // 更新颜色
            currentColor = GetBrushFromAnsiCode(match.Groups[1].Value, currentColor);

            // 记录颜色信息
            _colorMap[insertionOffset + cleanLength] = currentColor;
        }
    }

    public void ClearColorMap() => _colorMap.Clear();

    protected override void ColorizeLine(DocumentLine line)
    {
        try
        {
            var start = line.Offset;
            var end = line.EndOffset;
            IBrush currentColor = Brushes.White;

            for (var i = start; i < end; i++)
            {
                if (_colorMap.TryGetValue(i, out var newColor))
                {
                    currentColor = newColor;
                }

                ChangeLinePart(i, i + 1, element =>
                {
                    element.TextRunProperties.SetForegroundBrush(currentColor);
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Colorizing error: {ex.Message}");
        }
    }

    private IBrush GetBrushFromAnsiCode(string ansiCode, IBrush currentColor)
    {
        if (string.IsNullOrEmpty(ansiCode))
        {
            return currentColor;
        }

        var codes = ansiCode.Split(';');
        var foreground = currentColor;

        foreach (var code in codes)
        {
            if (int.TryParse(code, out var codeValue))
            {
                switch (codeValue)
                {
                    case 0: // Reset
                        foreground = Brushes.White;
                        break;
                    case 1: // Bold - 我们暂时忽略粗体，专注于颜色
                        break;
                    case 30: foreground = Brushes.Black; break;
                    case 31: foreground = Brushes.Red; break;
                    case 32: foreground = Brushes.Green; break;
                    case 33: foreground = Brushes.Yellow; break;
                    case 34: foreground = Brushes.Blue; break;
                    case 35: foreground = Brushes.Magenta; break;
                    case 36: foreground = Brushes.Cyan; break;
                    case 37: foreground = Brushes.White; break;
                    case 39: foreground = Brushes.White; break;
                    case 90: foreground = Brushes.DarkGray; break;
                    case 91: foreground = Brushes.LightCoral; break;
                    case 92: foreground = Brushes.LightGreen; break;
                    case 93: foreground = Brushes.LightYellow; break;
                    case 94: foreground = Brushes.LightBlue; break;
                    case 95: foreground = Brushes.LightPink; break;
                    case 96: foreground = Brushes.LightCyan; break;
                    case 97: foreground = Brushes.White; break;
                }
            }
        }

        return foreground;
    }
}

public class AnsiTextProcessor
{
    public static List<ColoredTextSegment> ParseAnsiText(string text)
    {
        var segments = new List<ColoredTextSegment>();
        var regex = new Regex(@"(?<ansi>\x1B\[[0-9;]*m)|(?<text>[^\x1B]+)");
        var matches = regex.Matches(text);

        IBrush currentColor = Brushes.White;
        var currentText = new StringBuilder();

        foreach (Match match in matches)
        {
            if (match.Groups["ansi"].Success)
            {
                // 如果已经有文本，先添加
                if (currentText.Length > 0)
                {
                    segments.Add(new ColoredTextSegment
                    {
                        Text = currentText.ToString(),
                        Color = currentColor
                    });
                    currentText.Clear();
                }

                // 更新颜色
                var ansiCode = match.Groups["ansi"].Value;
                currentColor = GetBrushFromAnsiCode(ansiCode, currentColor);
            }
            else if (match.Groups["text"].Success)
            {
                currentText.Append(match.Groups["text"].Value);
            }
        }

        // 添加最后一段文本
        if (currentText.Length > 0)
        {
            segments.Add(new ColoredTextSegment
            {
                Text = currentText.ToString(),
                Color = currentColor
            });
        }

        return segments;
    }

    private static IBrush GetBrushFromAnsiCode(string ansiCode, IBrush currentColor)
    {
        if (string.IsNullOrEmpty(ansiCode))
        {
            return currentColor;
        }

        // 提取数字代码
        var codeMatch = Regex.Match(ansiCode, @"\x1B\[([0-9;]*)m");
        if (!codeMatch.Success)
        {
            return currentColor;
        }

        var codes = codeMatch.Groups[1].Value.Split(';');
        var foreground = currentColor;

        foreach (var code in codes)
        {
            if (int.TryParse(code, out var codeValue))
            {
                switch (codeValue)
                {
                    case 0: foreground = Brushes.White; break;
                    case 1: break; // 粗体忽略
                    case 30: foreground = Brushes.Black; break;
                    case 31: foreground = Brushes.Red; break;
                    case 32: foreground = Brushes.Green; break;
                    case 33: foreground = Brushes.Yellow; break;
                    case 34: foreground = Brushes.Blue; break;
                    case 35: foreground = Brushes.Magenta; break;
                    case 36: foreground = Brushes.Cyan; break;
                    case 37: foreground = Brushes.White; break;
                    case 39: foreground = Brushes.White; break;
                    case 90: foreground = Brushes.DarkGray; break;
                    case 91: foreground = Brushes.LightCoral; break;
                    case 92: foreground = Brushes.LightGreen; break;
                    case 93: foreground = Brushes.LightYellow; break;
                    case 94: foreground = Brushes.LightBlue; break;
                    case 95: foreground = Brushes.LightPink; break;
                    case 96: foreground = Brushes.LightCyan; break;
                    case 97: foreground = Brushes.White; break;
                }
            }
        }

        return foreground;
    }

    public class ColoredTextSegment
    {
        public string Text
        {
            get;
            set;
        }

        public IBrush Color
        {
            get;
            set;
        }
    }
}