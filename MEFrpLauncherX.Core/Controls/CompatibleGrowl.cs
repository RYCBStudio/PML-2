using Message.Avalonia.Models;

namespace MEFrpLauncherX.Core.Controls;

public class Growl
{
    public static void Info(string message, string title = "", bool ShowIcon = true, bool ShowClose = true)
    {
        try
        {
            App.PML2MsgMnger.ShowInformationMessage(message, new MessageOptions
            {
                Title = string.IsNullOrEmpty(title) ? Languages.Languages.Caption_Info : title,
                HideClose = !ShowClose,
                HideIcon = !ShowIcon
            });
        }
        catch
        {
        }

        // Dispatcher.UIThread.Invoke(() =>
        //     App.WindowNotificationManager?.Show(
        //         new Notification(title, message),
        //         type: NotificationType.Information, TimeSpan.FromSeconds(3))
        // );
    }

    public static void Warning(string message, string title = "", bool ShowIcon = true, bool ShowClose = true)
    {
        try
        {
            App.PML2MsgMnger.ShowWarningMessage(message, new MessageOptions
            {
                Title = string.IsNullOrEmpty(title) ? Languages.Languages.Caption_Warning : title,
                HideClose = !ShowClose,
                HideIcon = !ShowIcon
            });
        }
        catch
        {
        }

        // Dispatcher.UIThread.Invoke(() =>
        //     App.WindowNotificationManager?.Show(
        //         new Notification(title, message),
        //         type: NotificationType.Warning, TimeSpan.FromSeconds(3))
        // );
    }

    public static void Error(string message, string title = "", bool ShowIcon = true, bool ShowClose = true)
    {
        try
        {
            App.PML2MsgMnger.ShowErrorMessage(message, new MessageOptions
            {
                Title = string.IsNullOrEmpty(title) || title == "错误" ? Languages.Languages.Caption_Error : title,
                HideClose = !ShowClose,
                HideIcon = !ShowIcon
            });
        }
        catch
        {
        }

        // Dispatcher.UIThread.Invoke(() =>
        //     App.WindowNotificationManager?.Show(
        //         new Notification(title, message),
        //         type: NotificationType.Error, TimeSpan.FromSeconds(3))
        // );
    }

    public static void Success(string message, string title = "", bool ShowIcon = true, bool ShowClose = true)
    {
        try
        {
            App.PML2MsgMnger.ShowSuccessMessage(message, new MessageOptions
            {
                Title = string.IsNullOrEmpty(title) ? Languages.Languages.Caption_Success : title,
                HideClose = !ShowClose,
                HideIcon = !ShowIcon
            });
        }
        catch
        {
        }

        // Dispatcher.UIThread.Invoke(() =>
        //     App.WindowNotificationManager?.Show(
        //         new Notification(title, message),
        //         type: NotificationType.Success, TimeSpan.FromSeconds(3))
        // );
    }
}