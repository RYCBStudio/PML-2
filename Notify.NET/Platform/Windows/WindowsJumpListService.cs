using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Notify.NET.Abstractions;
using Notify.NET.Platform;

namespace Notify.NET.Platform.Windows
{
    /// <summary>
    /// <see cref="IJumpListService"/> implementation backed by the shell
    /// <c>ICustomDestinationList</c> "user tasks" API (Windows 7+). No native wrapper DLL is required.
    ///
    /// Each task is an <c>IShellLink</c> that relaunches the host executable with
    /// <c>--notify-jumplist &lt;id&gt;</c>; the bundled <see cref="SingleInstanceChannel"/> forwards the
    /// id to the already-running primary instance so <see cref="IJumpListHandler.OnTaskActivated"/>
    /// fires live.
    ///
    /// Threading model:
    ///   The destination-list COM objects are apartment-threaded, so all COM work runs on a dedicated
    ///   STA thread (created lazily on first use), mirroring <see cref="WindowsTaskbarProgressService"/>.
    /// </summary>
    public sealed class WindowsJumpListService : IJumpListService
    {
        private readonly string _appUserModelId;
        private readonly string _executablePath;
        private readonly JumpListActivationRouter _router;
        private readonly object _gate = new object();

        private Thread? _staThread;
        private BlockingCollection<Action>? _workQueue;
        private ManualResetEventSlim? _staReady;
        private volatile bool _disposed;

        /// <inheritdoc/>
        public bool IsSupported { get; }

        /// <param name="appUserModelId">
        /// The same AppUserModelId used for notifications, so the jump list attaches to the correct
        /// taskbar button.
        /// </param>
        /// <param name="executablePath">
        /// Absolute path to the executable to relaunch when a task is clicked. When null, the current
        /// process executable is used.
        /// </param>
        public WindowsJumpListService(string appUserModelId, string? executablePath = null)
        {
            _appUserModelId = appUserModelId ?? throw new ArgumentNullException(nameof(appUserModelId));
            _executablePath = executablePath ?? JumpListActivation.CurrentExecutablePath();
            _router = new JumpListActivationRouter(JumpListActivation.ChannelName(_appUserModelId));

            // Jump lists require Windows 7+. The shell coclasses are present from Win7 onward;
            // treat the platform as supported and degrade gracefully if COM creation fails.
            IsSupported = true;
        }

        // ------------------------------------------------------------------
        // IJumpListService
        // ------------------------------------------------------------------

        /// <inheritdoc/>
        public bool TryHandleActivation(string[] args)
        {
            if (!IsSupported || _disposed) return false;
            return _router.TryHandleActivation(args);
        }

        /// <inheritdoc/>
        public void SetHandler(IJumpListHandler? handler)
        {
            if (!IsSupported || _disposed) return;
            _router.SetHandler(handler);
        }

        /// <inheritdoc/>
        public void SetTasks(IEnumerable<JumpListTask> tasks)
        {
            if (!IsSupported || _disposed) return;
            if (tasks == null) throw new ArgumentNullException(nameof(tasks));

            var list = new List<JumpListTask>(tasks);
            _router.EnsureListening();
            EnqueueOnSta(() => BuildList(list));
        }

        /// <inheritdoc/>
        public void ClearTasks()
        {
            if (!IsSupported || _disposed) return;
            EnqueueOnSta(DeleteList);
        }

        // ------------------------------------------------------------------
        // IDisposable
        // ------------------------------------------------------------------

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _workQueue?.CompleteAdding();
            if (_staThread != null && _staThread.IsAlive)
                _staThread.Join(TimeSpan.FromSeconds(5));

            _workQueue?.Dispose();
            _staReady?.Dispose();
            _router.Dispose();
        }

        // ------------------------------------------------------------------
        // STA worker
        // ------------------------------------------------------------------

        private void EnqueueOnSta(Action action)
        {
            EnsureStaThread();
            try { _workQueue!.Add(action); }
            catch (InvalidOperationException) { /* queue completed — disposed */ }
        }

