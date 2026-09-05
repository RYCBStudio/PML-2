using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Notify.NET.Abstractions;

namespace Notify.NET.Platform.Linux
{
    /// <summary>
    /// Bridges the unmanaged libnotify GObject signal callbacks back to the managed
    /// <see cref="INotificationHandler"/> for each in-flight notification.
    ///
    /// Threading: GLib delivers both the action and closed signals on the GMainLoop thread.
    /// Handler invocations therefore happen on the GMainLoop background thread;
    /// consumers are responsible for marshalling to a UI thread if required.
    ///
    /// Lifetime rules (mirror the Windows bridge):
    ///   1. Static delegates are pinned permanently — their function pointers are valid forever.
    ///   2. Per-notification bridges are held in a static ConcurrentDictionary keyed by the
    ///      native NotifyNotification* pointer (cast to long).
    ///   3. The bridge's GCHandle prevents GC collection until the "closed" signal fires.
    ///   4. Release() is called once from the closed callback; it frees the GCHandle,
    ///      unrefs the GObject, and removes the dictionary entry.
    /// </summary>
    internal sealed class LibNotifyCallbackBridge
    {
        // ------------------------------------------------------------------
        // Static callbacks — one instance shared across all notifications
        // ------------------------------------------------------------------
        private static readonly LibNotifyNative.NotifyActionCallback _staticAction;
        private static readonly LibNotifyNative.NotifyClosedCallback _staticClosed;

        internal static readonly IntPtr PtrAction;
        internal static readonly IntPtr PtrClosed;

        // ------------------------------------------------------------------
        // Live bridge registry
        // ------------------------------------------------------------------
        private static readonly ConcurrentDictionary<long, LibNotifyCallbackBridge> _live
            = new ConcurrentDictionary<long, LibNotifyCallbackBridge>();

        // ------------------------------------------------------------------
        // Per-instance state
        // ------------------------------------------------------------------
        private readonly INotificationHandler? _handler;
        private readonly System.Collections.Generic.IReadOnlyList<Builder.NotificationButton> _buttons;
        private GCHandle _gcHandle;

        static LibNotifyCallbackBridge()
        {
            _staticAction = OnActionStatic;
            _staticClosed = OnClosedStatic;

            PtrAction = Marshal.GetFunctionPointerForDelegate(_staticAction);
            PtrClosed = Marshal.GetFunctionPointerForDelegate(_staticClosed);
        }

        private LibNotifyCallbackBridge(
            INotificationHandler? handler,
            System.Collections.Generic.IReadOnlyList<Builder.NotificationButton> buttons)
        {
            _handler = handler;
            _buttons = buttons;
            _gcHandle = GCHandle.Alloc(this, GCHandleType.Normal);
        }

        /// <summary>
        /// Registers a bridge for the native notification pointer.
        /// Must be called on the GMainLoop thread BEFORE connecting GObject signals,
        /// so the dictionary entry exists before any signal can fire.
        /// </summary>
        internal static LibNotifyCallbackBridge Register(
            IntPtr notificationPtr,
            INotificationHandler? handler,
            System.Collections.Generic.IReadOnlyList<Builder.NotificationButton> buttons)
        {
            var bridge = new LibNotifyCallbackBridge(handler, buttons);
            _live[(long)notificationPtr] = bridge;
            return bridge;
        }

        /// <summary>
        /// Removes the bridge and releases all resources.
        /// Called from the "closed" signal handler — do not call from application code.
        /// </summary>
        internal static void Release(IntPtr notificationPtr)
        {
            long key = (long)notificationPtr;
            if (_live.TryRemove(key, out var bridge))
            {
                if (bridge._gcHandle.IsAllocated)
                    bridge._gcHandle.Free();

                // Release the libnotify GObject reference.
                LibNotifyNative.g_object_unref(notificationPtr);
            }
        }

        // ------------------------------------------------------------------
        // Static GLib signal handlers — called on the GMainLoop thread
        // ------------------------------------------------------------------

        private static void OnActionStatic(IntPtr notification, string action, IntPtr userData)
        {
            try
            {
                long key = (long)notification;
                if (!_live.TryGetValue(key, out var bridge)) return;

                for (int i = 0; i < bridge._buttons.Count; i++)
                {
                    if (string.Equals(bridge._buttons[i].ActionId, action, StringComparison.Ordinal))
                    {
                        bridge._buttons[i].Callback?.Invoke(key);
                        bridge._handler?.OnButtonActivated(key, i);
                        return;
                    }
                }

                bridge._handler?.OnActivated(key);
            }
            catch { }
        }

        private static void OnClosedStatic(IntPtr notification, IntPtr userData)
        {
            try
            {
                long key = (long)notification;
                if (_live.TryGetValue(key, out var bridge))
                {
                    int reason = LibNotifyNative.notify_notification_get_closed_reason(notification);
                    bridge._handler?.OnDismissed(key, MapCloseReason(reason));
                }
            }
            catch { }
            finally { Release(notification); }
        }

        private static DismissReason MapCloseReason(int reason)
        {
            // freedesktop.org notification spec close reasons:
            // 1 = expired, 2 = dismissed by user, 3 = closed by app, 4 = undefined
            switch (reason)
            {
                case 1:  return DismissReason.TimedOut;
                case 2:  return DismissReason.UserCancelled;
                case 3:  return DismissReason.ApplicationHidden;
                default: return DismissReason.Unknown;
            }
        }
    }
}
