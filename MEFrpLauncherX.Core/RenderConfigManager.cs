using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace MEFrpLauncherX.Core;

/// <summary>
///     渲染设置（独立于 Settings.json 的单文件配置）。
///     由于 ConfigManager 在 BuildAvaloniaApp 之后才初始化, 渲染相关设置
///     必须在 Avalonia 启动前读取, 因此单独存放于 Config/Render.json。
/// </summary>
public class RenderSettings
{
    /// <summary>
    ///     渲染模式: Auto / Vulkan / OpenGL / Software
    /// </summary>
    public string RenderingMode
    {
        get;
        set;
    } = "Auto";

    /// <summary>
    ///     Skia GPU 资源缓存上限 (MB)
    /// </summary>
    public int GpuMemoryLimitMb
    {
        get;
        set;
    } = 256;

    /// <summary>
    ///     Win32 低延迟交换链渲染模式
    /// </summary>
    public bool LowLatencyRendering
    {
        get;
        set;
    }
}

/// <summary>
///     读写 Config/Render.json, 不依赖 ConfigManager 与 Avalonia, 可在 BuildAvaloniaApp 阶段调用。
/// </summary>
public static class RenderConfigManager
{
    public static string RenderConfigPath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Render.json");

    private static readonly object Lock = new();

    private static RenderSettings _current;

    /// <summary>
    ///     加载渲染设置, 文件不存在或损坏时返回默认值。
    /// </summary>
    public static RenderSettings Load()
    {
        lock (Lock)
        {
            if (_current != null)
            {
                return _current;
            }

            try
            {
                if (File.Exists(RenderConfigPath))
                {
                    var json = File.ReadAllText(RenderConfigPath);
                    EnsureSerializer();
                    _current = JsonSerializer.Deserialize(json, App.AppJsonSerializerContext.RenderSettings);
                }
            }
            catch
            {
                _current = null;
            }

            _current ??= new RenderSettings();
            return _current;
        }
    }

    /// <summary>
    ///     更新渲染设置并保存。
    /// </summary>
    public static void UpdateConfig(System.Action<RenderSettings> updateAction)
    {
        lock (Lock)
        {
            var settings = Load();
            updateAction?.Invoke(settings);
            Save();
        }
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RenderConfigPath)!);
            EnsureSerializer();
            var json = JsonSerializer.Serialize(_current, App.AppJsonSerializerContext.RenderSettings);
            File.WriteAllText(RenderConfigPath, json);
        }
        catch
        {
            // 保存失败时静默忽略, 下次启动仍使用默认值
        }
    }

    /// <summary>
    ///     确保 JSON 序列化上下文已创建 (BuildAvaloniaApp 阶段早于 Core.App.Initialize)。
    /// </summary>
    private static void EnsureSerializer()
    {
        App.AppJsonSerializerContext ??= new AppJsonSerializerContext(new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        });
    }
}
