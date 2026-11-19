using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MEFrpLauncherX.Core.Languages;

namespace MEFrpLauncherX.Controls;

/// <summary>
///     「网络不可用」状态提示控件（26.5.1）。
///     <para>
///         与 <see cref="NoData" /> 同族：都是「当前内容不可用」的占位提示，
///         区别在于 <see cref="NoData" /> 表示「本来就没有数据」，
///         本控件表示「有数据但拿不到 —— 网络断了」。
///     </para>
///     <para>
///         26.5.1 C2：登录页在人机验证请求失败（断网）时会用到该状态；
///         文案可由调用方覆盖，以适配不同场景（如「更新失败」「节点不可达」）。
///     </para>
/// </summary>
public partial class NoConnection : UserControl
{
    /// <summary>
    ///     主提示文案。
    /// </summary>
    public static readonly StyledProperty<string> MessageProperty =
        AvaloniaProperty.Register<NoConnection, string>(nameof(Message), "网络连接已断开");

    /// <summary>
    ///     次要说明文案（默认为空，不显示）。
    /// </summary>
    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<NoConnection, string>(nameof(Description), "");

    /// <summary>
    ///     重试命令。为 null 时不显示重试按钮。
    /// </summary>
    public static readonly StyledProperty<ICommand?> RetryCommandProperty =
        AvaloniaProperty.Register<NoConnection, ICommand?>(nameof(RetryCommand));

    public NoConnection()
    {
        InitializeComponent();

        // 设计时给一点默认文案，方便在 IDE 里预览效果
        if (Design.IsDesignMode)
        {
            Message = Languages.Text_Global_NoConnection;
            Description = Languages.Text_Global_NoConnectionAdvice;
        }
    }

    /// <summary>
    ///     主提示文案，默认为「网络连接已断开」。
    /// </summary>
    public string Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <summary>
    ///     次要说明文案，默认为空字符串（不显示任何副标题）。
    /// </summary>
    public string Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>
    ///     重试命令。为 null 时不显示重试按钮。
    /// </summary>
    public ICommand? RetryCommand
    {
        get => GetValue(RetryCommandProperty);
        set => SetValue(RetryCommandProperty, value);
    }
}
