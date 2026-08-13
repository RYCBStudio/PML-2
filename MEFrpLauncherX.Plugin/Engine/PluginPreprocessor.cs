using System.Text;
using MEFrpLauncherX.Core;
using MEFrpLauncherX.Core.Languages;
using MEFrpLauncherX.Plugin.Core;
using YamlDotNet.Serialization;

namespace MEFrpLauncherX.Plugin.Engine;

public class PluginPreprocessor
{
    private readonly IDeserializer _deserializer = new StaticDeserializerBuilder(new YamlModelStaticContext())
        .WithCaseInsensitivePropertyMatching().Build();

    public PluginDefinition Process(string pluginFilePath, FunctionRegistry funcRegistry)
    {
        var raw = PreprocessAndDeserialize(pluginFilePath);
        if (raw.Id == "错误")
            return new PluginDefinition()
            {
                Id = "错误",
                Name = Languages.Text_Plugin_FileNotFound
            };
        // 注册函数
        foreach (var kv in raw.Functions)
        {
            funcRegistry.Define(kv.Key, kv.Value);
        }

        return new PluginDefinition
        {
            Id = raw.Id,
            Name = raw.Name,
            Triggers = raw.Triggers
        };
    }

    private RawPlugin PreprocessAndDeserialize(string path)
    {
        string content;
        try
        {
            content = File.ReadAllText(path);
        }
        catch (FileNotFoundException e)
        {
            App.CurrentLogger.Warning($"插件文件不存在: {path}, {e.Message}", module: EnumLogModule.Plugin);
            return new RawPlugin()
            {
                Id = "错误",
                Name = Languages.Text_Plugin_FileNotFound,
            };
        }
        catch (IOException e)
        {
            App.CurrentLogger.Warning($"插件文件读取失败: {path}, {e.Message}", module: EnumLogModule.Plugin);
            return new RawPlugin()
            {
                Id = "错误",
                Name = Languages.Text_Plugin_CannotReadFile,
            };
        }
        catch (Exception e)
        {
            App.CurrentLogger.Warning($"插件文件读取异常: {path}, {e.Message}", module: EnumLogModule.Plugin);
            return new RawPlugin()
            {
                Id = "错误",
                Name = Languages.Text_Plugin_ReadFileError,
            };
        }

        var lines = content.Split('\n');
        var sb = new StringBuilder();
        var baseDir = Path.GetDirectoryName(path)!;

        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("#include:"))
            {
                var includePath = line.Split(':')[1].Trim().Trim('"', '\'');
                var fullPath = Path.Combine(baseDir, includePath);
                if (File.Exists(fullPath))
                {
                    sb.AppendLine(File.ReadAllText(fullPath));
                }
                else
                {
                    App.CurrentLogger.Warning($"插件 {path} 的 include 文件不存在: {fullPath}",
                        module: EnumLogModule.Plugin);
                }
            }
            else
            {
                sb.AppendLine(line);
            }
        }

        try
        {
            return _deserializer.Deserialize<RawPlugin>(sb.ToString());
        }
        catch (Exception ex)
        {
            App.CurrentLogger.Warning($"插件 YAML 解析失败: {path}, {ex.Message}", module: EnumLogModule.Plugin);
            return new RawPlugin()
            {
                Id = "错误",
                Name = Languages.Text_Plugin_ReadFileError,
            };
        }
    }
}