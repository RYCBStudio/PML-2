using System;
using System.Runtime.InteropServices;

namespace Notify.NET.Platform.MacOS
{
    /// <summary>
    /// P/Invoke declarations for <c>libMacNotifyWrapper.dylib</c>.
    ///
    /// All strings in structs are marshalled as UTF-8 via <see cref="IntPtr"/> and
    /// <see cref="Marshal.StringToHGlobalAnsi"/>. On macOS the ANSI code page is UTF-8,
    /// so this is a faithful UTF-8 round-trip.
    ///
    /// Every exported function uses the C calling convention (cdecl), which is the
    /// platform default for all architectures on macOS.
    /// </summary>
    internal static partial class MacNotifyNative
    {
        internal const string LibName = "MacNotifyWrapper";

        // ------------------------------------------------------------------
        // Unmanaged callback delegate types
        // ------------------------------------------------------------------

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void ActivatedCallback(long notifId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void ButtonActivatedCallback(long notifId, int buttonIndex);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void DismissedCallback(long notifId, int reason);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void FailedCallback(long notifId);

        // ------------------------------------------------------------------
        // MNW_Handler — bundle of four function pointers
        // ------------------------------------------------------------------

        [StructLayout(LayoutKind.Sequential)]
        internal struct MNW_Handler
        {
            public IntPtr onActivated;
            public IntPtr onButtonActivated;
            public IntPtr onDismissed;
            public IntPtr onFailed;
        }

        // ------------------------------------------------------------------
        // MNW_NotificationDescriptor — all string fields are raw pointers
        // ------------------------------------------------------------------

        [StructLayout(LayoutKind.Sequential)]
        internal struct MNW_NotificationDescriptor
        {
            public IntPtr title;             // const char* UTF-8, required
            public IntPtr body;              // const char* UTF-8, may be Zero
            public IntPtr imagePath;         // const char* UTF-8, may be Zero
            public IntPtr buttonLabels;      // const char** array, may be Zero
            public int    buttonCount;
            public long   expirationMs;      // reserved — not used by UNUserNotificationCenter
            public int    audioOption;
            public int    interruptionLevel;
        }

        // ------------------------------------------------------------------
        // Exported functions
        // ------------------------------------------------------------------

        [LibraryImport(LibName)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static partial bool MNW_IsSupported();

        [LibraryImport(LibName)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static partial bool MNW_Initialize(
            [MarshalAs(UnmanagedType.LPStr)] string appName);

        [LibraryImport(LibName)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static partial void MNW_Uninitialize();

        [LibraryImport(LibName)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static partial long MNW_ShowNotification(
            ref MNW_NotificationDescriptor descriptor,
            ref MNW_Handler handler);

        [LibraryImport(LibName)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static partial bool MNW_HideNotification(long notifId);

        // ------------------------------------------------------------------
        // Dock-tile progress
        // ------------------------------------------------------------------

        internal const int MNW_PROGRESS_NONE          = 0;
        internal const int MNW_PROGRESS_INDETERMINATE = 1;
        internal const int MNW_PROGRESS_NORMAL        = 2;
        internal const int MNW_PROGRESS_PAUSED        = 3;
        internal const int MNW_PROGRESS_ERROR         = 4;

        [LibraryImport(LibName)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static partial void MNW_SetTaskbarProgress(int state, double fraction);
    }
}
