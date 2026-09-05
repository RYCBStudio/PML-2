using System;
using System.IO;
using System.Runtime.InteropServices;
using Notify.NET.Exceptions;

namespace Notify.NET.Platform.MacOS
{
    /// <summary>
    /// Ensures <c>libMacNotifyWrapper.dylib</c> is loaded before the first P/Invoke call.
    ///
    /// Resolution order:
    ///   1. Alongside the executing assembly (typical for published apps).
    ///   2. NuGet <c>runtimes/&lt;rid&gt;/native/</c> layout relative to the executing assembly.
    ///   3. NuGet layout relative to the entry assembly.
    /// </summary>
    internal static class MacOSNativeLibraryLoader
    {
        private const string DylibName = "libMacNotifyWrapper.dylib";

        private static volatile bool _loaded;
        private static readonly object _lock = new object();

        /// <summary>
        /// Loads the dylib if it has not been loaded yet.
        /// Throws <see cref="DllNotFoundException"/> if the file cannot be found or opened.
        /// </summary>
        internal static void EnsureLoaded()
        {
            if (_loaded) return;
            lock (_lock)
            {
                if (_loaded) return;
                LoadDylib();
                _loaded = true;
            }
        }

        private static void LoadDylib()
        {
            string rid = GetRuntimeIdentifier();
            string relativeSubPath = Path.Combine("runtimes", rid, "native", DylibName);

            string? assemblyDir = Path.GetDirectoryName(
                typeof(MacOSNativeLibraryLoader).Assembly.Location);

            // Search locations in priority order.
            string[] candidates = assemblyDir != null
                ? new[]
                {
                    Path.Combine(assemblyDir, DylibName),
                    Path.Combine(assemblyDir, relativeSubPath),
                    Path.Combine(AppContext.BaseDirectory, DylibName),
                    Path.Combine(AppContext.BaseDirectory, relativeSubPath),
                }
                : new[]
                {
                    Path.Combine(AppContext.BaseDirectory, DylibName),
                    Path.Combine(AppContext.BaseDirectory, relativeSubPath),
                };

            foreach (string candidate in candidates)
            {
                if (!File.Exists(candidate)) continue;
                IntPtr handle = dlopen(candidate, RTLD_NOW | RTLD_GLOBAL);
                if (handle != IntPtr.Zero) return;
            }

            throw new DllNotFoundException(
                $"Could not load {DylibName}. " +
                $"Ensure the macOS native dylib is present in the output directory or " +
                $"runtimes/{rid}/native/. " +
                $"Build with: cd native/MacNotifyWrapper && make install");
        }

        private static string GetRuntimeIdentifier()
        {
            switch (RuntimeInformation.ProcessArchitecture)
            {
                case Architecture.X64:   return "osx-x64";
                case Architecture.Arm64: return "osx-arm64";
                default:
                    throw new Exceptions.PlatformNotSupportedException(
                        $"Unsupported macOS architecture: {RuntimeInformation.ProcessArchitecture}");
            }
        }

        // RTLD_NOW=2, RTLD_GLOBAL=8 on macOS.
        private const int RTLD_NOW    = 2;
        private const int RTLD_GLOBAL = 8;

        [DllImport("libSystem.B.dylib")]
        private static extern IntPtr dlopen(string path, int mode);
    }
}
