using System;
using Notify.NET.Abstractions;

namespace Notify.NET.Platform.MacOS
{
    /// <summary>
    /// <see cref="ITaskbarProgressService"/> implementation that draws an
    /// <c>NSProgressIndicator</c> on the application's Dock tile via the native
    /// <c>libMacNotifyWrapper.dylib</c> (<c>MNW_SetTaskbarProgress</c>).
    ///
    /// The Dock tile is only present for a regular GUI/bundled application whose main run loop
    /// is running. A bare console process has no Dock tile, so the calls are harmless no-ops in
    /// that case. The Dock cannot tint the bar, so <see cref="TaskbarProgressState.Paused"/> and
    /// <see cref="TaskbarProgressState.Error"/> render the same as <see cref="TaskbarProgressState.Normal"/>.
    /// </summary>
    public sealed class MacOSTaskbarProgressService : ITaskbarProgressService
    {
        private TaskbarProgressState _state = TaskbarProgressState.None;
        private double _progress;
        private volatile bool _disposed;

        /// <inheritdoc/>
        public bool IsSupported { get; private set; }

        public MacOSTaskbarProgressService()
        {
            try
            {
                MacOSNativeLibraryLoader.EnsureLoaded();
                IsSupported = true;
            }
            catch (DllNotFoundException)
            {
                IsSupported = false;
            }
        }

        // ------------------------------------------------------------------
        // ITaskbarProgressService
        // ------------------------------------------------------------------

        /// <inheritdoc/>
        public void SetWindow(IntPtr windowHandle) { /* Windows-only concept; no-op on macOS. */ }

        /// <inheritdoc/>
        public void SetState(TaskbarProgressState state)
        {
            if (_disposed || !IsSupported) return;
            _state = state;
            if (state == TaskbarProgressState.None) _progress = 0;
            Apply();
        }

        /// <inheritdoc/>
        public void SetProgress(ulong completed, ulong total)
        {
            if (_disposed || !IsSupported) return;
            if (total == 0) throw new ArgumentOutOfRangeException(nameof(total), "Total must be greater than zero.");
            SetProgress((double)completed / total);
        }

        /// <inheritdoc/>
        public void SetProgress(double fraction)
        {
            if (_disposed || !IsSupported) return;
            _progress = fraction < 0 ? 0 : (fraction > 1 ? 1 : fraction);
            if (_state != TaskbarProgressState.Error && _state != TaskbarProgressState.Paused)
                _state = TaskbarProgressState.Normal;
            Apply();
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
            {
                _state = TaskbarProgressState.None;
                _progress = 0;
                try { Apply(); } catch { /* best effort */ }
            }
        }

        // ------------------------------------------------------------------
        // Private helpers
        // ------------------------------------------------------------------

        private void Apply()
        {
            MacNotifyNative.MNW_SetTaskbarProgress(MapState(_state), _progress);
        }

        private static int MapState(TaskbarProgressState state)
        {
            switch (state)
            {
                case TaskbarProgressState.Indeterminate: return MacNotifyNative.MNW_PROGRESS_INDETERMINATE;
                case TaskbarProgressState.Normal:        return MacNotifyNative.MNW_PROGRESS_NORMAL;
                case TaskbarProgressState.Paused:        return MacNotifyNative.MNW_PROGRESS_PAUSED;
                case TaskbarProgressState.Error:         return MacNotifyNative.MNW_PROGRESS_ERROR;
                default:                                 return MacNotifyNative.MNW_PROGRESS_NONE;
            }
        }
    }
}
