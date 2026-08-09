// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Console;
using Windows.Win32.System.Threading;
using static Porta.Pty.Windows.NativeMethods;
using static Windows.Win32.PInvoke;

namespace Porta.Pty.Windows
{
    /// <summary>
    /// Provides a pty connection for windows machines using PseudoConsole.
    /// </summary>
    internal class PtyProvider : IPtyProvider
    {
        /// <inheritdoc/>
        public Task<IPtyConnection> StartTerminalAsync(
            PtyOptions options,
            TraceSource trace,
            CancellationToken cancellationToken)
        {
            if (!IsPseudoConsoleSupported)
            {
                throw new PlatformNotSupportedException(
                    "PseudoConsole (ConPTY) is not supported on this version of Windows. " +
                    "Windows 10 version 1809 (October 2018 Update) or later is required.");
            }

            return StartPseudoConsoleAsync(options, trace, cancellationToken);
        }

        private static string GetAppOnPath(string app, string cwd, IDictionary<string, string> env)
        {
            bool isWow64 = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432") != null;
            var windir = Environment.GetEnvironmentVariable("WINDIR");
            var sysnativePath = Path.Combine(windir, "Sysnative");
            var sysnativePathWithSlash = sysnativePath + Path.DirectorySeparatorChar;
            var system32Path = Path.Combine(windir, "System32");
            var system32PathWithSlash = system32Path + Path.DirectorySeparatorChar;

            try
            {
                // If we have an absolute path then we take it.
                if (Path.IsPathRooted(app))
                {
                    if (isWow64)
                    {
                        // If path is on system32, check sysnative first
                        if (app.StartsWith(system32PathWithSlash, StringComparison.OrdinalIgnoreCase))
                        {
                            var sysnativeApp = Path.Combine(sysnativePath, app.Substring(system32PathWithSlash.Length));
                            if (File.Exists(sysnativeApp))
                            {
                                return sysnativeApp;
                            }
                        }
                    }
                    else if (app.StartsWith(sysnativePathWithSlash, StringComparison.OrdinalIgnoreCase))
                    {
                        // Change Sysnative to System32 if the OS is Windows but NOT WoW64. It's
                        // safe to assume that this was used by accident as Sysnative does not
                        // exist and will break in non-WoW64 environments.
                        return Path.Combine(system32Path, app.Substring(sysnativePathWithSlash.Length));
                    }

                    return app;
                }

                if (Path.GetDirectoryName(app) != string.Empty)
                {
                    // We have a directory and the directory is relative. Make the path absolute
                    // to the current working directory.
                    return Path.Combine(cwd, app);
                }
            }
            catch (ArgumentException)
            {
                throw new ArgumentException($"Invalid terminal app path '{app}'");
            }
            catch (PathTooLongException)
            {
                throw new ArgumentException($"Terminal app path '{app}' is too long");
            }

            string pathEnvironment = (env != null && env.TryGetValue("PATH", out string p) ? p : null)
                ?? Environment.GetEnvironmentVariable("PATH");

            if (string.IsNullOrWhiteSpace(pathEnvironment))
            {
                // No PATH environment. Make path absolute to the cwd
                return Path.Combine(cwd, app);
            }

            var paths = new List<string>(pathEnvironment.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
            if (isWow64)
            {
                // On Wow64, if %PATH% contains %WINDIR%\System32 but does not have %WINDIR%\Sysnative, add it before System32.
                var indexOfSystem32 = paths.FindIndex(entry =>
                    string.Equals(entry, system32Path, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(entry, system32PathWithSlash, StringComparison.OrdinalIgnoreCase));

                var indexOfSysnative = paths.FindIndex(entry =>
                    string.Equals(entry, sysnativePath, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(entry, sysnativePathWithSlash, StringComparison.OrdinalIgnoreCase));

                if (indexOfSystem32 >= 0 && indexOfSysnative == -1)
                {
                    paths.Insert(indexOfSystem32, sysnativePath);
                }
            }

            // We have a simple file name. We get the path variable from the env
            // and try to find the executable on the path.
            foreach (string pathEntry in paths)
            {
                bool isPathEntryRooted;
                try
                {
                    isPathEntryRooted = Path.IsPathRooted(pathEntry);
                }
                catch (ArgumentException)
                {
                    // Ignore invalid entry on %PATH%
                    continue;
                }

                // The path entry is absolute.
                string fullPath = isPathEntryRooted ? Path.Combine(pathEntry, app) : Path.Combine(cwd, pathEntry, app);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }

                var withExtension = fullPath + ".com";
                if (File.Exists(withExtension))
                {
                    return withExtension;
                }

                withExtension = fullPath + ".exe";
                if (File.Exists(withExtension))
                {
                    return withExtension;
                }
            }

            // Not found on PATH. Make path absolute to the cwd
            return Path.Combine(cwd, app);
        }

        private static string GetEnvironmentString(IDictionary<string, string> environment)
        {
            string[] keys = new string[environment.Count];
            environment.Keys.CopyTo(keys, 0);

            string[] values = new string[environment.Count];
            environment.Values.CopyTo(values, 0);

            // Sort both by the keys
            // Windows 2000 requires the environment block to be sorted by the key.
            Array.Sort(keys, values, StringComparer.OrdinalIgnoreCase);

            // Create a list of null terminated "key=val" strings
            var result = new StringBuilder();
            for (int i = 0; i < environment.Count; ++i)
            {
                result.Append(keys[i]);
                result.Append('=');
                result.Append(values[i]);
                result.Append('\0');
            }

            // An extra null at the end indicates end of list.
            result.Append('\0');

            return result.ToString();
        }

        private unsafe Task<IPtyConnection> StartPseudoConsoleAsync(
           PtyOptions options,
           TraceSource trace,
           CancellationToken cancellationToken)
        {
            // Create a Job Object to ensure child processes are killed when the terminal exits.
            // This prevents zombie ConPTY sessions by using JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE.
            SafeFileHandle jobObjectHandle = JobObject.Create();

            try
            {
                // Create the in/out pipes using CsWin32 (returns SafeFileHandle)
                if (!CreatePipe(out var inPipePseudoConsoleSide, out var inPipeOurSide, null, 0))
                {
                    throw new InvalidOperationException("Could not create an anonymous pipe", new Win32Exception());
                }

                if (!CreatePipe(out var outPipeOurSide, out var outPipePseudoConsoleSide, null, 0))
                {
                    throw new InvalidOperationException("Could not create an anonymous pipe", new Win32Exception());
                }

                var coord = new COORD { X = (short)options.Cols, Y = (short)options.Rows };

                // Create the Pseudo Console, using the pipes
                var hr = CreatePseudoConsole(
                    coord,
                    inPipePseudoConsoleSide,
                    outPipePseudoConsoleSide,
                    0,
                    out ClosePseudoConsoleSafeHandle pseudoConsoleHandle);

                if (hr.Failed)
                {
                    throw new InvalidOperationException($"Could not create pseudo console: {hr}", Marshal.GetExceptionForHR(hr.Value));
                }

                // IMPORTANT: Close the pseudoconsole side of the pipes after CreatePseudoConsole
                // The pseudoconsole now owns these handles, and keeping them open on our side
                // can cause input/output buffering issues.
                inPipePseudoConsoleSide.Dispose();
                outPipePseudoConsoleSide.Dispose();

                // Prepare the StartupInfoEx structure attached to the ConPTY.
                var startupInfo = new STARTUPINFOEXW();
                startupInfo.InitAttributeListAttachedToConPTY(pseudoConsoleHandle);

                try
                {
                    string app = GetAppOnPath(options.App, options.Cwd, options.Environment);
                    string arguments = options.VerbatimCommandLine ?
                        WindowsArguments.FormatVerbatim(options.CommandLine) :
                        WindowsArguments.Format(options.CommandLine);

                    var commandLine = new StringBuilder(app.Length + arguments.Length + 4);
                    bool quoteApp = app.Contains(" ") && !app.StartsWith("\"") && !app.EndsWith("\"");
                    if (quoteApp)
                    {
                        commandLine.Append('"').Append(app).Append('"');
                    }
                    else
                    {
                        commandLine.Append(app);
                    }

                    if (!string.IsNullOrWhiteSpace(arguments))
                    {
                        commandLine.Append(' ');
                        commandLine.Append(arguments);
                    }

                    trace.TraceInformation($"Starting terminal process '{app}' with command line {commandLine}");

                    SafeFileHandle? processHandle = null;
                    SafeFileHandle? mainThreadHandle = null;
                    int pid = 0;
                    bool success = false;

                    // Build the environment block from the options
                    string environmentBlock = GetEnvironmentString(options.Environment);

                    // Pin the environment string and get a pointer to it
                    var environmentHandle = GCHandle.Alloc(Encoding.Unicode.GetBytes(environmentBlock), GCHandleType.Pinned);
                    try
                    {
                        // Call the Win32 CreateProcess.
                        // STARTUPINFOEXW's layout begins with STARTUPINFOW, so we can pass a
                        // pointer to the whole structure as a STARTUPINFOW*.
                        var processInfoRaw = new PROCESS_INFORMATION();
                        string commandLineString = commandLine.ToString();
                        fixed (char* cmdLinePtr = commandLineString)
                        fixed (char* cwdPtr = options.Cwd)
                        {
                            success = CreateProcess(
                                default,   // lpApplicationName
                                (PWSTR)cmdLinePtr,   // lpCommandLine
                                null,   // lpProcessAttributes
                                null,   // lpThreadAttributes
                                false,  // bInheritHandles VERY IMPORTANT that this is false
                                PROCESS_CREATION_FLAGS.EXTENDED_STARTUPINFO_PRESENT | PROCESS_CREATION_FLAGS.CREATE_UNICODE_ENVIRONMENT, // dwCreationFlags
                                (void*)environmentHandle.AddrOfPinnedObject(),   // lpEnvironment - pass the environment block
                                (PCWSTR)cwdPtr,   // lpCurrentDirectory
                                (STARTUPINFOW*)&startupInfo,   // lpStartupInfo (STARTUPINFOEXW begins with STARTUPINFOW)
                                &processInfoRaw);
                        }

                        if (success)
                        {
                            // Create safe handles from raw handles
                            processHandle = new SafeFileHandle(processInfoRaw.hProcess, ownsHandle: true);
                            mainThreadHandle = new SafeFileHandle(processInfoRaw.hThread, ownsHandle: true);
                            pid = (int)processInfoRaw.dwProcessId;

                            // Assign the process to the job object immediately after creation.
                            // This ensures the process and any children it spawns will be terminated
                            // when the job handle is closed (e.g., when our terminal crashes).
                            JobObject.AssignProcess(jobObjectHandle, (IntPtr)processInfoRaw.hProcess);
                        }
                    }
                    finally
                    {
                        environmentHandle.Free();
                    }

                    if (!success)
                    {
                        var errorCode = Marshal.GetLastWin32Error();
                        var exception = new Win32Exception(errorCode);
                        throw new InvalidOperationException($"Could not start terminal process {commandLine.ToString()}: {exception.Message}", exception);
                    }

                    var connectionOptions = new PseudoConsoleConnection.PseudoConsoleConnectionHandles(
                        inPipeOurSide,
                        outPipeOurSide,
                        pseudoConsoleHandle,
                        processHandle!,
                        pid,
                        mainThreadHandle!,
                        jobObjectHandle);

                    var result = new PseudoConsoleConnection(connectionOptions);
                    return Task.FromResult<IPtyConnection>(result);
                }
                finally
                {
                    startupInfo.FreeAttributeList();
                }
            }
            catch
            {
                // If anything fails, make sure to dispose the job object
                jobObjectHandle?.Dispose();
                throw;
            }
        }
    }
}
