// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Windows.Win32;
using Windows.Win32.System.Console;
using static Windows.Win32.PInvoke;

namespace Porta.Pty.Windows
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.InteropServices;
    using Microsoft.Win32.SafeHandles;

    /// <summary>
    /// A connection to a pseudoterminal spawned by native windows APIs.
    /// </summary>
    internal sealed class PseudoConsoleConnection : IPtyConnection
    {
        private readonly Process process;
        private readonly object disposeLock = new object();
        private PseudoConsoleConnectionHandles? handles;
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="PseudoConsoleConnection"/> class.
        /// </summary>
        /// <param name="handles">The set of handles associated with the pseudoconsole.</param>
        public PseudoConsoleConnection(PseudoConsoleConnectionHandles handles)
        {
            // Use FileStream with the pipe handles for direct access
            // This avoids the buffering issues that can occur with AnonymousPipeClientStream
            // Note: FileStream(SafeFileHandle, ...) does not take ownership of the handle,
            // so disposing the streams below does not close the underlying pipes early.
            ReaderStream = new FileStream(
                handles.OutPipeOurSide,
                FileAccess.Read,
                bufferSize: 0,  // No buffering
                isAsync: false);

            WriterStream = new FileStream(
                handles.InPipeOurSide,
                FileAccess.Write,
                bufferSize: 0,  // No buffering - writes go directly to pipe
                isAsync: false);

            this.handles = handles;
            Pid = handles.Pid;
            process = Process.GetProcessById(Pid);
            process.Exited += Process_Exited;
            process.EnableRaisingEvents = true;
        }

        /// <inheritdoc/>
        public event EventHandler<PtyExitedEventArgs>? ProcessExited;

        /// <inheritdoc/>
        public Stream ReaderStream { get; }

        /// <inheritdoc/>
        public Stream WriterStream { get; }

        /// <inheritdoc/>
        public int Pid { get; }

        /// <inheritdoc/>
        public int ExitCode => process.ExitCode;

        /// <inheritdoc/>
        public void Dispose()
        {
            lock (disposeLock)
            {
                if (isDisposed)
                {
                    return;
                }

                isDisposed = true;
            }

            // Unsubscribe from events first to prevent callbacks during disposal
            process.Exited -= Process_Exited;

            // ConPTY cleanup order (per Microsoft documentation):
            // 1. Close the PseudoConsole handle - signals conhost to shut down
            // 2. Close the pipes - allows pending I/O to complete
            // 3. Close process/thread handles
            // 4. Close job object last - terminates any remaining processes

            if (handles != null)
            {
                // Step 1: Close the pseudo console first (calls ClosePseudoConsole)
                // This signals conhost.exe to shut down gracefully
                handles.PseudoConsoleHandle?.Dispose();

                // Step 2: Close the pipes
                // Close our side of the pipes - this will cause any pending reads to complete
                handles.InPipeOurSide?.Dispose();
                handles.OutPipeOurSide?.Dispose();

                // Step 3: Close process and thread handles
                handles.MainThreadHandle?.Dispose();
                handles.ProcessHandle?.Dispose();

                // Step 4: Dispose the job object last - this will terminate any remaining
                // child processes due to JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                handles.JobObjectHandle?.Dispose();

                handles = null;
            }

            // Dispose streams (they don't own the underlying handles)
            ReaderStream?.Dispose();
            WriterStream?.Dispose();

            // Dispose the Process object
            process?.Dispose();
        }

        /// <inheritdoc/>
        public void Kill()
        {
            process.Kill();
        }

        /// <inheritdoc/>
        public void Resize(int cols, int rows)
        {
            var handles = this.handles;
            if (handles == null || isDisposed)
            {
                throw new ObjectDisposedException(nameof(PseudoConsoleConnection));
            }

            var coord = new COORD { X = (short)cols, Y = (short)rows };
            var hr = ResizePseudoConsole((HPCON)handles.PseudoConsoleHandle.DangerousGetHandle(), coord);
            if (hr.Failed)
            {
                throw new InvalidOperationException(
                    $"Could not resize pseudo console: {hr}",
                    Marshal.GetExceptionForHR(hr.Value));
            }
        }

        /// <inheritdoc/>
        public bool WaitForExit(int milliseconds)
        {
            return process.WaitForExit(milliseconds);
        }

        private void Process_Exited(object? sender, EventArgs e)
        {
            // Check if we're disposed to avoid raising events during/after disposal
            if (isDisposed)
            {
                return;
            }

            ProcessExited?.Invoke(this, new PtyExitedEventArgs(process.ExitCode));
        }

        /// <summary>
        /// handles to resources creates when a pseudoconsole is spawned.
        /// </summary>
        internal sealed class PseudoConsoleConnectionHandles
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="PseudoConsoleConnectionHandles"/> class.
            /// </summary>
            /// <param name="inPipeOurSide">the input pipe on the local side (we write to this).</param>
            /// <param name="outPipeOurSide">the output pipe on the local side (we read from this).</param>
            /// <param name="pseudoConsoleHandle">the handle to the pseudoconsole.</param>
            /// <param name="processHandle">the handle to the spawned process.</param>
            /// <param name="pid">the process ID.</param>
            /// <param name="mainThreadHandle">the handle to the main thread.</param>
            /// <param name="jobObjectHandle">the handle to the job object that manages process lifetime.</param>
            public PseudoConsoleConnectionHandles(
                SafeFileHandle inPipeOurSide,
                SafeFileHandle outPipeOurSide,
                ClosePseudoConsoleSafeHandle pseudoConsoleHandle,
                SafeFileHandle processHandle,
                int pid,
                SafeFileHandle mainThreadHandle,
                SafeFileHandle jobObjectHandle)
            {
                InPipeOurSide = inPipeOurSide;
                OutPipeOurSide = outPipeOurSide;
                PseudoConsoleHandle = pseudoConsoleHandle;
                ProcessHandle = processHandle;
                Pid = pid;
                MainThreadHandle = mainThreadHandle;
                JobObjectHandle = jobObjectHandle;
            }

            /// <summary>
            /// Gets the input pipe on the local side (we write to this to send to console).
            /// </summary>
            internal SafeFileHandle InPipeOurSide { get; }

            /// <summary>
            /// Gets the output pipe on the local side (we read from this to get console output).
            /// </summary>
            internal SafeFileHandle OutPipeOurSide { get; }

            /// <summary>
            /// Gets the handle to the pseudoconsole.
            /// </summary>
            internal ClosePseudoConsoleSafeHandle PseudoConsoleHandle { get; }

            /// <summary>
            /// Gets the handle to the spawned process.
            /// </summary>
            internal SafeFileHandle ProcessHandle { get; }

            /// <summary>
            /// Gets the process ID.
            /// </summary>
            internal int Pid { get; }

            /// <summary>
            /// Gets the handle to the main thread.
            /// </summary>
            internal SafeFileHandle MainThreadHandle { get; }

            /// <summary>
            /// Gets the handle to the job object that manages process lifetime.
            /// When this handle is closed, all processes assigned to the job are terminated.
            /// </summary>
            internal SafeFileHandle JobObjectHandle { get; }
        }
    }
}
