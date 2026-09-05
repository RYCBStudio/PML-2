using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;

namespace Notify.NET.Platform.Linux
{
    /// <summary>
    /// Owns and manages a GLib GMainLoop on a dedicated background thread.
    ///
    /// libnotify delivers notification signals (action-invoked, closed) through the GLib
    /// event system and requires a running GMainLoop to dispatch them. Console applications
    /// and ASP.NET hosts do not have one by default, so this class creates and owns one.
    ///
    /// All calls to libnotify that create/show/close notifications MUST be executed on the
    /// GMainLoop thread to ensure proper GObject signal wiring. Use <see cref="InvokeAsync"/>
    /// to marshal work onto that thread.
    /// </summary>
    internal sealed class GLibMainLoopRunner : IDisposable
    {
        private readonly Thread _loopThread;
        private readonly ManualResetEventSlim _started = new ManualResetEventSlim(false);

        // Pinned GSourceFunc delegate — static, never collected.
        private static readonly LibNotifyNative.GSourceFunc _dispatchSourceFunc = DispatchSourceFuncStatic;
        private static readonly IntPtr _dispatchFuncPtr =
            Marshal.GetFunctionPointerForDelegate(_dispatchSourceFunc);

        // Work items posted from external threads via g_main_context_invoke.
        // We use a ConcurrentQueue keyed by a GCHandle to the WorkItem so we can pass a
        // single IntPtr through the GLib userData parameter.
        private IntPtr _mainLoop;
        private bool _disposed;

        internal GLibMainLoopRunner()
        {
            _loopThread = new Thread(LoopThreadProc)
            {
                Name = "Notify.NET GMainLoop",
                IsBackground = true
            };
            _loopThread.Start();
            _started.Wait();
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>
        /// Posts <paramref name="action"/> to be executed on the GMainLoop thread and
        /// returns a task that completes when the action finishes.
        /// </summary>
        internal System.Threading.Tasks.Task InvokeAsync(Action action)
        {
            var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
            var item = new WorkItem(action, tcs);

            // Allocate a GCHandle to keep the WorkItem alive from unmanaged code.
            GCHandle handle = GCHandle.Alloc(item, GCHandleType.Normal);

            // g_main_context_invoke(null) posts to the default context, which is owned
            // by our GMainLoop thread.
            LibNotifyNative.g_main_context_invoke(IntPtr.Zero, _dispatchFuncPtr, GCHandle.ToIntPtr(handle));

            return tcs.Task;
        }

        // ------------------------------------------------------------------
        // IDisposable
        // ------------------------------------------------------------------

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_mainLoop != IntPtr.Zero)
            {
                LibNotifyNative.g_main_loop_quit(_mainLoop);
                if (_loopThread.IsAlive)
                    _loopThread.Join(TimeSpan.FromSeconds(5));

                LibNotifyNative.g_main_loop_unref(_mainLoop);
                _mainLoop = IntPtr.Zero;
            }

            _started.Dispose();
        }

        // ------------------------------------------------------------------
        // Private
        // ------------------------------------------------------------------

        private void LoopThreadProc()
        {
            _mainLoop = LibNotifyNative.g_main_loop_new(IntPtr.Zero, false);
            _started.Set();
            LibNotifyNative.g_main_loop_run(_mainLoop);
            // g_main_loop_unref is called in Dispose, not here, to avoid double-unref.
        }

        // Static GSourceFunc — invoked on the GMainLoop thread by GLib.
        // Returns false so GLib removes the source after one invocation.
        private static bool DispatchSourceFuncStatic(IntPtr userData)
        {
            GCHandle handle = GCHandle.FromIntPtr(userData);
            var item = (WorkItem)handle.Target!;
            handle.Free();

            try   { item.Action(); item.Tcs.TrySetResult(true); }
            catch (Exception ex) { item.Tcs.TrySetException(ex); }

            return false; // one-shot
        }

        private sealed class WorkItem
        {
            internal readonly Action Action;
            internal readonly System.Threading.Tasks.TaskCompletionSource<bool> Tcs;
            internal WorkItem(Action action, System.Threading.Tasks.TaskCompletionSource<bool> tcs)
            { Action = action; Tcs = tcs; }
        }
    }
}
