using System;
using System.Collections.Generic;

namespace Notify.NET.Abstractions
{
    /// <summary>
    /// Manages the application's jump list (Windows), launcher shortcut menu (Linux
    /// <c>.desktop</c> Actions) or Dock menu (macOS), with a bundled live-callback layer so a
    /// clicked task is delivered to the already-running instance via
    /// <see cref="IJumpListHandler.OnTaskActivated"/>.
    ///
    /// <para><b>Activation model.</b> Jump-list and <c>.desktop</c> tasks fundamentally relaunch
    /// the executable; macOS Dock menus fire in-process. To present a single, uniform live-callback
    /// API across all three, this service bundles a single-instance channel:</para>
    /// <list type="number">
    ///   <item><description>
    ///     A clicked task on Windows/Linux relaunches the app with a hidden activation argument.
    ///   </description></item>
    ///   <item><description>
    ///     Call <see cref="TryHandleActivation"/> at the very start of <c>Main</c>. If this launch is
    ///     such a relaunch and a primary instance is already running, the activation is forwarded to
    ///     it over a named-pipe channel and the method returns <c>true</c> — the caller should exit
    ///     immediately without showing any UI.
    ///   </description></item>
    ///   <item><description>
    ///     Otherwise the method returns <c>false</c> and the app continues normal startup. The first
    ///     call to <see cref="SetTasks"/> registers the OS jump list and (lazily) starts the
    ///     single-instance listener, making this process the primary instance. If this launch was a
    ///     cold-start activation (no primary was running), the pending task is replayed to the
    ///     handler once one is set.
    ///   </description></item>
    /// </list>
    ///
    /// <para>Nothing is registered and no listener, mutex or pipe is created until
    /// <see cref="SetTasks"/> (or <see cref="SetHandler"/>) is first called, so applications that do
    /// not use jump lists incur no overhead.</para>
    ///
    /// <para>Capability notes:</para>
    /// <list type="bullet">
    ///   <item><description>
    ///     <b>Windows</b> — uses the shell <c>ICustomDestinationList</c> "user tasks" (Windows 7+).
    ///     Requires the same AppUserModelId used for notifications so the list attaches to the
    ///     correct taskbar button.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Linux</b> — writes <c>Actions</c> into the application's <c>.desktop</c> file (honoured
    ///     by GNOME, KDE, Unity and others). Requires <c>NotificationOptions.DesktopFileId</c>; if no
    ///     installed <c>.desktop</c> file is found, a minimal one is created under
    ///     <c>~/.local/share/applications</c>.
    ///   </description></item>
    ///   <item><description>
    ///     <b>macOS</b> — adds items to the Dock menu via the application delegate. Only effective for
    ///     a bundled GUI application with a running main loop; a bare console process has no Dock
    ///     menu. No relaunch or forwarding is involved.
    ///   </description></item>
    /// </list>
    /// When jump lists are not available on the current platform, <see cref="IsSupported"/> is
    /// <c>false</c> and all methods are silent no-ops (<see cref="TryHandleActivation"/> returns
    /// <c>false</c>).
    /// </summary>
    public interface IJumpListService : IDisposable
    {
        /// <summary>
        /// Whether jump lists / launcher actions / Dock-menu items are available on this platform.
        /// When <c>false</c>, all other methods are silent no-ops.
        /// </summary>
        bool IsSupported { get; }

        /// <summary>
        /// Registers the handler that receives <see cref="IJumpListHandler.OnTaskActivated"/> events.
        /// Calling this with a non-null handler also starts the single-instance listener (if not
        /// already started) and replays any activation captured during a cold start. Pass <c>null</c>
        /// to detach the current handler.
        /// </summary>
        void SetHandler(IJumpListHandler? handler);

        /// <summary>
        /// Replaces the application's jump-list tasks with the supplied set. The first call also
        /// starts the single-instance listener, making this process the primary instance. An empty
        /// sequence is equivalent to <see cref="ClearTasks"/>.
        /// </summary>
        void SetTasks(IEnumerable<JumpListTask> tasks);

        /// <summary>Removes all jump-list tasks registered by this application.</summary>
        void ClearTasks();

        /// <summary>
        /// Inspects the process command-line arguments for a jump-list activation. Call this once, as
        /// early as possible in <c>Main</c>, before any UI is shown.
        /// </summary>
        /// <param name="args">The arguments passed to <c>Main</c>.</param>
        /// <returns>
        /// <c>true</c> if this launch was a jump-list activation that has been forwarded to an
        /// already-running primary instance and the caller should exit immediately; otherwise
        /// <c>false</c> (continue normal startup — the activation, if any, will be replayed to the
        /// handler once this instance becomes primary).
        /// </returns>
        bool TryHandleActivation(string[] args);
    }
}
