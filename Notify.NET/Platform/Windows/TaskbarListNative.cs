using System;
using System.Runtime.InteropServices;

namespace Notify.NET.Platform.Windows
{
    /// <summary>
    /// COM interop declarations for the Windows <c>ITaskbarList3</c> interface, used to drive the
    /// taskbar-button progress indicator. No native wrapper DLL is required — the COM object is the
    /// in-box shell <c>CLSID_TaskbarList</c> coclass, available on Windows 7 and later.
    /// </summary>
    internal static partial class TaskbarListNative
    {
        /// <summary>Progress-bar states accepted by <see cref="ITaskbarList3.SetProgressState"/>.</summary>
        [Flags]
        internal enum TBPFLAG
        {
            TBPF_NOPROGRESS    = 0,
            TBPF_INDETERMINATE = 0x1,
            TBPF_NORMAL        = 0x2,
            TBPF_ERROR         = 0x4,
            TBPF_PAUSED        = 0x8
        }

        /// <summary>
        /// The shell taskbar-list coclass. Instantiate via <c>new TaskbarInstance()</c> and cast to
        /// <see cref="ITaskbarList3"/>.
        /// </summary>
        [ComImport]
        [Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
        [ClassInterface(ClassInterfaceType.None)]
        internal class TaskbarInstance { }

        /// <summary>
        /// Subset of <c>ITaskbarList3</c>. Methods are declared in exact vtable order (inherited
        /// <c>ITaskbarList</c> and <c>ITaskbarList2</c> members first) up to the two we use; later
        /// members are intentionally omitted.
        /// </summary>
        [ComImport]
        [Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface ITaskbarList3
        {
            // ---- ITaskbarList ----
            void HrInit();
            void AddTab(IntPtr hwnd);
            void DeleteTab(IntPtr hwnd);
            void ActivateTab(IntPtr hwnd);
            void SetActiveAlt(IntPtr hwnd);

            // ---- ITaskbarList2 ----
            void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);

            // ---- ITaskbarList3 (only the members we need) ----
            void SetProgressValue(IntPtr hwnd, ulong ullCompleted, ulong ullTotal);
            void SetProgressState(IntPtr hwnd, TBPFLAG tbpFlags);
        }

        /// <summary>Returns the HWND of the console window owned by this process, or Zero if none.</summary>
        [LibraryImport("kernel32.dll")]
        internal static partial IntPtr GetConsoleWindow();
    }
}
