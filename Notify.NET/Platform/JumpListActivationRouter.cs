using System;
using System.Threading;
using Notify.NET.Abstractions;

namespace Notify.NET.Platform
{
    /// <summary>
    /// Encapsulates the single-instance activation logic shared by the Windows and Linux jump-list
    /// services: forwarding a clicked task to the primary instance, listening for forwarded
    /// activations, and replaying a cold-start activation once a handler is registered.
    ///
    /// The owning service supplies only the platform-specific channel key and consumes the routed
    /// task ids via the handler it sets. Nothing is created until <see cref="EnsureListening"/> or a
    /// non-null <see cref="SetHandler"/> is first called.
    /// </summary>
    internal sealed class JumpListActivationRouter : IDisposable
    {
        private readonly string _channelName;
        private readonly object _gate = new object();

        private SingleInstanceChannel? _channel;
        private IJumpListHandler? _handler;
        private string? _pending;
        private bool _disposed;

        internal JumpListActivationRouter(string channelName)
        {
            _channelName = channelName;
        }

        /// <summary>
        /// Handles a possible jump-list activation command line. Returns <c>true</c> if the activation
        /// was forwarded to an already-running primary instance (caller should exit); otherwise
        /// <c>false</c> (the activation, if any, is captured for cold-start replay).
        /// </summary>
        internal bool TryHandleActivation(string[] args)
        {
            if (_disposed) return false;

            string? taskId = JumpListActivation.TryParseTaskId(args);
            if (taskId == null) return false;

            if (SingleInstanceChannel.TryForward(_channelName, taskId))
                return true;

            lock (_gate) _pending = taskId;
            return false;
        }

        /// <summary>Sets (or clears) the handler and starts listening when a handler is attached.</summary>
        internal void SetHandler(IJumpListHandler? handler)
        {
            if (_disposed) return;
            lock (_gate)
            {
                _handler = handler;
                if (handler != null) EnsureListening_NoLock();
            }
        }

        /// <summary>
        /// Becomes the primary instance (if elected) and begins listening for forwarded activations.
        /// Called by the service the first time tasks are registered.
        /// </summary>
        internal void EnsureListening()
        {
            if (_disposed) return;
            lock (_gate) EnsureListening_NoLock();
        }

        private void EnsureListening_NoLock()
        {
            _channel ??= new SingleInstanceChannel(_channelName);
            _channel.EnsureListening(OnForwardedActivation);
            ReplayPending_NoLock();
        }

        private void ReplayPending_NoLock()
        {
            if (_handler == null || _pending == null) return;
            if (_channel == null || !_channel.IsPrimary) return;

            string id = _pending;
            _pending = null;
            IJumpListHandler handler = _handler;
            ThreadPool.QueueUserWorkItem(_ => SafeInvoke(handler, id));
        }

        private void OnForwardedActivation(string taskId)
        {
            IJumpListHandler? handler;
            lock (_gate) handler = _handler;
            if (handler != null) SafeInvoke(handler, taskId);
        }

        private static void SafeInvoke(IJumpListHandler handler, string taskId)
        {
            try { handler.OnTaskActivated(taskId); }
            catch { /* a handler exception must never crash the listener */ }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _channel?.Dispose();
                _channel = null;
            }
        }
    }
}
