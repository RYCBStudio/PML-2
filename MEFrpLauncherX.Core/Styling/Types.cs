#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 'required' 修饰符或声明为可以为 null。
namespace MEFrpLauncherX.Core.Styling;

public class ThemeManifest
{
    public string Name
    {
        get;
        set;
    }

    public string Author
    {
        get;
        set;
    }

    public string Description
    {
        get;
        set;
    }

    public string Version
    {
        get;
        set;
    }

    public List<AccentMeta> AccentColor
    {
        get;
        set;
    }

    /// <summary>
    ///     预览图路径，支持相对路径（相对于主题文件）和绝对路径
    /// </summary>
    public string? PreviewImage
    {
        get;
        set;
    }

    public BackgroundMeta Background
    {
        get;
        set;
    }

    /// <summary>
    ///     字体名称，支持系统已安装的字体和相对路径（相对于主题文件）的字体文件（.ttf或.otf）。如果指定了字体文件，应用程序将优先使用该字体文件而不是系统字体。
    /// </summary>
    public object FontFamily
    {
        get;
        set;
    }
}

public class BackgroundMeta
{
    /// <summary>
    ///     背景类型，支持 "SolidColor"（纯色）和 "Image"（图片）
    /// </summary>
    public string Type
    {
        get;
        set;
    }

    /// <summary>
    ///     当 Type 为 "SolidColor" 时，Color 字段表示背景颜色值，支持 Hex（#RRGGBB 或 #AARRGGBB）格式；当 Type 为 "Image" 时，Image
    ///     字段表示背景图片路径，支持相对路径（相对于主题文件）和绝对路径
    /// </summary>
    public string? Image
    {
        get;
        set;
    }

    public string? Color
    {
        get;
        set;
    }

    /// <summary>
    ///     填充模式，当 Type 为 "Image" 时，支持 "Fill"（拉伸填充）、"Uniform"（保持纵横比缩放以适应容器）和 "UniformToFill"（保持纵横比缩放以完全覆盖容器）三种模式
    ///     当 Type 为 "SolidColor" 时，此字段指示如何填充背景图片。支持的类型有"Solid", "Radiation", "Gradient"。
    /// </summary>
    public string FillMode
    {
        get;
        set;
    }

    public double LayerOpacity
    {
        get;
        set;
    }
}

public class AccentMeta
{
    /// <summary>
    ///     颜色值，支持Hex（#RRGGBB或#AARRGGBB）格式
    /// </summary>
    public string Color
    {
        get;
        set;
    }

    /// <summary>
    ///     渐变时间，单位秒
    /// </summary>
    public double Duration
    {
        get;
        set;
    }
}