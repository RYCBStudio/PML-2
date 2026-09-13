using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Notify.NET.Abstractions;
using Notify.NET.Exceptions;

namespace Notify.NET.Platform.MacOS
{
    /// <summary>
    /// <see cref="INotificationService"/> implementation backed by macOS
    /// <c>UNUserNotificationCenter</c> (macOS 10.14+) via a thin native Objective-C
    /// wrapper (<c>libMacNotifyWrapper.dylib</c>).
    ///
    /// Threading model:
    ///   <c>UNUserNotificationCenter</c> is internally thread-safe; all P/Invoke calls
    ///   may be made from any thread. Callbacks arrive on a background GCD thread managed
    ///   by the framework; consumers are responsible for marshalling to a UI thread if
    ///   required.
    ///
    /// macOS-specific behaviour:
    ///   - A user authorisation prompt is shown on the first call (Alert + Sound + Badge).
    ///   - Body-tap and button-tap are terminal events; <c>onDismissed</c> is NOT fired
    ///     after an action response (unlike Windows where WinToastLib always fires it).
    ///   - Auto-expiry (timed out) does not produce a callback — macOS does not expose
    ///     this event to <c>UNUserNotificationCenterDelegate</c>.
    ///   - Non-bundled processes (e.g. bare dotnet CLI) receive the banner but may not
    ///     receive action callbacks depending on the OS version and app entitlements.
    /// </summary>
    public sealed class MacOSNotificationService : INotificationService
    {
        private volatile bool _disposed;

        /// <inheritdoc/>
        public bool IsSupported { get; private set; }

