using System;
using System.Collections.Generic;
using Notify.NET.Abstractions;
using Notify.NET.Platform;

namespace Notify.NET.Platform.Linux
{
    /// <summary>
    /// <see cref="IJumpListService"/> implementation that registers launcher shortcut tasks as
    /// freedesktop.org Desktop Actions in the application's <c>.desktop</c> file (see
    /// <see cref="DesktopFileWriter"/>), honoured by GNOME, KDE, Unity and others.
    ///
    /// Clicking an action relaunches the executable with <c>--notify-jumplist &lt;id&gt;</c>; the bundled
    /// <see cref="JumpListActivationRouter"/> forwards the id to the running primary instance so
    /// <see cref="IJumpListHandler.OnTaskActivated"/> fires live (or replays it on a cold start).
    /// </summary>
    public sealed class LinuxJumpListService : IJumpListService
    {
        private readonly string _appName;
        private readonly string _desktopFileId;
        private readonly string _executablePath;
        private readonly JumpListActivationRouter _router;
        private volatile bool _disposed;

        /// <inheritdoc/>
        public bool IsSupported => true;

        /// <param name="appName">Human-readable application name, used if a new .desktop file is created.</param>
        /// <param name="desktopFileId">
        /// The application's <c>.desktop</c> file id (with or without the ".desktop" suffix). Identifies
        /// which launcher entry the actions are written into and keys the single-instance channel.
        /// </param>
        /// <param name="executablePath">
        /// Absolute command used to relaunch the app for an action's <c>Exec</c>. When null, the
        /// current process executable is used (note: for framework-dependent dotnet apps this may be
        /// the host; pass an explicit path for those).
        /// </param>
        public LinuxJumpListService(string appName, string desktopFileId, string? executablePath = null)
        {
            _appName = appName ?? throw new ArgumentNullException(nameof(appName));
            _desktopFileId = desktopFileId ?? throw new ArgumentNullException(nameof(desktopFileId));
            _executablePath = executablePath ?? JumpListActivation.CurrentExecutablePath();
            _router = new JumpListActivationRouter(JumpListActivation.ChannelName(_desktopFileId));
        }

        /// <inheritdoc/>
        public bool TryHandleActivation(string[] args)
        {
            if (_disposed) return false;
            return _router.TryHandleActivation(args);
        }

        /// <inheritdoc/>
        public void SetHandler(IJumpListHandler? handler)
        {
            if (_disposed) return;
            _router.SetHandler(handler);
        }

        /// <inheritdoc/>
        public void SetTasks(IEnumerable<JumpListTask> tasks)
        {
            if (_disposed) return;
            if (tasks == null) throw new ArgumentNullException(nameof(tasks));

            var list = new List<JumpListTask>(tasks);
            _router.EnsureListening();

            try
            {
                if (list.Count == 0)
                    DesktopFileWriter.RemoveActions(_desktopFileId);
                else
                    DesktopFileWriter.WriteActions(_desktopFileId, _appName, _executablePath, list);
            }
            catch (Exception)
            {
                // Writing the .desktop file is best-effort; a read-only or absent home directory
                // must not bring the application down.
            }
        }

        /// <inheritdoc/>
        public void ClearTasks()
        {
            if (_disposed) return;
            try { DesktopFileWriter.RemoveActions(_desktopFileId); }
            catch (Exception) { /* best effort */ }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _router.Dispose();
        }
    }
}
