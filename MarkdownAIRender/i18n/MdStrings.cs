using System.Globalization;

namespace MarkdownAIRender.i18n;

/// <summary>
///     轻量级多语言字符串（zh-CN 默认 / en-US / zh-Hant），按当前 UI 区域性取值
/// </summary>
public static class MdStrings
{
    private static string Lang => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh"
        ? CultureInfo.CurrentUICulture.Name.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase) ||
          CultureInfo.CurrentUICulture.Name.Contains("-TW", StringComparison.OrdinalIgnoreCase) ||
          CultureInfo.CurrentUICulture.Name.Contains("-HK", StringComparison.OrdinalIgnoreCase) ||
          CultureInfo.CurrentUICulture.Name.Contains("-MO", StringComparison.OrdinalIgnoreCase)
            ? "hant"
            : "cn"
        : "en";

    public static string AlertTip => Lang switch { "cn" => "提示", "hant" => "提示", _ => "Tip" };

    public static string AlertNote => Lang switch { "cn" => "注意", "hant" => "注意", _ => "Note" };

    public static string AlertWarning => Lang switch { "cn" => "警告", "hant" => "警告", _ => "Warning" };

    public static string AlertCaution => Lang switch { "cn" => "重要", "hant" => "重要", _ => "Important" };

    public static string AlertInfo => Lang switch { "cn" => "信息", "hant" => "資訊", _ => "Information" };

    public static string Copy => Lang switch { "cn" => "复制", "hant" => "複製", _ => "Copy" };

    public static string CopySucceeded => Lang switch { "cn" => "复制成功", "hant" => "複製成功", _ => "Copied successfully" };
}
