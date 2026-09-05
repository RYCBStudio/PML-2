using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Notify.NET.Abstractions;

namespace Notify.NET.Platform.MacOS
{
    /// <summary>
    /// <see cref="IJumpListService"/> implementation backed by the application's macOS Dock menu
    /// (shown on right-click / click-and-hold of the Dock icon), provided through the native
    /// <c>libMacNotifyWrapper.dylib</c> (<c>MNW_SetDockMenu</c> and friends).
    ///
    /// Unlike the Windows and Linux services there is no relaunch and no single-instance
    /// forwarding: clicking a Dock-menu item fires <see cref="IJumpListHandler.OnTaskActivated"/>
    /// live in the running process. Consequently <see cref="TryHandleActivation"/> never matches
    /// — there is no activation command line on macOS.
    ///
    /// The Dock menu is only effective for a regular bundled GUI application with a running main
    /// loop; a bare console process has no Dock menu and the native calls are harmless no-ops.
    /// </summary>
    public sealed class MacOSJumpListService : IJumpListService
    {
        // A single static delegate kept alive for the whole process so the native side always has
        // a valid function pointer to invoke (mirrors MacNotifyCallbackBridge).
        private static readonly MacJumpListNative.DockMenuCallback _staticCallback;

        private static IJumpListHandler? _handler;
        private static readonly object _handlerGate = new object();

        private volatile bool _disposed;

        /// <inheritdoc/>
        public bool IsSupported { get; }

        static MacOSJumpListService()
        {
            _staticCallback = OnDockItemActivated;
        }

        public MacOSJumpListService()
        {
            try
            {
                MacOSNativeLibraryLoader.EnsureLoaded();
                IsSupported = MacJumpListNative.MNW_IsSupported();
            }
            catch (DllNotFoundException)
            {
                IsSupported = false;
            }
        }

        // ------------------------------------------------------------------
        // IJumpListService
        // ------------------------------------------------------------------

        /// <inheritdoc/>
        public bool TryHandleActivation(string[] args)
        {
            // macOS dock-menu clicks are delivered live in-process; there is no relaunch with an
            // activation command line to handle.
            return false;
        }

        /// <inheritdoc/>
        public void SetHandler(IJumpListHandler? handler)
        {
            if (_disposed || !IsSupported) return;

            lock (_handlerGate) _handler = handler;

            // Register (or clear) the native callback only when a handler is actually attached,
            // honouring the "don't register unless used" requirement.
            MacJumpListNative.MNW_SetDockMenuHandler(handler != null ? _staticCallback : null);
        }

        /// <inheritdoc/>
        public void SetTasks(IEnumerable<JumpListTask> tasks)
        {
            if (_disposed || !IsSupported) return;
            if (tasks == null) throw new ArgumentNullException(nameof(tasks));

            var list = new List<JumpListTask>(tasks);
            if (list.Count == 0)
            {
                MacJumpListNative.MNW_ClearDockMenu();
                return;
            }

            var ids = new string[list.Count];
            var titles = new string[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                ids[i] = list[i].Id;
                titles[i] = list[i].Title;
            }

            MacJumpListNative.MNW_SetDockMenu(ids, titles, list.Count);
        }

        /// <inheritdoc/>
        public void ClearTasks()
        {
            if (_disposed || !IsSupported) return;
            MacJumpListNative.MNW_ClearDockMenu();
        }

        // ------------------------------------------------------------------
        // IDisposable
        // ------------------------------------------------------------------

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (!IsSupported) return;
            try
            {
                MacJumpListNative.MNW_ClearDockMenu();
                MacJumpListNative.MNW_SetDockMenuHandler(null);
            }
            catch { /* best effort */ }

            lock (_handlerGate) _handler = null;
        }

        // ------------------------------------------------------------------
        // Native callback routing — invoked on the main thread from AppKit
        // ------------------------------------------------------------------

        private static void OnDockItemActivated(string taskId)
        {
            IJumpListHandler? handler;
            lock (_handlerGate) handler = _handler;

            try { handler?.OnTaskActivated(taskId); }
            catch { /* a handler exception must never propagate into native code */ }
        }
    }
}
