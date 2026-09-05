using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Notify.NET.Platform.Windows
{
    /// <summary>
    /// Loads WinToastWrapper.dll from the correct runtime-identifier sub-folder before
    /// the first P/Invoke call is made. This ensures the x64/x86/arm64 variant that
    /// matches the current process architecture is used.
    ///
    /// Once LoadLibraryW succeeds, subsequent DllImport("WinToastWrapper") resolutions
    /// find the already-loaded module in the process module list automatically.
    /// </summary>
    internal static partial class NativeLibraryLoader
    {
        private static volatile bool _loaded;
        private static readonly object _lock = new object();

        [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        private static partial IntPtr LoadLibraryW(string lpLibFileName);

        internal static void EnsureLoaded()
        {
            if (_loaded) return;

            lock (_lock)
            {
                if (_loaded) return;

                string rid = GetRuntimeIdentifier();
                string dllPath = ResolveNativePath(rid);

                IntPtr handle = LoadLibraryW(dllPath);
                if (handle == IntPtr.Zero)
                {
                    int err = Marshal.GetLastWin32Error();
                    throw new DllNotFoundException(
                        $"Failed to load WinToastWrapper.dll from '{dllPath}' (Win32 error {err}). " +
                        "Ensure the native DLL for your platform architecture is present in the " +
                        $"runtimes/{rid}/native/ directory relative to the assembly.");
                }

                _loaded = true;
            }
        }

        private static string GetRuntimeIdentifier() => RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "win-x64",
            Architecture.X86 => "win-x86",
            Architecture.Arm64 => "win-arm64",
            _ => throw new PlatformNotSupportedException(
                $"No WinToastWrapper.dll is available for architecture {RuntimeInformation.ProcessArchitecture}.")
        };

        private static string ResolveNativePath(string rid)
        {
            const string dllName = "WinToastWrapper.dll";
            string runtimeRelative = Path.Combine("runtimes", rid, "native", dllName);

            // Search order:
            // 1. App base directory — the one reliable root in every layout, including
            //    single-file / NativeAOT publishes where CodeBase is unavailable and
            //    Assembly.Location is empty for bundled assemblies.
            // 2. Alongside the assembly location (classic multi-file output layout).
            // 3. Relative to the entry assembly location.
            //
            // NOTE: Assembly.CodeBase must NOT be used here — it throws
            // NotSupportedException for assemblies loaded from a single-file bundle.

            // Typical publish output: <appdir>/WinToastWrapper.dll (copied by MSBuild)
            string flat = Path.Combine(AppContext.BaseDirectory, dllName);
            if (File.Exists(flat)) return flat;

            // NuGet runtimes layout: <appdir>/runtimes/<rid>/native/WinToastWrapper.dll
            string runtimePath = Path.Combine(AppContext.BaseDirectory, runtimeRelative);
            if (File.Exists(runtimePath)) return runtimePath;

            // Assembly location (empty string in a single-file bundle, so this is skipped there)
            string? assemblyDir = Path.GetDirectoryName(typeof(NativeLibraryLoader).Assembly.Location);
            if (!string.IsNullOrEmpty(assemblyDir))
            {
                string assemblyFlat = Path.Combine(assemblyDir, dllName);
                if (File.Exists(assemblyFlat)) return assemblyFlat;

                string assemblyRuntime = Path.Combine(assemblyDir, runtimeRelative);
                if (File.Exists(assemblyRuntime)) return assemblyRuntime;
            }

            // Fallback: relative to the entry assembly location
            string? entryDir = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
            if (!string.IsNullOrEmpty(entryDir))
            {
                string entryRuntime = Path.Combine(entryDir, runtimeRelative);
                if (File.Exists(entryRuntime)) return entryRuntime;

                string entryFlat = Path.Combine(entryDir, dllName);
                if (File.Exists(entryFlat)) return entryFlat;
            }

            // Return the NuGet path even if not found — LoadLibraryW will fail with a useful error
            return Path.Combine(AppContext.BaseDirectory, runtimeRelative);
        }
    }
}