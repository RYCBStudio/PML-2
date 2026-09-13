using System;
using System.Runtime.InteropServices;

namespace Notify.NET.Platform.Linux
{
    /// <summary>
    /// P/Invoke declarations for libnotify.so.4 and the GLib/GObject functions
    /// needed to manage signals and the main event loop.
    ///
    /// All GLib string parameters use ANSI (UTF-8) encoding, which matches GLib's
    /// internal string convention on Linux.
    /// </summary>
    internal static partial class LibNotifyNative
    {
        private const string LibNotify = "libnotify.so.4";
        private const string LibGLib   = "libglib-2.0.so.0";
        private const string LibGObj   = "libgobject-2.0.so.0";
        private const string LibGdkPB  = "libgdk_pixbuf-2.0.so.0";

        // -------------------------------------------------------------------------
        // Unmanaged callback delegate types
        // -------------------------------------------------------------------------

        /// <summary>Callback fired when the user clicks an action button on the notification.</summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        internal delegate void NotifyActionCallback(IntPtr notification, string action, IntPtr userData);

        /// <summary>Callback fired when the notification is closed (any reason).</summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void NotifyClosedCallback(IntPtr notification, IntPtr userData);

        /// <summary>Function posted to the GMainContext via g_main_context_invoke.</summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate bool GSourceFunc(IntPtr userData);

        // -------------------------------------------------------------------------
        // libnotify
        // -------------------------------------------------------------------------

