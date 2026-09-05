using System;
using System.Runtime.InteropServices;

namespace Notify.NET.Platform.Linux
{
    /// <summary>
    /// P/Invoke declarations for GIO/GLib functions used to emit the Unity LauncherEntry
    /// <c>Update</c> D-Bus signal that drives launcher/taskbar progress on Linux.
    ///
    /// The GVariant payload is built entirely from the non-variadic constructor functions
    /// (<c>g_variant_new_string</c>, <c>_double</c>, <c>_boolean</c>, <c>_variant</c>,
    /// <c>_dict_entry</c>, <c>_array</c>, <c>_tuple</c>) so that no <c>g_variant_new</c>
    /// varargs call — which cannot be marshalled via P/Invoke — is required. Each helper
    /// consumes the floating reference of its children, and
    /// <c>g_dbus_connection_emit_signal</c> sinks the final floating tuple, so no manual
    /// unref of the GVariants is needed.
    /// </summary>
    internal static partial class GioDBusNative
    {
        private const string LibGio  = "libgio-2.0.so.0";
        private const string LibGLib = "libglib-2.0.so.0";

        /// <summary>GBusType.G_BUS_TYPE_SESSION.</summary>
        internal const int G_BUS_TYPE_SESSION = 2;

        // -------------------------------------------------------------------------
        // GIO — session bus + signal emission
        // -------------------------------------------------------------------------

        /// <summary>
        /// Synchronously connects to a message bus. Returns a GDBusConnection* (a GObject
        /// reference owned by the caller) or IntPtr.Zero on failure.
        /// </summary>
        [LibraryImport(LibGio, EntryPoint = "g_bus_get_sync")]
        internal static partial IntPtr g_bus_get_sync(int busType, IntPtr cancellable, ref IntPtr error);

        /// <summary>
        /// Emits a D-Bus signal on the given connection. <paramref name="parameters"/> must be a
        /// tuple GVariant (its floating reference is sunk by this call).
        /// </summary>
        [LibraryImport(LibGio, EntryPoint = "g_dbus_connection_emit_signal", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool g_dbus_connection_emit_signal(
            IntPtr connection,
            string? destinationBusName,
            string objectPath,
            string interfaceName,
            string signalName,
            IntPtr parameters,
            ref IntPtr error);

        /// <summary>Synchronously flushes queued outgoing messages on the connection.</summary>
        [LibraryImport(LibGio, EntryPoint = "g_dbus_connection_flush_sync")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool g_dbus_connection_flush_sync(
            IntPtr connection, IntPtr cancellable, ref IntPtr error);

        // -------------------------------------------------------------------------
        // GLib — GVariant constructors (all return floating references)
        // -------------------------------------------------------------------------

        [LibraryImport(LibGLib, EntryPoint = "g_variant_new_string", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
        internal static partial IntPtr g_variant_new_string(string value);

        [LibraryImport(LibGLib, EntryPoint = "g_variant_new_double")]
        internal static partial IntPtr g_variant_new_double(double value);

        [LibraryImport(LibGLib, EntryPoint = "g_variant_new_int64")]
        internal static partial IntPtr g_variant_new_int64(long value);

        /// <summary>Creates a boolean GVariant. <paramref name="value"/> is a gboolean (0/1).</summary>
        [LibraryImport(LibGLib, EntryPoint = "g_variant_new_boolean")]
        internal static partial IntPtr g_variant_new_boolean(int value);

        /// <summary>Boxes a GVariant inside a variant (the "v" type), consuming the child's float ref.</summary>
        [LibraryImport(LibGLib, EntryPoint = "g_variant_new_variant")]
        internal static partial IntPtr g_variant_new_variant(IntPtr value);

        /// <summary>Creates a "{sv}" dictionary entry, consuming both children's float refs.</summary>
        [LibraryImport(LibGLib, EntryPoint = "g_variant_new_dict_entry")]
        internal static partial IntPtr g_variant_new_dict_entry(IntPtr key, IntPtr value);

        /// <summary>
        /// Creates an array GVariant. With <paramref name="childType"/> = Zero the element type is
        /// inferred from the (non-empty) children, each of whose floating refs is consumed.
        /// </summary>
        [LibraryImport(LibGLib, EntryPoint = "g_variant_new_array")]
        internal static partial IntPtr g_variant_new_array(IntPtr childType, IntPtr[] children, UIntPtr nChildren);

        /// <summary>Creates a tuple GVariant, consuming each child's floating reference.</summary>
        [LibraryImport(LibGLib, EntryPoint = "g_variant_new_tuple")]
        internal static partial IntPtr g_variant_new_tuple(IntPtr[] children, UIntPtr nChildren);

        // -------------------------------------------------------------------------
        // GObject / GLib cleanup
        // -------------------------------------------------------------------------

        [LibraryImport("libgobject-2.0.so.0", EntryPoint = "g_object_unref")]
        internal static partial void g_object_unref(IntPtr obj);

        [LibraryImport(LibGLib, EntryPoint = "g_error_free")]
        internal static partial void g_error_free(IntPtr error);
    }
}
