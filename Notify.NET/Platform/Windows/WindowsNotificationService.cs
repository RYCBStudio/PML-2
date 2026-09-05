using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Notify.NET.Abstractions;
using Notify.NET.Exceptions;

namespace Notify.NET.Platform.Windows
{
    /// <summary>
    /// <see cref="INotificationService"/> implementation backed by WinToastLib via a thin
    /// native C wrapper DLL (<c>WinToastWrapper.dll</c>).
    ///
    /// Threading model:
    ///   WinRT toast APIs require a Single-Threaded Apartment (STA). This service owns a
    ///   dedicated STA background thread that runs a Win32 message pump. All P/Invoke calls
    ///   are marshalled onto that thread via a work-item queue. Callbacks from WinToast arrive
    ///   on a WinRT thread-pool thread (NOT the STA thread) and are safe to dispatch directly.
    /// </summary>
    public sealed partial class WindowsNotificationService : INotificationService
    {
        private readonly string _appName;
        private readonly string _appUserModelId;
        private readonly string? _appIconPath;

        private readonly Thread _staThread;
        private readonly BlockingCollection<Action> _workQueue = new BlockingCollection<Action>();
        private readonly ManualResetEventSlim _initialised = new ManualResetEventSlim(false);
        private volatile bool _isSupported;
        private volatile bool _disposed;
        private Exception? _initException;

        /// <inheritdoc/>
        public bool IsSupported => _isSupported;

        /// <param name="appName">Human-readable application name (shown in Action Centre).</param>
        /// <param name="appUserModelId">
        /// Your application's AppUserModelId, e.g. <c>"MyCompany.MyApp"</c>.
        /// A Start-Menu shortcut carrying this AUMI is required for notifications to persist in
        /// the Action Centre. The native wrapper creates the shortcut automatically when missing.
        /// </param>
        /// <param name="appIconPath">
        /// Optional absolute path to an .ico (or .exe/.dll) file whose first icon is used as the
        /// small icon in the top-left corner of every toast notification from this app.
        /// Pass null to use the host executable's default icon.
        /// </param>
        public WindowsNotificationService(string appName, string appUserModelId, string? appIconPath = null)
        {
            _appName = appName ?? throw new ArgumentNullException(nameof(appName));
            _appUserModelId = appUserModelId ?? throw new ArgumentNullException(nameof(appUserModelId));
            _appIconPath = appIconPath;

            _staThread = new Thread(StaThreadProc)
            {
                Name = "Notify.NET STA",
                IsBackground = true
            };
            _staThread.SetApartmentState(ApartmentState.STA);
            _staThread.Start();

            // Block until the STA thread has finished initialising (or failed).
            _initialised.Wait();
            if (_initException != null)
                throw new NotificationException("WinToastLib initialisation failed.", _initException);
        }

        // ------------------------------------------------------------------
        // INotificationService
        // ------------------------------------------------------------------

