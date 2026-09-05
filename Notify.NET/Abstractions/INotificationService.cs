using System;
using System.Threading;
using System.Threading.Tasks;

namespace Notify.NET.Abstractions
{
    /// <summary>
    /// Dispatches OS notifications to the native notification subsystem for the current platform.
    /// </summary>
    public interface INotificationService : IDisposable
    {
        /// <summary>
        /// Whether the native notification subsystem is available and initialised on this platform.
        /// If false, <see cref="ShowAsync"/> will throw <see cref="Exceptions.PlatformNotSupportedException"/>.
        /// </summary>
        bool IsSupported { get; }

        /// <summary>
        /// Displays a notification and returns a platform-specific ID that can be used to hide it later.
        /// </summary>
        /// <param name="request">The notification to display, constructed via <see cref="Builder.NotificationBuilder"/>.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A non-negative notification ID on success.</returns>
        Task<long> ShowAsync(NotificationRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Programmatically dismisses a previously shown notification.
        /// </summary>
        /// <param name="notificationId">The ID returned by <see cref="ShowAsync"/>.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        Task HideAsync(long notificationId, CancellationToken cancellationToken = default);
    }
}
