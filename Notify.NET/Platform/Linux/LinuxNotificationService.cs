using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Notify.NET.Abstractions;
using Notify.NET.Exceptions;

namespace Notify.NET.Platform.Linux
{
    /// <summary>
    /// <see cref="INotificationService"/> implementation backed by libnotify.
    ///
    /// Threading model:
    ///   All libnotify calls must be made from the GLib GMainLoop thread to ensure correct
    ///   signal wiring. <see cref="GLibMainLoopRunner.InvokeAsync"/> marshals work onto
    ///   that thread. Callbacks (action-invoked, closed) are delivered on the same thread.
    /// </summary>
    public sealed class LinuxNotificationService : INotificationService
    {
        private readonly string _appName;
        private readonly GLibMainLoopRunner _loopRunner;
        private volatile bool _disposed;

        /// <inheritdoc/>
        public bool IsSupported { get; private set; }

        /// <param name="appName">Application name passed to notify_init().</param>
        public LinuxNotificationService(string appName)
        {
            _appName = appName ?? throw new ArgumentNullException(nameof(appName));
            _loopRunner = new GLibMainLoopRunner();

            // Initialise libnotify on the GMainLoop thread.
            // Detect a missing libnotify gracefully so IsSupported is false rather than throwing.
            try
            {
                _loopRunner.InvokeAsync(() =>
                {
                    if (!LibNotifyNative.notify_is_initted())
                    {
                        bool ok = LibNotifyNative.notify_init(_appName);
                        IsSupported = ok;
                    }
                    else
                    {
                        IsSupported = true;
                    }
                }).GetAwaiter().GetResult();
            }
            catch (DllNotFoundException)
            {
                IsSupported = false;
            }
        }

        // ------------------------------------------------------------------
        // INotificationService
        // ------------------------------------------------------------------

        /// <inheritdoc/>
        public async Task<long> ShowAsync(NotificationRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            ThrowIfDisposedOrUnsupported();

            cancellationToken.ThrowIfCancellationRequested();

            long notificationId = 0;

            await _loopRunner.InvokeAsync(() =>
            {
                notificationId = ShowOnLoopThread(request);
            }).ConfigureAwait(false);

            return notificationId;
        }

        /// <inheritdoc/>
        public async Task HideAsync(long notificationId, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposedOrUnsupported();
            cancellationToken.ThrowIfCancellationRequested();

            await _loopRunner.InvokeAsync(() =>
            {
                var ptr = (IntPtr)notificationId;
                IntPtr error = IntPtr.Zero;
                bool ok = LibNotifyNative.notify_notification_close(ptr, ref error);

                if (!ok)
                {
                    string msg = MarshalGError(ref error);
                    throw new NotificationException($"notify_notification_close failed: {msg}");
                }

                // Release is normally triggered by the "closed" signal, but call it here
                // too in case the signal doesn't fire (some notification daemons omit it).
                LibNotifyCallbackBridge.Release(ptr);
            }).ConfigureAwait(false);
        }

        // ------------------------------------------------------------------
        // IDisposable
        // ------------------------------------------------------------------

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                _loopRunner.InvokeAsync(() =>
                {
                    if (LibNotifyNative.notify_is_initted())
                        LibNotifyNative.notify_uninit();
                }).GetAwaiter().GetResult();
            }
            catch { /* best-effort cleanup */ }

