using Notify.NET.Builder;

namespace Notify.NET.Abstractions
{
    /// <summary>
    /// Receives lifecycle events for a single notification.
    /// Implement this interface or use the delegate-based callbacks on
    /// <see cref="NotificationBuilder"/> to respond to user interactions.
    /// </summary>
    public interface INotificationHandler
    {
        /// <summary>Called when the user clicks the notification body (not a button).</summary>
        void OnActivated(long notificationId);

        /// <summary>Called when the user clicks one of the action buttons.</summary>
        /// <param name="notificationId">The notification's platform ID.</param>
        /// <param name="buttonIndex">Zero-based index matching the order buttons were added via the builder.</param>
        void OnButtonActivated(long notificationId, int buttonIndex);

        /// <summary>Called when the notification is dismissed (by the user, system, or expiration).</summary>
        void OnDismissed(long notificationId, DismissReason reason);

        /// <summary>Called when the platform fails to display the notification.</summary>
        void OnFailed(long notificationId);
    }
}
