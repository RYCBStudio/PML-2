using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Notify.NET.Abstractions;

namespace Notify.NET.Platform.Windows
{
    /// <summary>
    /// Bridges the unmanaged WinToastWrapper callbacks back to the managed
    /// <see cref="INotificationHandler"/> for each in-flight toast.
    ///
    /// Design rules that MUST be maintained to avoid memory-safety bugs:
    ///
    /// 1. The four static delegates (<see cref="_staticActivated"/> etc.) are kept alive
    ///    for the process lifetime because they are stored in static fields. Their function
    ///    pointers are therefore permanently valid for unmanaged code to call.
    ///
    /// 2. Per-notification state is held in <see cref="WinToastHandlerBridge"/> instances
    ///    tracked in the static <see cref="_live"/> dictionary. Each instance's GCHandle
    ///    prevents the GC from collecting it while the toast is alive.
    ///
    /// 3. Routing works by passing the toast ID (long) back through the static callbacks,
    ///    which look up the matching bridge instance in <see cref="_live"/>.
    ///
    /// 4. <see cref="Release"/> is called exactly once, from whichever callback fires last
    ///    (dismissed or failed). It removes the entry and frees the GCHandle.
    /// </summary>
    internal sealed class WinToastHandlerBridge
    {
        // ------------------------------------------------------------------
        // Static callback function pointers — allocated once, never collected
        // ------------------------------------------------------------------
        private static readonly WinToastNative.ActivatedCallback      _staticActivated;
        private static readonly WinToastNative.ButtonActivatedCallback _staticButtonActivated;
        private static readonly WinToastNative.DismissedCallback       _staticDismissed;
        private static readonly WinToastNative.FailedCallback          _staticFailed;

        // Pointer-sized function pointers stored in the WNT_Handler struct
        internal static readonly IntPtr PtrActivated;
        internal static readonly IntPtr PtrButtonActivated;
        internal static readonly IntPtr PtrDismissed;
        internal static readonly IntPtr PtrFailed;

        // ------------------------------------------------------------------
        // Static dictionary: toastId → live bridge instance
        // ------------------------------------------------------------------
        private static readonly ConcurrentDictionary<long, WinToastHandlerBridge> _live
            = new ConcurrentDictionary<long, WinToastHandlerBridge>();

        // ------------------------------------------------------------------
        // Per-instance state
        // ------------------------------------------------------------------
        private readonly INotificationHandler? _handler;
        private GCHandle _gcHandle; // keeps this bridge alive from unmanaged side

        static WinToastHandlerBridge()
        {
            // Create static delegates and pin their function pointers permanently.
            _staticActivated      = OnActivatedStatic;
            _staticButtonActivated = OnButtonActivatedStatic;
            _staticDismissed      = OnDismissedStatic;
            _staticFailed         = OnFailedStatic;

            PtrActivated       = Marshal.GetFunctionPointerForDelegate(_staticActivated);
            PtrButtonActivated = Marshal.GetFunctionPointerForDelegate(_staticButtonActivated);
            PtrDismissed       = Marshal.GetFunctionPointerForDelegate(_staticDismissed);
            PtrFailed          = Marshal.GetFunctionPointerForDelegate(_staticFailed);
        }

        private WinToastHandlerBridge(INotificationHandler? handler)
        {
            _handler = handler;
            // Allocate a GCHandle so the GC cannot collect this instance.
            _gcHandle = GCHandle.Alloc(this, GCHandleType.Normal);
        }

        /// <summary>
        /// Creates a bridge and registers it under <paramref name="toastId"/>.
        /// Call this immediately after <see cref="WinToastNative.WNT_ShowToast"/> returns a positive ID.
        /// </summary>
        internal static WinToastHandlerBridge Register(long toastId, INotificationHandler? handler)
        {
            var bridge = new WinToastHandlerBridge(handler);
            _live[toastId] = bridge;
            return bridge;
        }

        /// <summary>
        /// Removes the bridge for <paramref name="toastId"/> and releases its GCHandle.
        /// Safe to call multiple times; subsequent calls are no-ops.
        /// </summary>
        internal static void Release(long toastId)
        {
            if (_live.TryRemove(toastId, out var bridge) && bridge._gcHandle.IsAllocated)
                bridge._gcHandle.Free();
        }

        // ------------------------------------------------------------------
        // Static routing callbacks — invoked by unmanaged code on a WinRT thread
        // ------------------------------------------------------------------

        private static void OnActivatedStatic(long toastId)
        {
            // Must not let exceptions escape to native code — unhandled exceptions
            // on WinRT callback threads crash the process with no useful error.
            try
            {
                if (_live.TryGetValue(toastId, out var bridge))
                    bridge._handler?.OnActivated(toastId);
            }
            catch { /* swallow — caller cannot handle managed exceptions */ }
        }

        private static void OnButtonActivatedStatic(long toastId, int buttonIndex)
        {
            try
            {
                if (_live.TryGetValue(toastId, out var bridge))
                    bridge._handler?.OnButtonActivated(toastId, buttonIndex);
            }
            catch { }
        }

        private static void OnDismissedStatic(long toastId, int reason)
        {
            try
            {
                if (_live.TryGetValue(toastId, out var bridge))
                    bridge._handler?.OnDismissed(toastId, MapDismissReason(reason));
            }
            catch { }
            finally { Release(toastId); }
        }

        private static void OnFailedStatic(long toastId)
        {
            try
            {
                if (_live.TryGetValue(toastId, out var bridge))
                    bridge._handler?.OnFailed(toastId);
            }
            catch { }
            finally { Release(toastId); }
        }

        private static DismissReason MapDismissReason(int native)
        {
            // WinToastDismissalReason: 0=UserCancelled, 1=ApplicationHidden, 2=TimedOut
            switch (native)
            {
                case 0:  return DismissReason.UserCancelled;
                case 1:  return DismissReason.ApplicationHidden;
                case 2:  return DismissReason.TimedOut;
                default: return DismissReason.Unknown;
            }
        }
    }
}