        private void EnsureStaThread()
        {
            if (_staThread != null) return;
            lock (_gate)
            {
                if (_staThread != null) return;

                _workQueue = new BlockingCollection<Action>();
                _staReady = new ManualResetEventSlim(false);
                _staThread = new Thread(StaThreadProc)
                {
                    Name = "Notify.NET JumpList STA",
                    IsBackground = true
                };
                _staThread.SetApartmentState(ApartmentState.STA);
                _staThread.Start();
                _staReady.Wait();
            }
        }

        private void StaThreadProc()
        {
            _staReady!.Set();
            try
            {
                foreach (Action work in _workQueue!.GetConsumingEnumerable())
                {
                    try { work(); }
                    catch { /* a single failed list build must not stop the worker */ }
                }
            }
            catch (InvalidOperationException) { /* queue completed */ }
        }

        // ------------------------------------------------------------------
        // Jump-list construction (runs on the STA thread)
        // ------------------------------------------------------------------

        private void BuildList(List<JumpListTask> tasks)
        {
            if (tasks.Count == 0) { DeleteList(); return; }

            CustomDestinationListNative.ICustomDestinationList? list = null;
            CustomDestinationListNative.IObjectCollection? collection = null;
            object? removed = null;
            try
            {
                list = (CustomDestinationListNative.ICustomDestinationList)
                    new CustomDestinationListNative.CDestinationList();
                list.SetAppID(_appUserModelId);

                Guid riid = CustomDestinationListNative.IID_IObjectArray;
                list.BeginList(out _, ref riid, out removed);

                collection = (CustomDestinationListNative.IObjectCollection)
                    new CustomDestinationListNative.CEnumerableObjectCollection();

                foreach (JumpListTask task in tasks)
                {
                    object? link = CreateTaskLink(task);
                    if (link != null) collection.AddObject(link);
                }

                list.AddUserTasks((CustomDestinationListNative.IObjectArray)collection);
                list.CommitList();
            }
            catch (Exception)
            {
                // Abort a half-built list so the previous one is preserved.
                try { list?.AbortList(); } catch { /* best effort */ }
            }
            finally
            {
                ReleaseCom(removed);
                ReleaseCom(collection);
                ReleaseCom(list);
            }
        }

        private object? CreateTaskLink(JumpListTask task)
        {
            CustomDestinationListNative.IShellLinkW? link = null;
            try
            {
                link = (CustomDestinationListNative.IShellLinkW)
                    new CustomDestinationListNative.CShellLink();

                link.SetPath(_executablePath);
                link.SetArguments($"{JumpListActivation.ActivationFlag} {task.Id}");

                string? workingDir = Path.GetDirectoryName(_executablePath);
                if (!string.IsNullOrEmpty(workingDir))
                    link.SetWorkingDirectory(workingDir);

                if (!string.IsNullOrEmpty(task.Description))
                    link.SetDescription(task.Description);

                // Icon: explicit override, else the host executable's own icon.
                if (!string.IsNullOrEmpty(task.IconPath))
                    link.SetIconLocation(task.IconPath, task.IconIndex);
                else
                    link.SetIconLocation(_executablePath, 0);

                // The title is mandatory for a user task to be shown.
                var store = (CustomDestinationListNative.IPropertyStore)link;
                CustomDestinationListNative.SetStringValue(
                    store, CustomDestinationListNative.PKEY_Title, task.Title);

                return link;
            }
            catch (Exception)
            {
                ReleaseCom(link);
                return null;
            }
        }

        private void DeleteList()
        {
            CustomDestinationListNative.ICustomDestinationList? list = null;
            try
            {
                list = (CustomDestinationListNative.ICustomDestinationList)
                    new CustomDestinationListNative.CDestinationList();
                list.DeleteList(_appUserModelId);
            }
            catch (Exception) { /* nothing to delete or shell unavailable */ }
            finally { ReleaseCom(list); }
        }

        private static void ReleaseCom(object? comObject)
        {
            if (comObject != null && Marshal.IsComObject(comObject))
            {
                try { Marshal.FinalReleaseComObject(comObject); }
                catch { /* best effort */ }
            }
        }
    }
}