        /// <param name="appName">
        /// Application name used for logging. The OS uses the bundle identifier for
        /// notification attribution; pass a descriptive name for diagnostic purposes.
        /// </param>
        public MacOSNotificationService(string appName)
        {
            if (appName == null) throw new ArgumentNullException(nameof(appName));

            try
            {
                MacOSNativeLibraryLoader.EnsureLoaded();

                if (!MacNotifyNative.MNW_IsSupported())
                {
                    IsSupported = false;
                    return;
                }

                IsSupported = MacNotifyNative.MNW_Initialize(appName);
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
        public Task<long> ShowAsync(NotificationRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            ThrowIfDisposedOrUnsupported();
            cancellationToken.ThrowIfCancellationRequested();

            long notifId = ShowNative(request);
            return Task.FromResult(notifId);
        }

        /// <inheritdoc/>
        public Task HideAsync(long notificationId,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposedOrUnsupported();
            cancellationToken.ThrowIfCancellationRequested();

            bool ok = MacNotifyNative.MNW_HideNotification(notificationId);

            // MNW_HideNotification fires onDismissed synchronously via the native
            // layer; release the managed bridge entry too.
            MacNotifyCallbackBridge.Release(notificationId);

            if (!ok)
                throw new NotificationException(
                    $"MNW_HideNotification failed for id {notificationId}.");

            return Task.CompletedTask;
        }

        // ------------------------------------------------------------------
        // IDisposable
        // ------------------------------------------------------------------

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (IsSupported)
                MacNotifyNative.MNW_Uninitialize();
        }

        // ------------------------------------------------------------------
        // Private helpers
        // ------------------------------------------------------------------

        private void ThrowIfDisposedOrUnsupported()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MacOSNotificationService));
            if (!IsSupported) throw new Exceptions.PlatformNotSupportedException();
        }

        private static long ShowNative(NotificationRequest request)
        {
            // Marshal all strings to unmanaged UTF-8 memory for the duration of the call.
            // Marshal.StringToHGlobalAnsi uses the system ANSI encoding; on macOS that is UTF-8.
            using var titlePin   = new PinnedStringAnsi(request.Title);
            using var bodyPin    = new PinnedStringAnsi(request.Body);
            using var imagePin   = new PinnedStringAnsi(ResolveImagePath(request.ImagePath));

            // Build array of pinned button label pointers.
            int btnCount = request.Buttons.Count;
            var btnPins  = new PinnedStringAnsi[btnCount];
            var btnPtrs  = new IntPtr[btnCount];

            for (int i = 0; i < btnCount; i++)
            {
                btnPins[i] = new PinnedStringAnsi(request.Buttons[i].Label);
                btnPtrs[i] = btnPins[i].Pointer;
            }

            try
            {
                // Pin the button pointer array so its address is stable during the call.
                GCHandle btnArrayHandle = default;
                IntPtr   btnArrayPtr    = IntPtr.Zero;

                if (btnCount > 0)
                {
                    btnArrayHandle = GCHandle.Alloc(btnPtrs, GCHandleType.Pinned);
                    btnArrayPtr    = btnArrayHandle.AddrOfPinnedObject();
                }

                var descriptor = new MacNotifyNative.MNW_NotificationDescriptor
                {
                    title              = titlePin.Pointer,
                    body               = bodyPin.Pointer,
                    imagePath          = imagePin.Pointer,
                    buttonLabels       = btnArrayPtr,
                    buttonCount        = btnCount,
                    expirationMs       = request.Expiration.HasValue
                                            ? (long)request.Expiration.Value.TotalMilliseconds
                                            : 0L,
                    audioOption        = MapAudio(request.Audio),
                    interruptionLevel  = MapInterruptionLevel(request.Urgency)
                };

                var handler = new MacNotifyNative.MNW_Handler
                {
                    onActivated       = MacNotifyCallbackBridge.PtrActivated,
                    onButtonActivated = MacNotifyCallbackBridge.PtrButtonActivated,
                    onDismissed       = MacNotifyCallbackBridge.PtrDismissed,
                    onFailed          = MacNotifyCallbackBridge.PtrFailed
                };

                long notifId = MacNotifyNative.MNW_ShowNotification(ref descriptor, ref handler);

                if (btnArrayHandle.IsAllocated)
                    btnArrayHandle.Free();

                if (notifId < 0)
                    throw new NotificationException(
                        $"MNW_ShowNotification failed with code {notifId}.");

                // Register managed bridge before any callback can fire.
                MacNotifyCallbackBridge.Register(notifId, BuildCompositeHandler(request));

                return notifId;
            }
            finally
            {
                foreach (var pin in btnPins)
                    pin.Dispose();
            }
        }

        private static INotificationHandler? BuildCompositeHandler(NotificationRequest request)
        {
            bool hasButtonCallbacks = false;
            foreach (var btn in request.Buttons)
                if (btn.Callback != null) { hasButtonCallbacks = true; break; }

            if (!hasButtonCallbacks)
                return request.Handler;

            return new CompositeHandler(request.Handler, request.Buttons);
        }

        private static int MapAudio(NotificationAudio audio)
        {
            return audio == NotificationAudio.Silent
                ? 1 /* MNW_AUDIO_SILENT */
                : 0 /* MNW_AUDIO_DEFAULT */;
        }

        private static int MapInterruptionLevel(NotificationUrgency urgency)
        {
            switch (urgency)
            {
                case NotificationUrgency.Low:      return 1; // MNW_INTERRUPTION_PASSIVE
                case NotificationUrgency.Critical:
                case NotificationUrgency.Alarm:    return 3; // MNW_INTERRUPTION_CRITICAL
                default:                           return 0; // MNW_INTERRUPTION_ACTIVE
            }
        }

        /// <summary>
        /// Resolves a (possibly relative) image path to an absolute path and verifies the file
        /// exists. Returns null if the path is empty, unresolvable, or missing so that the
        /// native layer skips the attachment rather than failing the whole notification.
        /// </summary>
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

        // ------------------------------------------------------------------
        // Inner helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// Copies a .NET string to unmanaged ANSI (UTF-8 on macOS) memory.
        /// The allocation is freed on <see cref="Dispose"/>.
        /// </summary>
        private sealed class PinnedStringAnsi : IDisposable
        {
            public IntPtr Pointer { get; }

            public PinnedStringAnsi(string? value)
            {
                Pointer = value != null
                    ? Marshal.StringToHGlobalAnsi(value)
                    : IntPtr.Zero;
            }

            public void Dispose()
            {
                if (Pointer != IntPtr.Zero)
                    Marshal.FreeHGlobal(Pointer);
            }
        }

        /// <summary>
        /// Combines a top-level <see cref="INotificationHandler"/> with per-button callbacks
        /// stored in <see cref="Builder.NotificationButton.Callback"/>.
        /// </summary>
        private sealed class CompositeHandler : INotificationHandler
        {
            private readonly INotificationHandler? _inner;
            private readonly System.Collections.Generic.IReadOnlyList<Builder.NotificationButton> _buttons;

            public CompositeHandler(
                INotificationHandler? inner,
                System.Collections.Generic.IReadOnlyList<Builder.NotificationButton> buttons)
            {
                _inner   = inner;
                _buttons = buttons;
            }

            public void OnActivated(long id) => _inner?.OnActivated(id);

            public void OnButtonActivated(long id, int index)
            {
                if (index >= 0 && index < _buttons.Count)
                    _buttons[index].Callback?.Invoke(id);
                _inner?.OnButtonActivated(id, index);
            }

            public void OnDismissed(long id, DismissReason reason) =>
                _inner?.OnDismissed(id, reason);

            public void OnFailed(long id) => _inner?.OnFailed(id);
        }
    }
}
