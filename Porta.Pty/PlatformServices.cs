// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Porta.Pty
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Provides platform specific functionality.
    /// </summary>
    internal static class PlatformServices
    {
        private static readonly Lazy<IPtyProvider> WindowsProviderLazy = new Lazy<IPtyProvider>(() => new Windows.PtyProvider());
        private static readonly Lazy<IPtyProvider> LinuxProviderLazy = new Lazy<IPtyProvider>(() => new Linux.PtyProvider());
        private static readonly Lazy<IPtyProvider> MacProviderLazy = new Lazy<IPtyProvider>(() => new Mac.PtyProvider());
        private static readonly Lazy<IPtyProvider> PtyProviderLazy;
        private static readonly IDictionary<string, string> WindowsPtyEnvironment = new Dictionary<string, string>();
        private static readonly IDictionary<string, string> UnixPtyEnvironment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "TERM", "xterm-256color" },

                // Make sure we didn't start our server from inside tmux.
            { "TMUX", string.Empty },
            { "TMUX_PANE", string.Empty },

                // Make sure we didn't start our server from inside screen.
                // http://web.mit.edu/gnu/doc/html/screen_20.html
            { "STY", string.Empty },
            { "WINDOW", string.Empty },

                // These variables that might confuse our terminal
            { "WINDOWID", string.Empty },
            { "TERMCAP", string.Empty },
            { "COLUMNS", string.Empty },
            { "LINES", string.Empty },
        };

        static PlatformServices()
        {
            // DllImport 默认只探测应用根目录，不会查找 runtimes/<rid>/native 下
            // 手动拷贝的本机库，因此需要注册自定义解析器。
            NativeLibrary.SetDllImportResolver(typeof(PlatformServices).Assembly, ResolvePortaPtyLibrary);

            if (IsWindows)
            {
                PtyProviderLazy = WindowsProviderLazy;
                EnvironmentVariableComparer = StringComparer.OrdinalIgnoreCase;
                PtyEnvironment = WindowsPtyEnvironment;
            }
            else if (IsMac)
            {
                PtyProviderLazy = MacProviderLazy;
                EnvironmentVariableComparer = StringComparer.Ordinal;
                PtyEnvironment = UnixPtyEnvironment;
            }
            else if (IsLinux)
            {
                PtyProviderLazy = LinuxProviderLazy;
                EnvironmentVariableComparer = StringComparer.Ordinal;
                PtyEnvironment = UnixPtyEnvironment;
            }
            else
            {
                throw new PlatformNotSupportedException();
            }
        }

        /// <summary>
        /// Gets the <see cref="IPtyProvider"/> for the current platform.
        /// </summary>
        public static IPtyProvider PtyProvider => PtyProviderLazy.Value;

        /// <summary>
        /// Gets the comparer to determine if two environment variable keys are equivalent on the current platform.
        /// </summary>
        public static StringComparer EnvironmentVariableComparer { get; }

        /// <summary>
        /// Gets specific environment variables that are needed when spawning the PTY.
        /// </summary>
        public static IDictionary<string, string> PtyEnvironment { get; }

        private static bool IsLinux => OperatingSystem.IsLinux();

        private static bool IsMac => OperatingSystem.IsMacOS();
        private static bool IsWindows => OperatingSystem.IsWindows();

        /// <summary>
        /// Resolves the native shim (porta_pty.dll / libporta_pty.so / libporta_pty.dylib)
        /// from runtimes/&lt;rid&gt;/native, falling back to the application root directory.
        /// </summary>
        private static IntPtr ResolvePortaPtyLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (libraryName.IndexOf("porta_pty", StringComparison.Ordinal) < 0)
            {
                return IntPtr.Zero;
            }

            foreach (string candidate in EnumerateLibraryCandidates())
            {
                if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out IntPtr handle))
                {
                    return handle;
                }
            }

            return IntPtr.Zero;
        }

        private static IEnumerable<string> EnumerateLibraryCandidates()
        {
            string fileName = IsMac
                    ? "libporta_pty.dylib"
                    : "libporta_pty.so";

            string baseDir = AppContext.BaseDirectory;
            foreach (string rid in EnumerateRuntimeIdentifiers())
            {
                yield return Path.Combine(baseDir, "runtimes", rid, "native", fileName);
            }

            yield return Path.Combine(baseDir, fileName);
        }

        /// <summary>
        /// Returns candidate RIDs in priority order: the exact RID, the musl-stripped
        /// variant (e.g. linux-musl-x64 -&gt; linux-x64) and the portable OS-arch RID.
        /// </summary>
        private static IEnumerable<string> EnumerateRuntimeIdentifiers()
        {
            string rid = RuntimeInformation.RuntimeIdentifier;
            yield return rid;

            string arch = RuntimeInformation.OSArchitecture switch
            {
                Architecture.X64 => "x64",
                Architecture.Arm64 => "arm64",
                Architecture.X86 => "x86",
                Architecture.Arm => "arm",
                _ => string.Empty,
            };

            string osPrefix = IsWindows ? "win" : IsMac ? "osx" : "linux";
            if (arch.Length > 0)
            {
                string portableRid = $"{osPrefix}-{arch}";
                if (!string.Equals(portableRid, rid, StringComparison.OrdinalIgnoreCase))
                {
                    yield return portableRid;
                }

                if (rid.StartsWith("linux-musl-", StringComparison.OrdinalIgnoreCase))
                {
                    yield return $"linux-{arch}";
                }
            }
        }
    }
}
