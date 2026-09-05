using System;
using System.Runtime.InteropServices;

namespace Notify.NET.Platform.MacOS
{
    /// <summary>
    /// P/Invoke declarations for the Dock-menu ("jump list") entry points exported by
    /// <c>libMacNotifyWrapper.dylib</c> (see <c>MacNotifyWrapper.h</c>).
    ///
    /// Unlike the Windows/Linux jump lists, the macOS Dock menu fires a live in-process
    /// callback (<see cref="DockMenuCallback"/>) — there is no relaunch. The entry points are
    /// only effective for a regular bundled GUI application with a running main loop; a bare
    /// console process has no Dock menu and the calls are harmless no-ops.
    ///
    /// All strings are UTF-8; on macOS the ANSI code page is UTF-8 so <see cref="UnmanagedType.LPStr"/>
    /// marshalling is a faithful round-trip. Every function uses the C calling convention (cdecl).
    /// </summary>
    internal static partial class MacJumpListNative
    {
        internal const string LibName = "MacNotifyWrapper";

        /// <summary>
        /// Fired on the main thread when the user clicks a Dock-menu item; <paramref name="taskId"/>
        /// is the id supplied to <see cref="MNW_SetDockMenu"/> for that item.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void DockMenuCallback([MarshalAs(UnmanagedType.LPStr)] string taskId);

        [LibraryImport(LibName)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static partial bool MNW_IsSupported();

        [LibraryImport(LibName)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static partial void MNW_SetDockMenuHandler(DockMenuCallback? callback);

        [LibraryImport(LibName)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static partial void MNW_SetDockMenu(
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string[]? ids,
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string[]? titles,
            int count);

        [LibraryImport(LibName)]
        [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
        internal static partial void MNW_ClearDockMenu();
    }
}
