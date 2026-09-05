using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Notify.NET.Abstractions;

namespace Notify.NET.Platform.MacOS
{
    /// <summary>
    /// Bridges unmanaged callbacks from <c>libMacNotifyWrapper.dylib</c> back to the
    /// managed <see cref="INotificationHandler"/> for each in-flight notification.
    ///
    /// Design rules — identical to the Windows and Linux bridges:
    ///
    /// 1. The four static delegates are stored in <c>static readonly</c> fields and
    ///    their function pointers obtained once; they are permanently valid.
    ///
    /// 2. Per-notification state is held in <see cref="MacNotifyCallbackBridge"/> instances
    ///    tracked in <see cref="_live"/>. A <see cref="GCHandle"/> prevents GC collection.
    ///
    /// 3. <see cref="Release"/> is called from <em>every</em> terminal callback.
    ///    On macOS, body-tap, button-tap, dismiss and failure are all terminal:
    ///    <c>UNUserNotificationCenter</c> fires exactly one response per notification
    ///    and does NOT separately fire a dismiss event after an action response.
    /// </summary>
    internal sealed class MacNotifyCallbackBridge
    {
        // ------------------------------------------------------------------
        // Static delegates — one set for the entire process lifetime
        // ------------------------------------------------------------------
        private static readonly MacNotifyNative.ActivatedCallback      _staticActivated;
        private static readonly MacNotifyNative.ButtonActivatedCallback _staticButtonActivated;
        private static readonly MacNotifyNative.DismissedCallback       _staticDismissed;
        private static readonly MacNotifyNative.FailedCallback          _staticFailed;

        internal static readonly IntPtr PtrActivated;
        internal static readonly IntPtr PtrButtonActivated;
        internal static readonly IntPtr PtrDismissed;
        internal static readonly IntPtr PtrFailed;

        // ------------------------------------------------------------------
        // Live bridge registry: notifId → bridge
        // ------------------------------------------------------------------
        private static readonly ConcurrentDictionary<long, MacNotifyCallbackBridge> _live
            = new ConcurrentDictionary<long, MacNotifyCallbackBridge>();

        // ------------------------------------------------------------------
        // Per-instance state
        // ------------------------------------------------------------------
        private readonly INotificationHandler? _handler;
        private GCHandle _gcHandle;

        static MacNotifyCallbackBridge()
        {
            _staticActivated       = OnActivatedStatic;
            _staticButtonActivated = OnButtonActivatedStatic;
            _staticDismissed       = OnDismissedStatic;
            _staticFailed          = OnFailedStatic;

            PtrActivated       = Marshal.GetFunctionPointerForDelegate(_staticActivated);
            PtrButtonActivated = Marshal.GetFunctionPointerForDelegate(_staticButtonActivated);
            PtrDismissed       = Marshal.GetFunctionPointerForDelegate(_staticDismissed);
            PtrFailed          = Marshal.GetFunctionPointerForDelegate(_staticFailed);
        }

        private MacNotifyCallbackBridge(INotificationHandler? handler)
        {
            _handler  = handler;
            _gcHandle = GCHandle.Alloc(this, GCHandleType.Normal);
        }

        /// <summary>
        /// Registers a bridge for <paramref name="notifId"/>.
        /// Call immediately after <see cref="MacNotifyNative.MNW_ShowNotification"/> returns
        /// a positive ID.
        /// </summary>
        internal static MacNotifyCallbackBridge Register(long notifId, INotificationHandler? handler)
        {
            var bridge = new MacNotifyCallbackBridge(handler);
            _live[notifId] = bridge;
            return bridge;
        }

        /// <summary>
        /// Removes the bridge and frees its <see cref="GCHandle"/>.
        /// Safe to call multiple times; subsequent calls are no-ops.
        /// </summary>
        internal static void Release(long notifId)
        {
            if (_live.TryRemove(notifId, out var bridge) && bridge._gcHandle.IsAllocated)
                bridge._gcHandle.Free();
        }

        // ------------------------------------------------------------------
        // Static routing callbacks — invoked on a background GCD thread
        // ------------------------------------------------------------------

        private static void OnActivatedStatic(long notifId)
        {
            try
            {
                if (_live.TryGetValue(notifId, out var bridge))
                    bridge._handler?.OnActivated(notifId);
            }
            catch { /* must not propagate into native code */ }
            finally { Release(notifId); }
        }

        private static void OnButtonActivatedStatic(long notifId, int buttonIndex)
        {
            try
            {
                if (_live.TryGetValue(notifId, out var bridge))
                    bridge._handler?.OnButtonActivated(notifId, buttonIndex);
            }
            catch { }
            finally { Release(notifId); }
        }

        private static void OnDismissedStatic(long notifId, int reason)
        {
            try
            {
                if (_live.TryGetValue(notifId, out var bridge))
                    bridge._handler?.OnDismissed(notifId, MapDismissReason(reason));
            }
            catch { }
            finally { Release(notifId); }
        }

        private static void OnFailedStatic(long notifId)
        {
            try
            {
                if (_live.TryGetValue(notifId, out var bridge))
                    bridge._handler?.OnFailed(notifId);
            }
            catch { }
            finally { Release(notifId); }
        }

        private static DismissReason MapDismissReason(int reason)
        {
            // MNW_DISMISS_EXPIRED = 0, MNW_DISMISS_USER = 1, MNW_DISMISS_APP_REMOVED = 2
            switch (reason)
            {
                case 0:  return DismissReason.TimedOut;
                case 1:  return DismissReason.UserCancelled;
                case 2:  return DismissReason.ApplicationHidden;
                default: return DismissReason.Unknown;
            }
        }
    }
}
