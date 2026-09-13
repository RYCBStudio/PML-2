using System;
using Notify.NET.Abstractions;

namespace Notify.NET.Platform.Linux
{
    /// <summary>
    /// <see cref="ITaskbarProgressService"/> implementation that drives launcher/taskbar progress
    /// via the Unity LauncherEntry D-Bus API (<c>com.canonical.Unity.LauncherEntry</c>). This is
    /// honoured by KDE Plasma, Unity, Dash-to-Dock, Plank and Latte.
    ///
    /// The signal is broadcast on the session bus and carries the app's <c>application://&lt;id&gt;.desktop</c>
    /// URI plus a property dictionary (<c>progress</c>, <c>progress-visible</c>, <c>urgent</c>). The
    /// app must therefore ship a matching <c>.desktop</c> file for any dock to display the bar.
    ///
    /// LauncherEntry has no indeterminate mode and cannot tint the bar, so
    /// <see cref="TaskbarProgressState.Indeterminate"/> renders as an empty (0%) bar and only
    /// <see cref="TaskbarProgressState.Error"/> is distinguished (via the "urgent" flag).
    /// </summary>
    public sealed class LinuxTaskbarProgressService : ITaskbarProgressService
    {
        private const string ObjectPath    = "/com/canonical/Unity/LauncherEntry";
        private const string InterfaceName = "com.canonical.Unity.LauncherEntry";
        private const string SignalName    = "Update";

        private readonly object _lock = new object();
        private readonly string _appUri;
        private IntPtr _connection;

        private TaskbarProgressState _state = TaskbarProgressState.None;
        private double _progress;
        private volatile bool _disposed;

        /// <inheritdoc/>
        public bool IsSupported { get; private set; }

        /// <param name="desktopFileId">
        /// The application's .desktop file id (with or without the ".desktop" suffix), e.g.
        /// <c>"myapp"</c> or <c>"com.example.MyApp.desktop"</c>. Used to build the
        /// <c>application://&lt;id&gt;.desktop</c> URI the launcher matches against.
        /// </param>
        public LinuxTaskbarProgressService(string desktopFileId)
        {
            if (desktopFileId == null) throw new ArgumentNullException(nameof(desktopFileId));

            string id = desktopFileId.EndsWith(".desktop", StringComparison.Ordinal)
                ? desktopFileId
                : desktopFileId + ".desktop";
            _appUri = "application://" + id;

            try
            {
                IntPtr error = IntPtr.Zero;
                _connection = GioDBusNative.g_bus_get_sync(GioDBusNative.G_BUS_TYPE_SESSION, IntPtr.Zero, ref error);

                if (_connection == IntPtr.Zero || error != IntPtr.Zero)
                {
                    if (error != IntPtr.Zero) GioDBusNative.g_error_free(error);
                    IsSupported = false;
                }
                else
                {
                    IsSupported = true;
                }
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
        public void SetWindow(IntPtr windowHandle) { /* Windows-only concept; no-op on Linux. */ }

        /// <inheritdoc/>
        public void SetState(TaskbarProgressState state)
        {
            if (_disposed || !IsSupported) return;
            lock (_lock)
            {
                _state = state;
                if (state == TaskbarProgressState.None) _progress = 0;
                Emit();
            }
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
            double clamped = fraction < 0 ? 0 : (fraction > 1 ? 1 : fraction);
            lock (_lock)
            {
                _progress = clamped;
                if (_state != TaskbarProgressState.Error && _state != TaskbarProgressState.Paused)
                    _state = TaskbarProgressState.Normal;
                Emit();
            }
        }

        // ------------------------------------------------------------------
        // IDisposable
        // ------------------------------------------------------------------

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            lock (_lock)
            {
                if (IsSupported && _connection != IntPtr.Zero)
                {
                    // Clear the bar, then release the connection.
                    _state = TaskbarProgressState.None;
                    _progress = 0;
                    try { Emit(); } catch { /* best effort */ }

                    GioDBusNative.g_object_unref(_connection);
                    _connection = IntPtr.Zero;
                }
            }
        }

        // ------------------------------------------------------------------
        // Private helpers
        // ------------------------------------------------------------------

        /// <summary>Builds and broadcasts the LauncherEntry "Update" signal for the current state.</summary>
        private void Emit()
        {
            bool   visible  = _state != TaskbarProgressState.None;
            bool   urgent   = _state == TaskbarProgressState.Error;
            double progress = _state == TaskbarProgressState.Indeterminate ? 0.0 : _progress;

            // Build the a{sv} property dictionary.
            IntPtr[] entries =
            {
                DictEntry("progress",         GioDBusNative.g_variant_new_double(progress)),
                DictEntry("progress-visible", GioDBusNative.g_variant_new_boolean(visible ? 1 : 0)),
                DictEntry("urgent",           GioDBusNative.g_variant_new_boolean(urgent ? 1 : 0))
            };

            IntPtr dict = GioDBusNative.g_variant_new_array(IntPtr.Zero, entries, (UIntPtr)entries.Length);

            // Build the (s a{sv}) tuple.
            IntPtr[] tupleChildren = { GioDBusNative.g_variant_new_string(_appUri), dict };
            IntPtr parameters = GioDBusNative.g_variant_new_tuple(tupleChildren, (UIntPtr)tupleChildren.Length);

            IntPtr error = IntPtr.Zero;
            GioDBusNative.g_dbus_connection_emit_signal(
                _connection, null, ObjectPath, InterfaceName, SignalName, parameters, ref error);

            if (error != IntPtr.Zero)
            {
                GioDBusNative.g_error_free(error);
                return;
            }

            IntPtr flushError = IntPtr.Zero;
            GioDBusNative.g_dbus_connection_flush_sync(_connection, IntPtr.Zero, ref flushError);
            if (flushError != IntPtr.Zero) GioDBusNative.g_error_free(flushError);
        }

        /// <summary>Creates a "{sv}" dict entry, boxing <paramref name="value"/> in a variant.</summary>
        private static IntPtr DictEntry(string key, IntPtr value)
        {
            IntPtr keyVariant   = GioDBusNative.g_variant_new_string(key);
            IntPtr boxedValue   = GioDBusNative.g_variant_new_variant(value);
            return GioDBusNative.g_variant_new_dict_entry(keyVariant, boxedValue);
        }
    }
}