        /// <inheritdoc/>
        public Task<long> ShowAsync(NotificationRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            ThrowIfDisposedOrUnsupported();

            var tcs = new TaskCompletionSource<long>();
            cancellationToken.Register(() => tcs.TrySetCanceled());

            EnqueueOnSta(() =>
            {
                try
                {
                    long id = ShowOnSta(request);
                    tcs.TrySetResult(id);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            return tcs.Task;
        }

        /// <inheritdoc/>
        public Task HideAsync(long notificationId, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposedOrUnsupported();

            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(() => tcs.TrySetCanceled());

            EnqueueOnSta(() =>
            {
                try
                {
                    bool ok = WinToastNative.WNT_HideToast(notificationId);
                    if (!ok)
                        tcs.TrySetException(new NotificationException($"WNT_HideToast failed for id {notificationId}."));
                    else
                    {
                        WinToastHandlerBridge.Release(notificationId);
                        tcs.TrySetResult(true);
                    }
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            return tcs.Task;
        }

        // ------------------------------------------------------------------
        // IDisposable
        // ------------------------------------------------------------------

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Signal the STA thread to shut down by completing the queue.
            _workQueue.CompleteAdding();

            // Wait for the STA thread to finish its message pump and uninitialise.
            if (_staThread.IsAlive)
                _staThread.Join(TimeSpan.FromSeconds(5));

            _workQueue.Dispose();
            _initialised.Dispose();
        }

        // ------------------------------------------------------------------
        // Private helpers
        // ------------------------------------------------------------------

        private void ThrowIfDisposedOrUnsupported()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(WindowsNotificationService));
            if (!_isSupported) throw new Exceptions.PlatformNotSupportedException();
        }

        private void EnqueueOnSta(Action action)
        {
            try { _workQueue.Add(action); }
            catch (InvalidOperationException) { /* queue completed — service is disposed */ }
        }

        /// <summary>
        /// The STA thread entry point. Runs a simple work-item loop as the message pump.
        /// </summary>
        private void StaThreadProc()
        {
            try
            {
                NativeLibraryLoader.EnsureLoaded();

                if (!WinToastNative.WNT_IsCompatible())
                {
                    _isSupported = false;
                    _initialised.Set();
                    return;
                }

                bool ok = WinToastNative.WNT_Initialize(_appName, _appUserModelId, _appIconPath);
                if (!ok)
                {
                    _initException = new NotificationException("WNT_Initialize returned false.");
                    _isSupported = false;
                    _initialised.Set();
                    return;
                }

                _isSupported = true;
                _initialised.Set();

                // Process work items until Dispose() calls CompleteAdding().
                foreach (Action work in _workQueue.GetConsumingEnumerable())
                {
                    // Pump pending Windows messages between work items so WinRT callbacks
                    // can be delivered to the STA message queue.
                    PumpMessages();
                    work();
                    PumpMessages();
                }
            }
            catch (Exception ex)
            {
                _initException = ex;
                _isSupported = false;
                _initialised.Set();
            }
            finally
            {
                // Drain any remaining messages before uninitialising.
                PumpMessages();
                if (_isSupported)
                    WinToastNative.WNT_Uninitialize();
            }
        }

        private long ShowOnSta(NotificationRequest request)
        {
            // We need to pass wchar_t* pointers to the native layer.
            // Pin managed strings as unmanaged UTF-16 memory for the duration of the call.
            // button label pointers are pinned in the IntPtr[] and that array is pinned too.

            using var titlePin         = new PinnedString(request.Title);
            using var bodyPin          = new PinnedString(request.Body);
            using var imagePin         = new PinnedString(ResolveImagePath(request.ImagePath));
            using var heroImagePin     = new PinnedString(ResolveImagePath(request.HeroImagePath));
            using var inlineImagePin   = new PinnedString(ResolveImagePath(request.InlineImagePath));
            using var attributionPin   = new PinnedString(request.AttributionText);
            using var customAudioPin   = new PinnedString(request.CustomAudioPath);

            // Build array of pinned button label pointers.
            var buttonPins   = new PinnedString[request.Buttons.Count];
            var buttonPtrs   = new IntPtr[request.Buttons.Count];
            for (int i = 0; i < request.Buttons.Count; i++)
            {
                buttonPins[i] = new PinnedString(request.Buttons[i].Label);
                buttonPtrs[i] = buttonPins[i].Pointer;
            }

            try
            {
                // Pin the button pointer array itself.
                GCHandle buttonArrayHandle = default;
                IntPtr buttonArrayPtr = IntPtr.Zero;

                if (buttonPtrs.Length > 0)
                {
                    buttonArrayHandle = GCHandle.Alloc(buttonPtrs, GCHandleType.Pinned);
                    buttonArrayPtr = buttonArrayHandle.AddrOfPinnedObject();
                }

                var descriptor = new WinToastNative.WNT_ToastDescriptor
                {
                    title           = titlePin.Pointer,
                    body            = bodyPin.Pointer,
                    imagePath       = imagePin.Pointer,
                    heroImagePath   = heroImagePin.Pointer,
                    buttonLabels    = buttonArrayPtr,
                    buttonCount     = request.Buttons.Count,
                    expirationMs    = request.Expiration.HasValue ? (long)request.Expiration.Value.TotalMilliseconds : 0L,
                    scenario        = MapScenario(request.Urgency),
                    audioOption     = MapAudio(request.Audio),
                    inlineImagePath = inlineImagePin.Pointer,
                    attributionText = attributionPin.Pointer,
                    customAudioPath = customAudioPin.Pointer,
                    cropHint        = request.ImageCropHint == NotificationImageCropHint.Circle
                                          ? WinToastNative.WNT_CROP_HINT_CIRCLE
                                          : WinToastNative.WNT_CROP_HINT_SQUARE,
                    audioFile       = request.AudioFile.HasValue
                                          ? (int)request.AudioFile.Value
                                          : WinToastNative.WNT_AUDIO_FILE_NONE
                };

                var handler = new WinToastNative.WNT_Handler
                {
                    onActivated       = WinToastHandlerBridge.PtrActivated,
                    onButtonActivated = WinToastHandlerBridge.PtrButtonActivated,
                    onDismissed       = WinToastHandlerBridge.PtrDismissed,
                    onFailed          = WinToastHandlerBridge.PtrFailed
                };

                long toastId = WinToastNative.WNT_ShowToast(ref descriptor, ref handler);

                if (buttonArrayHandle.IsAllocated)
                    buttonArrayHandle.Free();

                if (toastId < 0)
                    throw new NotificationException($"WNT_ShowToast failed with error code {toastId}.", (int)toastId);

                // Register the per-notification bridge BEFORE any callback can fire.
                WinToastHandlerBridge.Register(toastId, BuildCompositeHandler(request));

                return toastId;
            }
            finally
            {
                foreach (var pin in buttonPins)
                    pin.Dispose();
            }
        }

        /// <summary>
        /// Builds an <see cref="INotificationHandler"/> that combines the request-level handler
        /// with per-button callbacks defined on each <see cref="Builder.NotificationButton"/>.
        /// </summary>
        private static INotificationHandler? BuildCompositeHandler(NotificationRequest request)
        {
            bool hasButtonCallbacks = false;
            foreach (var btn in request.Buttons)
                if (btn.Callback != null) { hasButtonCallbacks = true; break; }

            if (!hasButtonCallbacks)
                return request.Handler;

            return new CompositeHandler(request.Handler, request.Buttons);
        }

        private static int MapScenario(NotificationUrgency urgency)
        {
            switch (urgency)
            {
                case NotificationUrgency.Alarm:    return WinToastNative.WNT_SCENARIO_ALARM;
                case NotificationUrgency.Reminder: return WinToastNative.WNT_SCENARIO_REMINDER;
                default:                           return WinToastNative.WNT_SCENARIO_DEFAULT;
            }
        }

        private static int MapAudio(NotificationAudio audio)
        {
            switch (audio)
            {
                case NotificationAudio.Silent: return WinToastNative.WNT_AUDIO_SILENT;
                case NotificationAudio.Loop:   return WinToastNative.WNT_AUDIO_LOOP;
                default:                       return WinToastNative.WNT_AUDIO_DEFAULT;
            }
        }

        /// <summary>Pumps pending Win32/WinRT messages on the STA thread.</summary>
        private static void PumpMessages()
        {
            NativeMessage msg;
            while (PeekMessageW(out msg, IntPtr.Zero, 0, 0, 0x0001 /* PM_REMOVE */))
            {
                TranslateMessage(ref msg);
                DispatchMessageW(ref msg);
            }
        }

        [DllImport("user32.dll")] private static extern bool PeekMessageW(out NativeMessage msg, IntPtr hwnd, uint min, uint max, uint remove);
        [DllImport("user32.dll")] private static extern bool TranslateMessage(ref NativeMessage msg);
        [LibraryImport("user32.dll")] private static partial IntPtr DispatchMessageW(ref NativeMessage msg);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeMessage
        {
            public IntPtr hwnd;
            public uint   msg;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint   time;
            public int    ptX;
            public int    ptY;
        }

        // ------------------------------------------------------------------
        // Inner helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// Resolves a (possibly relative) image path to an absolute path and verifies
        /// the file exists. Returns null if the path is empty, unresolvable, or the
        /// file is not found — causing the native layer to skip the image rather than crash.
        /// WinToastLib embeds the path verbatim into toast XML; it cannot handle
        /// relative paths or missing files gracefully.
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

        /// <summary>
        /// Copies a .NET string into unmanaged UTF-16 memory so it can be passed as
        /// a wchar_t* to native code. The memory is freed on Dispose.
        /// </summary>
        private sealed class PinnedString : IDisposable
        {
            public IntPtr Pointer { get; }

            public PinnedString(string? value)
            {
                Pointer = value != null
                    ? Marshal.StringToHGlobalUni(value)
                    : IntPtr.Zero;
            }

            public void Dispose()
            {
                if (Pointer != IntPtr.Zero)
                    Marshal.FreeHGlobal(Pointer);
            }
        }

        /// <summary>
        /// Combines a top-level <see cref="INotificationHandler"/> with per-button callbacks.
        /// </summary>
        private sealed class CompositeHandler : INotificationHandler
        {
            private readonly INotificationHandler? _inner;
            private readonly System.Collections.Generic.IReadOnlyList<Builder.NotificationButton> _buttons;

            public CompositeHandler(INotificationHandler? inner,
                System.Collections.Generic.IReadOnlyList<Builder.NotificationButton> buttons)
            {
                _inner = inner;
                _buttons = buttons;
            }

            public void OnActivated(long id) => _inner?.OnActivated(id);

            public void OnButtonActivated(long id, int index)
            {
                if (index >= 0 && index < _buttons.Count)
                    _buttons[index].Callback?.Invoke(id);
                _inner?.OnButtonActivated(id, index);
            }

            public void OnDismissed(long id, DismissReason reason) => _inner?.OnDismissed(id, reason);
            public void OnFailed(long id) => _inner?.OnFailed(id);
        }
    }
}
