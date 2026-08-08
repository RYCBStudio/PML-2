// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Windows.Win32;
using Windows.Win32.System.Threading;
using static Windows.Win32.PInvoke;

namespace Porta.Pty.Windows
{
    using System;
    using System.Runtime.InteropServices;

    internal static class NativeMethods
    {
        public const int S_OK = 0;

        // PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE value
        // This is ProcThreadAttributePseudoConsole (22) | PROC_THREAD_ATTRIBUTE_INPUT (0x20000)
        // Not present in CsWin32 metadata, so we keep the custom constant.
        private const int PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x20016; // 22 | 0x20000

        private static readonly Lazy<bool> IsPseudoConsoleSupportedLazy = new Lazy<bool>(
            () =>
            {
                var hLibrary = LoadLibrary("kernel32.dll");
                return !hLibrary.IsInvalid && GetProcAddress(hLibrary, "CreatePseudoConsole") != IntPtr.Zero;
            },
            isThreadSafe: true);

        internal static bool IsPseudoConsoleSupported => IsPseudoConsoleSupportedLazy.Value;

        // Extension method to initialize STARTUPINFOEXW with PseudoConsole attribute
        internal static unsafe void InitAttributeListAttachedToConPTY(ref this STARTUPINFOEXW startupInfo, ClosePseudoConsoleSafeHandle pseudoConsoleHandle)
        {
            startupInfo.StartupInfo.cb = (uint)Marshal.SizeOf<STARTUPINFOEXW>();
            startupInfo.StartupInfo.dwFlags = STARTUPINFOW_FLAGS.STARTF_USESTDHANDLES;

            const int AttributeCount = 1;
            nuint size = 0;

            // Create the appropriately sized thread attribute list
            // Note: the CsWin32 friendly overload omits dwFlags (must be 0).
            bool wasInitialized = InitializeProcThreadAttributeList(default, AttributeCount, ref size);
            if (wasInitialized || size == 0)
            {
                throw new InvalidOperationException(
                    $"Couldn't get the size of the process attribute list for {AttributeCount} attributes",
                    new System.ComponentModel.Win32Exception());
            }

            startupInfo.lpAttributeList = new LPPROC_THREAD_ATTRIBUTE_LIST(Marshal.AllocHGlobal((int)size));
            if (startupInfo.lpAttributeList.IsNull)
            {
                throw new OutOfMemoryException("Couldn't reserve space for a new process attribute list");
            }

            // Set startup info's attribute list & initialize it
            wasInitialized = InitializeProcThreadAttributeList(startupInfo.lpAttributeList, AttributeCount, ref size);
            if (!wasInitialized)
            {
                throw new InvalidOperationException("Couldn't create new process attribute list", new System.ComponentModel.Win32Exception());
            }

            // Set thread attribute list's Pseudo Console to the specified ConPTY
            // Note: We use the raw CsWin32 UpdateProcThreadAttribute overload with our
            // custom PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE constant since the metadata
            // does not define PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE (newer Win10 feature).
            wasInitialized = UpdateProcThreadAttribute(
                startupInfo.lpAttributeList,
                0,
                (uint)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                (void*)pseudoConsoleHandle.DangerousGetHandle(),
                (nuint)Marshal.SizeOf<IntPtr>(),
                null,
                null);

            if (!wasInitialized)
            {
                throw new InvalidOperationException("Couldn't update process attribute list", new System.ComponentModel.Win32Exception());
            }
        }

        internal static unsafe void FreeAttributeList(ref this STARTUPINFOEXW startupInfo)
        {
            if (!startupInfo.lpAttributeList.IsNull)
            {
                DeleteProcThreadAttributeList(startupInfo.lpAttributeList);
                Marshal.FreeHGlobal(new IntPtr(startupInfo.lpAttributeList.Value));
                startupInfo.lpAttributeList = default;
            }
        }
    }
}
