using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace MEFrpLauncherX.Controls;

public class TerminalEditor : TextEditor
{
    private readonly TerminalColorizer _colorizer;

    public TerminalEditor()
    {
        FontSize = 13;
        IsReadOnly = true;
        ShowLineNumbers = false;
        Options.EnableHyperlinks = true;

        // 初始化TextMate
        //var registryOptions = new RegistryOptions(ThemeName.DarkPlus);

        // 加载语法定义 - 使用新的API
        // var grammarPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "grammars", "terminal.tmLanguage.json");
        // var textMateInstallation = this.InstallTextMate(registryOptions);
        // textMateInstallation.SetGrammar(registryOptions.LoadTheme(ThemeName.DarkPlus).ToString()); // 显式加载主题
        // textMateInstallation.SetGrammarFile(grammarPath);
        // TextMate.RegisterExceptionHandler(ex =>
        // {
        //     Core.App.CurrentLogger.Error(ex);
        // });
        // 初始化颜色转换器
        _colorizer = new TerminalColorizer();
        TextArea.TextView.LineTransformers.Add(_colorizer);
    }

    public void AppendAnsiText(string text)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var segments = AnsiColorConverter.ConvertAnsiToSegments(text);
            foreach (var segment in segments)
            {
                Core.App.CurrentLogger.LogDebug($"Text: {segment.Text}, Color: {segment.Foreground}"); // 调试输出
                _colorizer.AddColoredSegment(segment);
                AppendText(segment.Text);
            }

            ScrollToEnd();
        });
    }

    public void ClearTerminal()
    {
        Dispatcher.UIThread.Post(() =>
        {
            Document.Text = string.Empty;
            _colorizer.ClearSegments();
        });
    }
}

public class TerminalColorizer : DocumentColorizingTransformer
{
    private readonly object _lock = new();
    private readonly List<ColoredSegment> _segments = [];

    public void AddColoredSegment(ColoredSegment segment)
    {
        lock (_lock)
        {
            _segments.Add(segment);
        }
    }

    public void ClearSegments()
    {
        lock (_lock)
        {
            _segments.Clear();
        }
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        lock (_lock)
        {
            if (_segments.Count == 0)
            {
                return;
            }

            var lineStart = line.Offset;
            var lineEnd = lineStart + line.Length;

            foreach (var segment in _segments)
            {
                var segmentStart = lineStart;
                var segmentEnd = lineEnd;

                if (segmentStart >= lineEnd || segmentEnd <= lineStart)
                {
                    continue;
                }

                if (segmentStart < lineStart)
                {
                    segmentStart = lineStart;
                }

                if (segmentEnd > lineEnd)
                {
                    segmentEnd = lineEnd;
                }

                ChangeLinePart(
                    segmentStart,
                    segmentEnd,
                    visualLine =>
                    {
                        visualLine.TextRunProperties.SetForegroundBrush(segment.Foreground);
                    });
            }
        }
    }
}

public static class AnsiColorConverter
{
    private static readonly Dictionary<int, IBrush> _ansiColors = new()
    {
        { 30, Brushes.Black },
        { 31, Brushes.Red },
        { 32, Brushes.Green },
        { 33, Brushes.Yellow },
        { 34, Brushes.Blue },
        { 35, Brushes.Magenta },
        { 36, Brushes.Cyan },
        { 37, Brushes.White },
        { 90, Brushes.Gray },
        { 91, Brushes.OrangeRed },
        { 92, Brushes.LimeGreen },
        { 93, Brushes.Gold },
        { 94, Brushes.DodgerBlue },
        { 95, Brushes.Violet },
        { 96, Brushes.LightSkyBlue },
        { 97, Brushes.White }
    };

    public static List<ColoredSegment> ConvertAnsiToSegments(string text)
    {
        var segments = new List<ColoredSegment>();
        IBrush currentColor = Brushes.White;
        var pos = 0;

        while (pos < text.Length)
        {
            var escIndex = text.IndexOf('\x1b', pos);
            if (escIndex < 0)
            {
                if (pos < text.Length)
                {
                    segments.Add(new ColoredSegment(text[pos..], currentColor));
                }

                break;
            }

            if (escIndex > pos)
            {
                segments.Add(new ColoredSegment(text.Substring(pos, escIndex - pos), currentColor));
            }

            var mIndex = text.IndexOf('m', escIndex);
            if (mIndex < 0)
            {
                break;
            }

            var escSeq = text.Substring(escIndex, mIndex - escIndex + 1);
            currentColor = ParseAnsiColor(escSeq);
            pos = mIndex + 1;
        }

        return segments;
    }

    private static IBrush ParseAnsiColor(string escSeq)
    {
        var codes = escSeq.Trim('\x1b', '[', 'm')
            .Split(';')
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(int.Parse);

        foreach (var code in codes)
        {
            if (_ansiColors.TryGetValue(code, out var color))
            {
                return color;
            }
        }

        return Brushes.White;
    }
}

public record ColoredSegment(string Text, IBrush Foreground);