using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Notify.NET.Platform
{
    /// <summary>
    /// A minimal single-instance forwarding channel shared by the Windows and Linux jump-list
    /// services. Implemented with a named pipe (which the .NET runtime maps to a named pipe on
    /// Windows and a Unix-domain socket on Linux), plus a named <see cref="Mutex"/> to elect the
    /// primary instance.
    ///
    /// The primary instance (the first to call <see cref="EnsureListening"/>) runs a background loop
    /// that accepts connections and invokes a callback with each received task id. Any instance can
    /// statically <see cref="TryForward"/> a task id to the primary; if no primary is listening the
    /// call returns <c>false</c> and the caller treats it as a cold start.
    ///
    /// Nothing here is created until a jump-list service actually needs it, honouring the
    /// "no overhead unless used" contract.
    /// </summary>
    internal sealed class SingleInstanceChannel : IDisposable
    {
        private readonly string _pipeName;
        private readonly Mutex _mutex;
        private readonly bool _isPrimary;

        private CancellationTokenSource? _cts;
        private Task? _listenTask;
        private volatile bool _disposed;

        /// <summary>Whether this process won the election and is the listening primary instance.</summary>
        internal bool IsPrimary => _isPrimary;

        internal SingleInstanceChannel(string channelName)
        {
            _pipeName = channelName;
            // initiallyOwned: true means we try to take ownership; createdNew tells us whether this
            // call created the kernel object, which we use as the primary-election signal.
            _mutex = new Mutex(initiallyOwned: true, name: channelName + "-mtx", out bool createdNew);
            _isPrimary = createdNew;
        }

        /// <summary>
        /// Starts the background accept loop if this process is the primary instance and the loop is
        /// not already running. Safe to call repeatedly. No-op for non-primary instances.
        /// </summary>
        internal void EnsureListening(Action<string> onActivated)
        {
            if (_disposed || !_isPrimary || _listenTask != null) return;

            _cts = new CancellationTokenSource();
            _listenTask = Task.Run(() => AcceptLoopAsync(onActivated, _cts.Token));
        }

        private async Task AcceptLoopAsync(Action<string> onActivated, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        _pipeName,
                        PipeDirection.In,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);

                    string taskId = await ReadAllAsync(server, token).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(taskId))
                    {
                        try { onActivated(taskId.Trim()); }
                        catch { /* never let a handler exception kill the accept loop */ }
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception)
                {
                    // Transient pipe error — pause briefly so we don't spin on a persistent failure.
                    try { await Task.Delay(50, token).ConfigureAwait(false); }
                    catch (OperationCanceledException) { return; }
                }
            }
        }

        private static async Task<string> ReadAllAsync(Stream stream, CancellationToken token)
        {
            var buffer = new byte[256];
            var sb = new StringBuilder();
            int read;
            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0)
                sb.Append(Encoding.UTF8.GetString(buffer, 0, read));
            return sb.ToString();
        }

        /// <summary>
        /// Attempts to deliver <paramref name="taskId"/> to a primary instance listening on
        /// <paramref name="pipeName"/>. Returns <c>true</c> if a primary accepted the connection and
        /// the id was written; <c>false</c> if no primary is listening (a cold start).
        /// </summary>
        internal static bool TryForward(string pipeName, string taskId, int timeoutMs = 400)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
                client.Connect(timeoutMs);
                byte[] payload = Encoding.UTF8.GetBytes(taskId);
                client.Write(payload, 0, payload.Length);
                client.Flush();
                return true;
            }
            catch (TimeoutException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try { _cts?.Cancel(); } catch { /* best effort */ }

            try
            {
                // Unblock a pending WaitForConnectionAsync by briefly connecting to our own pipe.
                if (_isPrimary && _listenTask != null)
                {
                    try
                    {
                        using var unblock = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
                        unblock.Connect(100);
                    }
                    catch { /* listener may already be gone */ }
                    _listenTask.Wait(TimeSpan.FromSeconds(2));
                }
            }
            catch { /* best effort */ }

            _cts?.Dispose();

            try
            {
                if (_isPrimary) _mutex.ReleaseMutex();
            }
            catch { /* not owned / already released */ }
            _mutex.Dispose();
        }
    }
}