            _loopRunner.Dispose();
        }

        // ------------------------------------------------------------------
        // Private helpers
        // ------------------------------------------------------------------

        private void ThrowIfDisposedOrUnsupported()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LinuxNotificationService));
            if (!IsSupported) throw new Exceptions.PlatformNotSupportedException();
        }

        /// <summary>Called on the GMainLoop thread to create and show a notification.</summary>
        private long ShowOnLoopThread(NotificationRequest request)
        {
            IntPtr notification = LibNotifyNative.notify_notification_new(
                request.Title,
                request.Body,
                null /* icon — we set it from imagePath below if provided */);

            if (notification == IntPtr.Zero)
                throw new NotificationException("notify_notification_new returned null.");

            // --- Image ---
            string? resolvedImage = ResolveImagePath(request.ImagePath);
            if (resolvedImage != null)
                ApplyImage(notification, resolvedImage);

            // --- Urgency hint ---
            byte urgency = MapUrgency(request.Urgency);
            IntPtr urgencyVariant = LibNotifyNative.g_variant_new_byte(urgency);
            LibNotifyNative.notify_notification_set_hint(notification, "urgency", urgencyVariant);

            // --- Expiration ---
            if (request.Expiration.HasValue)
            {
                int ms = (int)request.Expiration.Value.TotalMilliseconds;
                // notify_notification_set_timeout is available in newer libnotify versions;
                // set as a hint for compatibility with older versions too.
                LibNotifyNative.notify_notification_set_hint(
                    notification, "x-canonical-snap-decisions-timeout",
                    LibNotifyNative.g_variant_new_byte((byte)Math.Clamp(ms / 1000, 0, 255)));
            }

            // --- Register bridge BEFORE connecting signals ---
            LibNotifyCallbackBridge.Register(notification, request.Handler, request.Buttons);

            // --- Action buttons ---
            for (int i = 0; i < request.Buttons.Count; i++)
            {
                var btn = request.Buttons[i];
                LibNotifyNative.notify_notification_add_action(
                    notification,
                    btn.ActionId,
                    btn.Label,
                    LibNotifyCallbackBridge.PtrAction,
                    notification,   // userData = the notification pointer (our lookup key)
                    IntPtr.Zero);
            }

            // --- "closed" signal ---
            LibNotifyNative.g_signal_connect_data(
                notification,
                "closed",
                LibNotifyCallbackBridge.PtrClosed,
                notification,   // data = lookup key
                IntPtr.Zero,
                0);

            // --- Show ---
            IntPtr error = IntPtr.Zero;
            bool ok = LibNotifyNative.notify_notification_show(notification, ref error);
            if (!ok)
            {
                string msg = MarshalGError(ref error);
                LibNotifyCallbackBridge.Release(notification);
                throw new NotificationException($"notify_notification_show failed: {msg}");
            }

            return (long)notification;
        }

        private static string? ResolveImagePath(string? path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            try
            {
                string absolute = Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
                return File.Exists(absolute) ? absolute : null;
            }
            catch (Exception) { return null; }
        }

        private static void ApplyImage(IntPtr notification, string imagePath)
        {
            IntPtr pixbufError = IntPtr.Zero;
            IntPtr pixbuf = IntPtr.Zero;

            try
            {
                pixbuf = LibNotifyNative.gdk_pixbuf_new_from_file(imagePath, ref pixbufError);
                if (pixbuf != IntPtr.Zero)
                {
                    LibNotifyNative.notify_notification_set_image_from_pixbuf(notification, pixbuf);
                }
                else
                {
                    // Image loading failed — clear the error and continue without an image.
                    if (pixbufError != IntPtr.Zero)
                    {
                        LibNotifyNative.g_error_free(pixbufError);
                        pixbufError = IntPtr.Zero;
                    }
                }
            }
            finally
            {
                // Unref the pixbuf — the notification holds its own reference after set_image.
                if (pixbuf != IntPtr.Zero)
                    LibNotifyNative.g_object_unref(pixbuf);
                if (pixbufError != IntPtr.Zero)
                    LibNotifyNative.g_error_free(pixbufError);
            }
        }

        private static string MarshalGError(ref IntPtr error)
        {
            if (error == IntPtr.Zero) return "(unknown error)";

            // GError layout: domain (uint32) | code (int32) | message (char*)
            // On 64-bit Linux: domain at 0, code at 4, message pointer at 8.
            IntPtr messagePtr = Marshal.ReadIntPtr(error, 8);
            string message = Marshal.PtrToStringAnsi(messagePtr) ?? "(null)";

            LibNotifyNative.g_error_free(error);
            error = IntPtr.Zero;
            return message;
        }

        private static byte MapUrgency(NotificationUrgency urgency)
        {
            switch (urgency)
            {
                case NotificationUrgency.Low:      return LibNotifyNative.URGENCY_LOW;
                case NotificationUrgency.Critical:
                case NotificationUrgency.Alarm:    return LibNotifyNative.URGENCY_CRITICAL;
                default:                           return LibNotifyNative.URGENCY_NORMAL;
            }
        }
    }
}
