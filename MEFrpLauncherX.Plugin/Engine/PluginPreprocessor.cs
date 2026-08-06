using System.Text;
using MEFrpLauncherX.Plugin.Core;
using YamlDotNet.Serialization;

namespace MEFrpLauncherX.Plugin.Engine;


public class PluginPreprocessor
{
    private readonly IDeserializer _deserializer = new DeserializerBuilder().Build();

    public PluginDefinition Process(string pluginFilePath, FunctionRegistry funcRegistry)
    {
        var raw = PreprocessAndDeserialize(pluginFilePath);
        if (raw.Id == "错误")
            return new PluginDefinition()
            {
                Id = "错误",
                Name = "无法找到文件"
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
            return new RawPlugin()
            {
                Id = "错误",
                Name = "无法找到文件",
            };
        }
        catch (IOException e)
        {
            return new RawPlugin()
            {
                Id = "错误",
                Name = "无法读取文件",
            };
        }
        catch (Exception e)
        {
            return new RawPlugin()
            {
                Id = "错误",
                Name = "读取文件时发生错误",
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
                    sb.AppendLine(File.ReadAllText(fullPath));
            }
            else
            {
                sb.AppendLine(line);
            }
        }

        return _deserializer.Deserialize<RawPlugin>(sb.ToString());
    }
}