        /// <summary>Initialises libnotify. Must be called before any other notify_ function.</summary>
        [LibraryImport(LibNotify, EntryPoint = "notify_init", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool notify_init(string appName);

        /// <summary>Returns true if notify_init() has been called successfully.</summary>
        [LibraryImport(LibNotify, EntryPoint = "notify_is_initted")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool notify_is_initted();

        /// <summary>Releases all libnotify resources.</summary>
        [LibraryImport(LibNotify, EntryPoint = "notify_uninit")]
        internal static partial void notify_uninit();

        /// <summary>
        /// Creates a new notification object. The returned pointer is a GObject reference
        /// with a ref-count of 1. Callers must eventually call g_object_unref.
        /// </summary>
        [LibraryImport(LibNotify, EntryPoint = "notify_notification_new", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
        internal static partial IntPtr notify_notification_new(string summary, string? body, string? icon);

        /// <summary>Shows the notification. Returns false and sets <paramref name="error"/> on failure.</summary>
        [LibraryImport(LibNotify, EntryPoint = "notify_notification_show")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool notify_notification_show(IntPtr notification, ref IntPtr error);

        /// <summary>Programmatically closes the notification.</summary>
        [LibraryImport(LibNotify, EntryPoint = "notify_notification_close")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool notify_notification_close(IntPtr notification, ref IntPtr error);

        /// <summary>
        /// Adds an action button to the notification.
        /// <paramref name="callback"/> must be a pinned function pointer; see <see cref="LibNotifyCallbackBridge"/>.
        /// </summary>
        [LibraryImport(LibNotify, EntryPoint = "notify_notification_add_action", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
        internal static partial void notify_notification_add_action(
            IntPtr notification,
            string action,      // machine-readable action ID
            string label,       // human-readable label
            IntPtr callback,    // NotifyActionCallback function pointer
            IntPtr userData,
            IntPtr freeFunc);   // GFreeFunc, pass IntPtr.Zero

        /// <summary>Sets a display hint on the notification (e.g., urgency level).</summary>
        [LibraryImport(LibNotify, EntryPoint = "notify_notification_set_hint", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
        internal static partial void notify_notification_set_hint(
            IntPtr notification, string key, IntPtr value /* GVariant* */);

        /// <summary>Sets the notification's image from a GdkPixbuf.</summary>
        [LibraryImport(LibNotify, EntryPoint = "notify_notification_set_image_from_pixbuf")]
        internal static partial void notify_notification_set_image_from_pixbuf(
            IntPtr notification, IntPtr pixbuf /* GdkPixbuf* */);

        /// <summary>Returns the reason the notification was closed (call after the "closed" signal).</summary>
        [LibraryImport(LibNotify, EntryPoint = "notify_notification_get_closed_reason")]
        internal static partial int notify_notification_get_closed_reason(IntPtr notification);

        // -------------------------------------------------------------------------
        // GLib / GObject
        // -------------------------------------------------------------------------

        /// <summary>Creates a new GMainLoop.</summary>
        [DllImport(LibGLib, EntryPoint = "g_main_loop_new")]
        internal static extern IntPtr g_main_loop_new(IntPtr context /* null = default */, bool isRunning);

        /// <summary>Runs the GMainLoop, blocking until g_main_loop_quit is called.</summary>
        [LibraryImport(LibGLib, EntryPoint = "g_main_loop_run")]
        internal static partial void g_main_loop_run(IntPtr loop);

        /// <summary>Signals the GMainLoop to stop its run() and return.</summary>
        [LibraryImport(LibGLib, EntryPoint = "g_main_loop_quit")]
        internal static partial void g_main_loop_quit(IntPtr loop);

        /// <summary>Releases a GMainLoop reference.</summary>
        [LibraryImport(LibGLib, EntryPoint = "g_main_loop_unref")]
        internal static partial void g_main_loop_unref(IntPtr loop);

        /// <summary>
        /// Posts a function to be called on the default GMainContext from any thread.
        /// The function is invoked on the GMainLoop thread.
        /// </summary>
        [LibraryImport(LibGLib, EntryPoint = "g_main_context_invoke")]
        internal static partial void g_main_context_invoke(IntPtr context, IntPtr func, IntPtr userData);

        /// <summary>
        /// Connects a callback to a GObject signal.
        /// Returns the handler ID (used to disconnect later if needed).
        /// </summary>
        [LibraryImport(LibGObj, EntryPoint = "g_signal_connect_data", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
        internal static partial ulong g_signal_connect_data(
            IntPtr instance,
            string detailedSignal,
            IntPtr cHandler,
            IntPtr data,
            IntPtr destroyData,
            int connectFlags);

        /// <summary>Releases one reference on a GObject. The object is destroyed when the ref-count reaches 0.</summary>
        [LibraryImport(LibGObj, EntryPoint = "g_object_unref")]
        internal static partial void g_object_unref(IntPtr obj);

        /// <summary>Frees a GError and sets the pointer to null.</summary>
        [LibraryImport(LibGLib, EntryPoint = "g_error_free")]
        internal static partial void g_error_free(IntPtr error);

        // -------------------------------------------------------------------------
        // GLib GVariant helpers (needed for urgency hints)
        // -------------------------------------------------------------------------

        /// <summary>Creates a GVariant holding a byte value (used for the urgency hint).</summary>
        [LibraryImport(LibGLib, EntryPoint = "g_variant_new_byte")]
        internal static partial IntPtr g_variant_new_byte(byte value);

        // -------------------------------------------------------------------------
        // GdkPixbuf (optional — for loading images from disk paths)
        // -------------------------------------------------------------------------

        /// <summary>
        /// Loads an image from disk into a GdkPixbuf.
        /// Returns IntPtr.Zero on failure; callers should fall back gracefully.
        /// </summary>
        [LibraryImport(LibGdkPB, EntryPoint = "gdk_pixbuf_new_from_file", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(System.Runtime.InteropServices.Marshalling.AnsiStringMarshaller))]
        internal static partial IntPtr gdk_pixbuf_new_from_file(string filename, ref IntPtr error);

        // -------------------------------------------------------------------------
        // Urgency level constants (freedesktop.org spec)
        // -------------------------------------------------------------------------
        internal const byte URGENCY_LOW      = 0;
        internal const byte URGENCY_NORMAL   = 1;
        internal const byte URGENCY_CRITICAL = 2;
    }
}
