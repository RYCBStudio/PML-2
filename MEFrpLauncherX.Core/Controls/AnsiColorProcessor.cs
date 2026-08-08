using System.Text.RegularExpressions;
using Avalonia.Media;

namespace MEFrpLauncherX.Core.Controls;

public static class AnsiColorProcessor
{
    public static readonly Regex AnsiColorRegex = new(
        @"\x1B\[([0-9]{1,2}(;[0-9]{1,2})?)?[mK]",
        RegexOptions.Compiled
    );

    public static IBrush GetBrushFromAnsiCode(string ansiCode)
    {
        if (ansiCode.Contains("31"))
        {
            return Brushes.Red; // 红色
        }

        if (ansiCode.Contains("32"))
        {
            return Brushes.Green; // 绿色
        }

        if (ansiCode.Contains("33"))
        {
            return Brushes.Yellow; // 黄色
        }

        if (ansiCode.Contains("34"))
        {
            return Brushes.Blue; // 蓝色
        }

        if (ansiCode.Contains("35"))
        {
            return Brushes.Magenta; // 洋红色
        }

        if (ansiCode.Contains("36"))
        {
            return Brushes.Cyan; // 青色
        }

        if (ansiCode.Contains("37"))
        {
            return Brushes.White; // 白色
        }

        if (ansiCode.Contains("90"))
        {
            return Brushes.Gray; // 亮黑色
        }

        if (ansiCode.Contains("91"))
        {
            return Brushes.Red; // 亮红色
        }

        if (ansiCode.Contains("92"))
        {
            return Brushes.LightGreen; // 亮绿色
        }

        if (ansiCode.Contains("93"))
        {
            return Brushes.LightYellow; // 亮黄色
        }

        if (ansiCode.Contains("94"))
        {
            return Brushes.LightBlue; // 亮蓝色
        }

        if (ansiCode.Contains("95"))
        {
            return Brushes.Pink; // 亮洋红色
        }

        if (ansiCode.Contains("96"))
        {
            return Brushes.LightCyan; // 亮青色
        }

        if (ansiCode.Contains("97"))
        {
            return Brushes.White; // 亮白色
        }

        return Brushes.White; // 默认颜色
    }

    public static string StripAnsiCodes(string input) => AnsiColorRegex.Replace(input, string.Empty);

    public static bool ContainsAnsiCodes(string input) => AnsiColorRegex.IsMatch(input);
}