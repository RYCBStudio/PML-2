using System.Text.Json;

namespace MEFrpLauncherX.Core.Styling;

public class ThemeProcessor
{
    public static ThemeManifest? LoadTheme(string themeFilePath)
    {
        if (!File.Exists(themeFilePath))
        {
            throw new FileNotFoundException($"主题文件未找到: {themeFilePath}");
        }

        try
        {
            var json = File.ReadAllText(themeFilePath);
            var manifest =
                JsonSerializer.Deserialize<ThemeManifest>(json, AppJsonSerializerContext.Default.ThemeManifest);
            return manifest;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"主题文件格式无效: {themeFilePath}", ex);
        }
    }
}