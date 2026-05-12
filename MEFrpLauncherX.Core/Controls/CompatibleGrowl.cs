using Message.Avalonia.Models;

namespace MEFrpLauncherX.Core.Controls;

public class Growl
{
    public static void Info(string message, string title = "信息", bool ShowIcon = true, bool ShowClose = true)
    {
        try
        {
            App.PML2MsgMnger.ShowInformationMessage(message, new MessageOptions
            {
                Title = title,
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

    public static void Warning(string message, string title = "警告", bool ShowIcon = true, bool ShowClose = true)
    {
        try
        {
            App.PML2MsgMnger.ShowWarningMessage(message, new MessageOptions
            {
                Title = title,
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

    public static void Error(string message, string title = "错误", bool ShowIcon = true, bool ShowClose = true)
    {
        try
        {
            App.PML2MsgMnger.ShowErrorMessage(message, new MessageOptions
            {
                Title = title,
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

    public static void Success(string message, string title = "成功", bool ShowIcon = true, bool ShowClose = true)
    {
        try
        {
            App.PML2MsgMnger.ShowSuccessMessage(message, new MessageOptions
            {
                Title = title,
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