using System.Text.RegularExpressions;

namespace MEFrpLauncherX.Core;

public class FileFormatDetector
{
    public static string DetectFormatFromFile(string filePath)
    {
        // 读取文件内容
        var content = File.ReadAllText(filePath).Trim();

        // 判断是否为 JSON
        if (IsJson(content))
        {
            return "JSON";
        }

        // 判断是否为 INI
        if (IsIni(content))
        {
            return "INI";
        }

        // 判断是否为 TOML
        if (IsToml(content))
        {
            return "TOML";
        }

        // 判断是否为 YAML
        if (IsYaml(content))
        {
            return "YAML";
        }

        // 无法识别的格式
        return "Unknown";
    }

    public static string DetectFormatFromContent(string content)
    {
        // 判断是否为 JSON
        if (IsJson(content))
        {
            return "JSON";
        }

        // 判断是否为 TOML
        if (IsToml(content))
        {
            return "TOML";
        }


        // 判断是否为 YAML
        if (IsYaml(content))
        {
            return "YAML";
        }

        // 判断是否为 INI
        if (IsIni(content))
        {
            return "INI";
        }


        // 无法识别的格式
        return "TXT";
    }

    private static bool IsJson(string content)
    {
        // JSON 通常以 { 或 [ 开头
        return content.StartsWith("{") || content.StartsWith("[");
    }

    private static bool IsIni(string content)
    {
        // INI 文件包含 [section] 和 key=value 格式
        return Regex.IsMatch(content, @"^\[.*\]$|^[^=]+=.+$", RegexOptions.Multiline);
    }

    private static bool IsToml(string content)
    {
        // TOML 支持 key = value 和表结构 [[table]]
        return Regex.IsMatch(content, @"^[^=]+=\s*.*$|^\[\[.*\]\]$", RegexOptions.Multiline);
    }

    private static bool IsYaml(string content)
    {
        // YAML 使用缩进和 : 表示键值对，或 - 表示列表项
        return Regex.IsMatch(content, @"^(\s*-|\w+:)", RegexOptions.Multiline);
    }
}