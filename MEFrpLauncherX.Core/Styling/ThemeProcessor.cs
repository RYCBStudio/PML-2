using System.Text.Json;

namespace MEFrpLauncherX.Core.Styling;

public class ThemeProcessor
{
    /// <summary>
    ///     判断字体名称是否为文件路径（而非系统字体名）
    /// </summary>
    public static bool IsFontFilePath(string? fontFamily)
    {
        if (string.IsNullOrEmpty(fontFamily))
            return false;

        // 如果以 .ttf、.otf、.ttc 结尾，或者包含路径分隔符，则认为是文件路径
        return fontFamily.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
               fontFamily.EndsWith(".otf", StringComparison.OrdinalIgnoreCase) ||
               fontFamily.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase) ||
               fontFamily.Contains('/') ||
               fontFamily.Contains('\\');
    }

    public static ThemeManifest? LoadTheme(string themeFilePath)
    {
        if (!File.Exists(themeFilePath))
        {
            throw new FileNotFoundException($"主题文件未找到: {themeFilePath}");
        }

        var fs = new FileStream(themeFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite,
            FileShare.ReadWrite | FileShare.Delete | FileShare.Inheritable);
        try
        {
            var json = new StreamReader(fs).ReadToEnd();
            var manifest =
                JsonSerializer.Deserialize<ThemeManifest>(json, App.AppJsonSerializerContext.ThemeManifest);
            return manifest;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"主题文件格式无效: {themeFilePath}", ex);
        }
        finally
        {
            fs.Close();
        }
    }
}