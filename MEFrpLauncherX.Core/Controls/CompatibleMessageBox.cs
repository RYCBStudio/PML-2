using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using MsBox.Avalonia.Enums;

namespace MEFrpLauncherX.Core.Controls;

/// <summary>
///     兼容 Ursa 消息框样式的 MessageBox 类
/// </summary>
public static class MessageBox
{
    public static async Task<MessageBoxResult> ShowAsync(object content, string caption = "", string title = "",
        MessageBoxIcon icon = MessageBoxIcon.Info, IList<FATaskDialogButton> buttons = null)
    {
        var res = await Dispatcher.UIThread.Invoke(async () =>
        {
            if (buttons == null)
            {
                buttons = icon switch
                {
                    MessageBoxIcon.Error or MessageBoxIcon.Warning => [FATaskDialogButton.OKButton],
                    MessageBoxIcon.Question => [FATaskDialogButton.YesButton, FATaskDialogButton.NoButton],
                    _ => [FATaskDialogButton.OKButton]
                };
            }

            var symbol = icon switch
            {
                MessageBoxIcon.Error => FASymbol.Dismiss,
                MessageBoxIcon.Warning => FASymbol.Alert,
                MessageBoxIcon.Question => FASymbol.Help,
                MessageBoxIcon.Success => FASymbol.Accept,
                _ => FASymbol.ContactInfo
            };

            var td = new FATaskDialog
            {
                Title = title,
                Header = caption,
                Content = content,
                IconSource = new FASymbolIconSource { Symbol = symbol },
                Buttons = buttons,
                XamlRoot = App.MainWindow ?? App.MainVisual
            };
            var result = await td.ShowAsync();
            return ConvertToMessageBoxResult(result, buttons);
        });
        return res;
    }

    // 重载方法，支持更简单的调用
    public static Task<MessageBoxResult> ShowAsync(string message, string caption = "",
        MessageBoxIcon icon = MessageBoxIcon.Info) =>
        ShowAsync(message, caption, "", icon);

    /// <summary>
    ///     兼容MsBox.Avalonia的消息框
    /// </summary>
    /// <param name="message"></param>
    /// <param name="caption"></param>
    /// <param name="icon"></param>
    /// <returns></returns>
    public static Task<MessageBoxResult> ShowAsync(string title, string msg = "",
        ButtonEnum btn = ButtonEnum.Ok, Icon icon = Icon.Info)
    {
        return ShowAsync(title, msg, "", icon switch
        {
            Icon.Error => MessageBoxIcon.Error,
            Icon.Warning => MessageBoxIcon.Warning,
            Icon.Question => MessageBoxIcon.Question,
            Icon.Success => MessageBoxIcon.Success,
            _ => MessageBoxIcon.Info
        }, btn switch
        {
            ButtonEnum.Ok => new[] { FATaskDialogButton.OKButton },
            ButtonEnum.YesNo => new[] { FATaskDialogButton.YesButton, FATaskDialogButton.NoButton },
            ButtonEnum.YesNoCancel => new[]
                { FATaskDialogButton.YesButton, FATaskDialogButton.NoButton, FATaskDialogButton.CancelButton },
            _ => new[] { FATaskDialogButton.OKButton }
        });
    }

    // 重载方法，支持按钮参数
    public static Task<MessageBoxResult> ShowAsync(string message, string caption, IList<FATaskDialogButton> buttons) =>
        ShowAsync(message, caption, "", MessageBoxIcon.Info, buttons);

    private static MessageBoxResult ConvertToMessageBoxResult(object dialogResult, IList<FATaskDialogButton> buttons)
    {
        if (dialogResult is FATaskDialogStandardResult standardResult)
        {
            return standardResult switch
            {
                FATaskDialogStandardResult.OK => MessageBoxResult.OK,
                FATaskDialogStandardResult.Yes => MessageBoxResult.Yes,
                FATaskDialogStandardResult.No => MessageBoxResult.No,
                FATaskDialogStandardResult.Cancel => MessageBoxResult.Cancel,
                _ => MessageBoxResult.None
            };
        }

        // 处理自定义按钮
        if (dialogResult is FATaskDialogButton button)
        {
            // 这里可以根据按钮的文本或其他属性来映射
            if (button.Text?.Contains("是") == true || button.Text?.Contains("Yes") == true)
            {
                return MessageBoxResult.Yes;
            }

            if (button.Text?.Contains("否") == true || button.Text?.Contains("No") == true)
            {
                return MessageBoxResult.No;
            }

            if (button.Text?.Contains(Languages.Languages.Text_Global_Confirm) == true || button.Text?.Contains("OK") == true)
            {
                return MessageBoxResult.OK;
            }

            if (button.Text?.Contains(Languages.Languages.Text_Global_Cancel) == true || button.Text?.Contains("Cancel") == true)
            {
                return MessageBoxResult.Cancel;
            }
        }

        return MessageBoxResult.None;
    }
}

/// <summary>
///     消息框结果枚举
/// </summary>
public enum MessageBoxResult
{
    None,
    OK,
    Cancel,
    Yes,
    No
}

/// <summary>
///     消息框图标类型枚举
/// </summary>
public enum MessageBoxIcon
{
    None,
    Info,
    Warning,
    Error,
    Question,
    Success
}