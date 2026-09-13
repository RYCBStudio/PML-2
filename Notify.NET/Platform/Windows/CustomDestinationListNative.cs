using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Notify.NET.Platform.Windows
{
    /// <summary>
    /// COM interop declarations for building a Windows 7+ jump list via the shell
    /// <c>ICustomDestinationList</c> "user tasks" API. No native wrapper DLL is required — every
    /// coclass used here is an in-box shell object, mirroring <see cref="TaskbarListNative"/>.
    ///
    /// A user task is an <c>IShellLink</c> (a shortcut) that relaunches the application's executable
    /// with arguments; its display label is set via the <c>System.Title</c> (<c>PKEY_Title</c>)
    /// property on the link's <c>IPropertyStore</c>.
    /// </summary>
    internal static partial class CustomDestinationListNative
    {
        // VT_LPWSTR — the only PROPVARIANT type we produce (for the task title).
        private const ushort VT_LPWSTR = 31;

        /// <summary><c>System.Title</c> — the label shown for a jump-list user task.</summary>
        internal static readonly PROPERTYKEY PKEY_Title = new PROPERTYKEY
        {
            fmtid = new Guid("F29F85E0-4FF9-1068-AB91-08002B27B3D9"),
            pid = 2
        };

        // IID for IObjectArray, passed to ICustomDestinationList.BeginList.
        internal static Guid IID_IObjectArray = new Guid("92CA9DCD-5622-4bba-A805-5E9F541BD8C9");

        // ------------------------------------------------------------------
        // Structs
        // ------------------------------------------------------------------

        [StructLayout(LayoutKind.Sequential)]
        internal struct PROPERTYKEY
        {
            public Guid fmtid;
            public uint pid;
        }

        /// <summary>
        /// A deliberately minimal PROPVARIANT large enough for the simple inline value we set
        /// (<c>VT_LPWSTR</c>). The trailing padding makes the managed size match the native
        /// <c>PROPVARIANT</c> (16 bytes on x86, 24 on x64), which is all that <c>SetValue</c> and
        /// <c>PropVariantClear</c> require here.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct PROPVARIANT
        {
            public ushort vt;
            public ushort wReserved1;
            public ushort wReserved2;
            public ushort wReserved3;
            public IntPtr p;
            public int p2;
        }

        // ------------------------------------------------------------------
        // Coclasses
        // ------------------------------------------------------------------

        [ComImport, Guid("77f10cf0-3db5-4966-b520-b7c54fd35ed6"), ClassInterface(ClassInterfaceType.None)]
        internal class CDestinationList { }

        [ComImport, Guid("2d3468c1-36a7-43b6-ac24-d3f02fd9607a"), ClassInterface(ClassInterfaceType.None)]
        internal class CEnumerableObjectCollection { }

        [ComImport, Guid("00021401-0000-0000-C000-000000000046"), ClassInterface(ClassInterfaceType.None)]
        internal class CShellLink { }

        // ------------------------------------------------------------------
        // Interfaces
        // ------------------------------------------------------------------

        [ComImport, Guid("92CA9DCD-5622-4bba-A805-5E9F541BD8C9"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IObjectArray
        {
            void GetCount(out uint cObjects);
            void GetAt(uint uiIndex, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
        }

        [ComImport, Guid("5632b1a4-e38a-400a-928a-d4cd63230295"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IObjectCollection
        {
            // ---- IObjectArray ----
            void GetCount(out uint cObjects);
            void GetAt(uint uiIndex, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
            // ---- IObjectCollection ----
            void AddObject([MarshalAs(UnmanagedType.Interface)] object punk);
            void AddFromArray([MarshalAs(UnmanagedType.Interface)] IObjectArray poaSource);
            void RemoveObjectAt(uint uiIndex);
            void Clear();
        }

        [ComImport, Guid("6332debf-87b5-4670-90c0-5e57b408a49e"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface ICustomDestinationList
        {
            void SetAppID([MarshalAs(UnmanagedType.LPWStr)] string pszAppID);
            void BeginList(out uint pcMaxSlots, ref Guid riid,
                           [MarshalAs(UnmanagedType.Interface)] out object ppv);
            void AppendCategory([MarshalAs(UnmanagedType.LPWStr)] string pszCategory,
                                [MarshalAs(UnmanagedType.Interface)] IObjectArray poa);
            void AppendKnownCategory(int category);
            void AddUserTasks([MarshalAs(UnmanagedType.Interface)] IObjectArray poa);
            void CommitList();
            void GetRemovedDestinations(ref Guid riid,
                                        [MarshalAs(UnmanagedType.Interface)] out object ppv);
            void DeleteList([MarshalAs(UnmanagedType.LPWStr)] string pszAppID);
            void AbortList();
        }

        [ComImport, Guid("000214F9-0000-0000-C000-000000000046"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch,
                         IntPtr pfd, uint fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath,
                                 int cch, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            void Resolve(IntPtr hwnd, uint fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IPropertyStore
        {
            void GetCount(out uint cProps);
            void GetAt(uint iProp, out PROPERTYKEY pkey);
            void GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
            void SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);
            void Commit();
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        [LibraryImport("ole32.dll")]
        private static partial int PropVariantClear(ref PROPVARIANT pvar);

        /// <summary>
        /// Sets a string property on a link's property store and commits it. Used to assign the
        /// task's display title (<see cref="PKEY_Title"/>), which is required for the task to appear.
        /// </summary>
        internal static void SetStringValue(IPropertyStore store, PROPERTYKEY key, string value)
        {
            var pv = new PROPVARIANT
            {
                vt = VT_LPWSTR,
                p = Marshal.StringToCoTaskMemUni(value)
            };
            try
            {
                store.SetValue(ref key, ref pv);
                store.Commit();
            }
            finally
            {
                // Frees the CoTaskMem string we allocated for `p`.
                PropVariantClear(ref pv);
            }
        }
    }
}
