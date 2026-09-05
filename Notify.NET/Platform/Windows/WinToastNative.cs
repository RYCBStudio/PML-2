using System;
using System.Runtime.InteropServices;

namespace Notify.NET.Platform.Windows
{
    /// <summary>
    /// P/Invoke declarations for WinToastWrapper.dll — the native C DLL that wraps WinToastLib.
    /// All strings are UTF-16 (Unicode) to match the wchar_t* ABI of the wrapper.
    /// The DLL must be loaded via <see cref="NativeLibraryLoader"/> before these are called.
    /// </summary>
    internal static partial class WinToastNative
    {
        private const string DllName = "WinToastWrapper";

        // -------------------------------------------------------------------------
        // Unmanaged callback delegate types.
        // IMPORTANT: These must be static fields — never pass instance delegates as
        // unmanaged function pointers. The GC does not see unmanaged references and
        // will collect instance delegates, producing an AccessViolationException.
        // -------------------------------------------------------------------------

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void ActivatedCallback(long toastId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void ButtonActivatedCallback(long toastId, int buttonIndex);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void DismissedCallback(long toastId, int reason);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void FailedCallback(long toastId);

        // -------------------------------------------------------------------------
        // Structs matching the C ABI of WinToastWrapper.h
        // -------------------------------------------------------------------------

        /// <summary>
        /// Plain-data descriptor passed to <see cref="WNT_ShowToast"/>.
        /// String fields are pointers into pinned managed memory — callers must
        /// keep the pinned handles alive for the duration of the call.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct WNT_ToastDescriptor
        {
            public IntPtr title;             // wchar_t*
            public IntPtr body;              // wchar_t* (may be IntPtr.Zero)
            public IntPtr imagePath;         // wchar_t* (may be IntPtr.Zero) — app logo override in generic templates
            public IntPtr heroImagePath;     // wchar_t* (may be IntPtr.Zero) — full-width banner above notification
            public IntPtr buttonLabels;      // wchar_t** (array of pointers, may be IntPtr.Zero)
            public int    buttonCount;
            public long   expirationMs;      // 0 = platform default
            public int    scenario;          // WNT_Scenario enum value
            public int    audioOption;       // WNT_AudioOption enum value
            // Extended fields (v2):
            public IntPtr inlineImagePath;   // wchar_t* (may be IntPtr.Zero) — image shown inline in notification body
            public IntPtr attributionText;   // wchar_t* (may be IntPtr.Zero) — small text at the bottom
            public IntPtr customAudioPath;   // wchar_t* (may be IntPtr.Zero) — ms-winsoundevent: or file URI; overrides audioFile
            public int    cropHint;          // WNT_CropHint (0 = Square, 1 = Circle)
            public int    audioFile;         // WNT_AudioFile (-1 = not set)
        }

        /// <summary>
        /// Struct of four function pointers passed to <see cref="WNT_ShowToast"/>.
        /// Must be pinned for the lifetime of the toast (until dismissed or failed).
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct WNT_Handler
        {
            public IntPtr onActivated;        // ActivatedCallback
            public IntPtr onButtonActivated;  // ButtonActivatedCallback
            public IntPtr onDismissed;        // DismissedCallback
            public IntPtr onFailed;           // FailedCallback
        }

        // WNT_Scenario values (must match enum in WinToastWrapper.h)
        internal const int WNT_SCENARIO_DEFAULT       = 0;
        internal const int WNT_SCENARIO_ALARM         = 1;
        internal const int WNT_SCENARIO_REMINDER      = 2;
        internal const int WNT_SCENARIO_INCOMING_CALL = 3;

        // WNT_AudioOption values (must match enum in WinToastWrapper.h)
        internal const int WNT_AUDIO_DEFAULT = 0;
        internal const int WNT_AUDIO_SILENT  = 1;
        internal const int WNT_AUDIO_LOOP    = 2;

        // WNT_CropHint values (must match enum in WinToastWrapper.h)
        internal const int WNT_CROP_HINT_SQUARE = 0;
        internal const int WNT_CROP_HINT_CIRCLE = 1;

        // WNT_AudioFile values (must match enum in WinToastWrapper.h)
        internal const int WNT_AUDIO_FILE_NONE    = -1;
        internal const int WNT_AUDIO_FILE_DEFAULT =  0;
        internal const int WNT_AUDIO_FILE_IM      =  1;
        internal const int WNT_AUDIO_FILE_MAIL    =  2;
        internal const int WNT_AUDIO_FILE_REMINDER =  3;
        internal const int WNT_AUDIO_FILE_SMS     =  4;
        internal const int WNT_AUDIO_FILE_ALARM   =  5;

        // -------------------------------------------------------------------------
        // Exported functions
        // -------------------------------------------------------------------------

        /// <summary>
        /// Initialises WinToastLib. Must be called once from an STA thread before any other function.
        /// </summary>
        /// <param name="appName">Human-readable application name shown in the Action Centre.</param>
        /// <param name="appUserModelId">
        /// The AppUserModelId (AUMI) — must match the shortcut in the Start Menu.
        /// The wrapper creates the shortcut automatically if it doesn't exist.
        /// </param>
        /// <returns>true on success.</returns>
        [LibraryImport(DllName, EntryPoint = "WNT_Initialize", SetLastError = false, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool WNT_Initialize(string appName, string appUserModelId, string? appIconPath);

        /// <summary>Uninitialises WinToastLib and releases all internal resources.</summary>
        [LibraryImport(DllName, EntryPoint = "WNT_Uninitialize")]
        internal static partial void WNT_Uninitialize();

        /// <summary>Returns true if WinToast is supported on this version of Windows (requires Win 8+).</summary>
        [LibraryImport(DllName, EntryPoint = "WNT_IsCompatible")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool WNT_IsCompatible();

        /// <summary>
        /// Shows a toast notification. Must be called from the STA thread.
        /// </summary>
        /// <param name="descriptor">Pointer to a <see cref="WNT_ToastDescriptor"/> with notification data.</param>
        /// <param name="handler">Pointer to a <see cref="WNT_Handler"/> with callback function pointers.</param>
        /// <returns>A positive toast ID on success, or a negative error code on failure.</returns>
        [LibraryImport(DllName, EntryPoint = "WNT_ShowToast")]
        internal static partial long WNT_ShowToast(ref WNT_ToastDescriptor descriptor, ref WNT_Handler handler);

        /// <summary>Programmatically dismisses a previously shown toast. Must be called from the STA thread.</summary>
        [LibraryImport(DllName, EntryPoint = "WNT_HideToast")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool WNT_HideToast(long toastId);
    }
}
